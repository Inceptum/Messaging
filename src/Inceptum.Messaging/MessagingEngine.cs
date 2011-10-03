using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reactive.Disposables;
using System.Threading;
using Castle.Core.Logging;
using Inceptum.Core;
using Inceptum.Core.Messaging;
using Inceptum.Core.Utils;
using Sonic.Jms;

namespace Inceptum.Messaging
{
    public class MessagingEngine : IMessagingEngine
    {

        private const int MESSAGE_LIFESPAN = 1800000; // milliseconds (30 minutes)
        private const string JAILED_PROPERTY_NAME = "JAILED_TAG";
        private readonly ManualResetEvent m_Disposing = new ManualResetEvent(false);
        private readonly string m_JailedTag;
        private readonly CountingTracker m_RequestsTracker = new CountingTracker();
        private readonly ISerializationManager m_SerializationManager;
        private readonly List<IDisposable> m_SonicHandles = new List<IDisposable>();
        private readonly TransportManager m_TransportManager;
        private ILogger m_Logger = NullLogger.Instance;

        /// <summary>
        /// ctor for tests
        /// </summary>
        /// <param name="transportManager"></param>
        /// <param name="serializationManager"></param>
        /// <param name="jailStrategy"></param>
        internal MessagingEngine(TransportManager transportManager, ISerializationManager serializationManager, JailStrategy jailStrategy = null)
        {
            m_JailedTag = (jailStrategy ?? JailStrategy.None).CreateTag();
            JailedSelector = JAILED_PROPERTY_NAME + " = \'" + m_JailedTag + "\'";
            m_TransportManager =transportManager;
            m_SerializationManager = serializationManager;
            
        }
        public MessagingEngine(ITransportResolver transportResolver, ISerializationManager serializationManager, JailStrategy jailStrategy = null)
            : this(new TransportManager(transportResolver),serializationManager,jailStrategy)
        {
        }

        public bool Jailed
        {
            get { return m_JailedTag != null; }
        }



        public ILogger Logger
        {
            get { return m_Logger; }
            set { m_Logger = value; }
        }

        internal string JailedSelector { get; private set; }

        #region IMessagingEngine Members

        public IDisposable SubscribeOnTransportEvents(TrasnportEventHandler handler)
        {
            TrasnportEventHandler safeHandler = (transportId, @event) =>
                                            {
                                                try
                                                {
                                                    handler(transportId, @event);
                                                }
                                                catch (Exception ex)
                                                {
                                                    Logger.WarnFormat(ex, "transport events handler failed");
                                                }
                                            };
            m_TransportManager.TransportEvents += safeHandler;
            return Disposable.Create(() => m_TransportManager.TransportEvents -= safeHandler);
        }

        public void Send<TMessage>(TMessage message, string destination, string transportId)
        {
            if (destination == null) throw new ArgumentNullException("destination");
            if (m_Disposing.WaitOne(0))
                throw new InvalidOperationException("Engine is disposing");

            using (m_RequestsTracker.Track())
            {
                try
                {
                    Destination sendDestination;
                    Session session = getSessionAndCreateDestination(destination, transportId, out sendDestination);
                    var sender = session.createProducer(sendDestination);
                    
                    using (Disposable.Create(sender.close))
                    {
                        var serializedMessage = serializeMessage(message, session);
                        //TODO: arrange TTL
                        sender.send(serializedMessage, DeliveryMode.NON_PERSISTENT, DefaultMessageProperties.DEFAULT_PRIORITY, MESSAGE_LIFESPAN);
                    }
                }
                catch (Exception e)
                {
                    Logger.ErrorFormat(e, "Failed to send message. Transport: {0}, Queue: {1}", transportId, destination);
                    throw;
                }
            }
        }

        private Session getSessionAndCreateDestination (string destination, string transportId, out Destination sendDestination)
        {
            Session session;
            if (destination.StartsWith("queue://", true, CultureInfo.InvariantCulture))
            {
                session = m_TransportManager.GetSession(transportId);
                sendDestination = session.createQueue(destination.Substring(8));
            }
            else if (destination.StartsWith("topic://", true, CultureInfo.InvariantCulture))
            {
                session = m_TransportManager.GetSession(transportId, true);
                sendDestination = session.createTopic(destination.Substring(8));
            }
            else
                throw new InvalidOperationException("Wrong destionation name: " + destination + ". Should start with 'queue://' or 'topic://'");
            return session;
        }

        public IDisposable Subscribe<TMessage>(string source, string transportId, Action<TMessage> callback )
        {
            if (m_Disposing.WaitOne(0))
                throw new InvalidOperationException("Engine is disposing");

            using (m_RequestsTracker.Track())
            {
                try
                {
                    return subscribe(source, transportId, m => processMessage(m, callback));
                }
                catch (Exception e)
                {
                    Logger.ErrorFormat(e, "Failed to subscribe. Transport: {0}, Queue: {1}", transportId, source);
                    throw;
                }
            }
        }


        //NOTE: send via topic waits only first response.
        public TResponse SendRequest<TRequest, TResponse>(TRequest request, string queue, string transportId, int timeout = 30000)
        {
            if (m_Disposing.WaitOne(0))
                throw new InvalidOperationException("Engine is disposing");

            using (m_RequestsTracker.Track())
            {
                var responseRecieved = new ManualResetEvent(false);
                var response = default(TResponse);
                Exception exception = null;

                using (SendRequestAsync<TRequest, TResponse>(request, queue, transportId,
                                                             r =>
                                                                 {
                                                                     response = r;
                                                                     responseRecieved.Set();
                                                                 },
                                                             ex =>
                                                                 {
                                                                     exception = ex;
                                                                     responseRecieved.Set();
                                                                 }))
                {
                    var waitResult = WaitHandle.WaitAny(new WaitHandle[] {m_Disposing, responseRecieved}, timeout);
                    switch (waitResult)
                    {
                        case 1:
                            if (exception == null)
                                return response;
                            throw new ProcessingException("Failed to process response", exception);
                        case 0:
                            throw new ProcessingException("Request was cancelled due to engine dispose", exception);
                        case WaitHandle.WaitTimeout:
                            throw new TimeoutException();
                        default:
                            throw new InvalidOperationException();
                    }
                }
            }
        }


        public IDisposable RegisterHandler<TRequest, TResponse>(Func<TRequest, TResponse> handler, string source, string transportId)
            where TResponse : class
        {
            if (m_Disposing.WaitOne(0))
                throw new InvalidOperationException("Engine is disposing");

            using (m_RequestsTracker.Track())
            {
                try
                {
                     Session session = m_TransportManager.GetSession(transportId);
                    var replier = session.createProducer(null);
                    var requestSubscription = subscribe(source, transportId, m => handleRequest(m, handler, session, replier));
                    return createSonicHandle(() =>
                                                 {
                                                     replier.close();
                                                     requestSubscription.Dispose();
                                                 });
                }
                catch (Exception e)
                {
                    Logger.ErrorFormat(e, "Failed to register handler. Transport: {0}, Queue: {1}", transportId, source);
                    throw;
                }
            }
        }

        public IDisposable SendRequestAsync<TRequest, TResponse>(TRequest request, string destination, string transportId, Action<TResponse> callback, Action<Exception> onFailure)
        {
            if (m_Disposing.WaitOne(0))
                throw new InvalidOperationException("Engine is disposing");

            using (m_RequestsTracker.Track())
            {
                try
                {
                    Destination requestDestination;
                    Session session = getSessionAndCreateDestination(destination, transportId, out requestDestination);
                    var tempDestination = session.CreateTempDestination();
                    var sender = session.createProducer(requestDestination);

                    using (Disposable.Create(sender.close))
                    {
                        MessageConsumer receiver;
                        if (Jailed)
                            receiver = session.createConsumer(tempDestination, JailedSelector);
                        else
                            receiver = session.createConsumer(tempDestination);

//                        var receiver = session.createConsumer(tempDestination, JailedSelector);
                        var handle = createSonicHandle(() =>
                                                           {
                                                               receiver.close();
                                                               tempDestination.Delete();
                                                           });

                        receiver.setMessageListener(new GenericMessageListener(message =>
                                                                                   {
                                                                                       try
                                                                                       {
                                                                                           var responseMessage = m_SerializationManager.Deserialize<TResponse>(message);
                                                                                           callback(responseMessage);
                                                                                           handle.Dispose();
                                                                                       }
                                                                                       catch (Exception e)
                                                                                       {
                                                                                           onFailure(e);
                                                                                       }
                                                                                   }));

                        Message requestMessage = serializeMessage(request, session);
                        requestMessage.setJMSReplyTo(tempDestination);
                        sender.send(requestMessage);
                        return handle;
                    }
                }
                catch (Exception e)
                {
                    Logger.ErrorFormat(e, "Failed to register handler. Transport: {0}, Destination: {1}", transportId, destination);
                    throw;
                }
            }
        }

        public void Dispose()
        {
            m_Disposing.Set();
            m_RequestsTracker.WaitAll();
            lock (m_SonicHandles)
            {
                foreach (var sonicHandle in m_SonicHandles)
                {
                    sonicHandle.Dispose();
                }
            }
            m_TransportManager.Dispose();
        }

        #endregion
 

        private static Queue createSonicQueue(string name,Session sendSession)
        {
            if (name.StartsWith("queue://", true, CultureInfo.InvariantCulture))
                return sendSession.createQueue(name.Substring(8));
            throw new InvalidOperationException("Wrong queue name: " + name + ". Should start with 'queue://'");
        }

        private Message serializeMessage<TMessage>(TMessage message, Session session)
        {
            Message serializedMessage = m_SerializationManager.Serialize(message, session);
            if (Jailed)
                serializedMessage.setStringProperty(JAILED_PROPERTY_NAME, m_JailedTag);
            return serializedMessage;
        }

        private IDisposable subscribe(string source, string transportId, Action<Message> callback)
        {
            Destination sonicSource;
            Session session=getSessionAndCreateDestination(source, transportId, out sonicSource);

            MessageConsumer receiver;
            if (Jailed)
                receiver = session.createConsumer(sonicSource, JailedSelector);
            else
                receiver = session.createConsumer(sonicSource);
            receiver.setMessageListener(new GenericMessageListener(callback));
            return createSonicHandle(receiver.close);
        }

        private IDisposable createSonicHandle(Action destroy)
        {
            IDisposable handle = null;

            handle = Disposable.Create(() =>
                                           {
                                               destroy();
                                               lock (m_SonicHandles)
                                               {
// ReSharper disable AccessToModifiedClosure
                                                   m_SonicHandles.Remove(handle);
// ReSharper restore AccessToModifiedClosure
                                               }
                                           });
            lock (m_SonicHandles)
            {
                m_SonicHandles.Add(handle);
            }
            return handle;
        }

        private void handleRequest<TRequest, TResponse>(Message message, Func<TRequest, TResponse> handler, Session session, MessageProducer replier)
        {
            try
            {
                var request = m_SerializationManager.Deserialize<TRequest>(message);
                var replyQueue = message.getJMSReplyTo() as Queue;
                TResponse response = handler(request);
                var responseMessage = serializeMessage(response, session);
                responseMessage.setJMSCorrelationID(message.getJMSMessageID());
                if (replyQueue != null)
                {
                    replier.send(replyQueue, responseMessage);
                }
            }
            catch (Exception e)
            {
                Logger.ErrorFormat(e, "Failed to handle request.");
            }
        }

        private void processMessage<TMessage, TSonicMessage>(TSonicMessage sonicMessage, Action<TMessage> callback)
            where TSonicMessage : Message
        {
            try
            {
                var message = m_SerializationManager.Deserialize<TMessage>(sonicMessage);
                callback(message);
            }
            catch (Exception e)
            {
                Logger.ErrorFormat(e, "Failed to handle message.");
            }
        }
    }
}
