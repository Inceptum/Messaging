using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Text;
using System.Threading;
using Castle.Core.Logging;
using Inceptum.Core.Messaging;
using Inceptum.Core.Utils;
using Sonic.Jms;
using Queue = Sonic.Jms.Queue;

namespace Inceptum.Messaging
{
    public class MessagingEngine : IMessagingEngine
    {
        private const int MESSAGE_LIFESPAN = 0;// forever // 1800000; // milliseconds (30 minutes)
        private readonly ManualResetEvent m_Disposing = new ManualResetEvent(false);
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
        internal MessagingEngine(TransportManager transportManager, ISerializationManager serializationManager)
        {
            m_TransportManager = transportManager;
            m_SerializationManager = serializationManager;
        }

        public MessagingEngine(ITransportResolver transportResolver, ISerializationManager serializationManager)
            : this(new TransportManager(transportResolver), serializationManager)
        {
        }

        //public bool Jailed
        //{
        //    get { return m_JailedTag != null; }
        //}


        public ILogger Logger
        {
            get { return m_Logger; }
            set { m_Logger = value; }
        }

        //internal string JailedSelector { get; private set; }

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
                    Sonic.Jms.Session session;
                    var transport = m_TransportManager.GetTransport(transportId);
                    var sender = transport.CreateProducer(destination, out session);

                    using (Disposable.Create(sender.close))
                    {
                        Message serializedMessage = serializeMessage(message, session, transport);
                        //TODO: arrange TTL
                        sender.send(serializedMessage, DeliveryMode.PERSISTENT,
                                    DefaultMessageProperties.DEFAULT_PRIORITY, MESSAGE_LIFESPAN);
                    }
                }
                catch (Exception e)
                {
                    Logger.ErrorFormat(e, "Failed to send message. Transport: {0}, Queue: {1}", transportId, destination);
                    throw;
                }
            }
        }

        public IDisposable Subscribe<TMessage>(string source, string transportId, Action<TMessage> callback)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (m_Disposing.WaitOne(0))
                throw new InvalidOperationException("Engine is disposing");

            using (m_RequestsTracker.Track())
            {
                try
                {
                    return subscribe(source, transportId, m => processMessage(m, callback, source, transportId));
                }
                catch (Exception e)
                {
                    Logger.ErrorFormat(e, "Failed to subscribe. Transport: {0}, Queue: {1}", transportId, source);
                    throw;
                }
            }
        }


        //NOTE: send via topic waits only first response.
        public TResponse SendRequest<TRequest, TResponse>(TRequest request, string queue, string transportId,
                                                          int timeout = 30000)
        {
            if (m_Disposing.WaitOne(0))
                throw new InvalidOperationException("Engine is disposing");

            using (m_RequestsTracker.Track())
            {
                var responseRecieved = new ManualResetEvent(false);
                TResponse response = default(TResponse);
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
                    int waitResult = WaitHandle.WaitAny(new WaitHandle[] {m_Disposing, responseRecieved}, timeout);
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


        public IDisposable RegisterHandler<TRequest, TResponse>(Func<TRequest, TResponse> handler, string source,
                                                                string transportId)
            where TResponse : class
        {
            var handle = new SerialDisposable();
            IDisposable transportWatcher = SubscribeOnTransportEvents((id, @event) =>
                                                                          {
                                                                              if (@event != TransportEvents.Failure)
                                                                                  return;
                                                                              registerHandlerWithRetry(handler, source, transportId, handle);
                                                                          });
                
           registerHandlerWithRetry(handler, source, transportId, handle);

            return new CompositeDisposable(transportWatcher, handle);
        }

        public void registerHandlerWithRetry<TRequest, TResponse>(Func<TRequest, TResponse> handler,
                                                                         string source, string transportId, SerialDisposable handle)
            where TResponse : class
        {
           
            lock (handle)
            {
                try
                {
                    handle.Disposable = registerHandler(handler, source, transportId);
                }
                catch
                {
                    Logger.InfoFormat("Scheduling register handler attempt in 1 minute. Transport: {0}, Queue: {1}", transportId, source);
                    handle.Disposable = Scheduler.ThreadPool.Schedule(DateTimeOffset.Now.AddMinutes(1),
                                                                      () =>
                                                                          {
                                                                              lock (handle)
                                                                              {
                                                                                  registerHandlerWithRetry(handler, source, transportId,handle);
                                                                              }
                                                                          });
                }
            }
        }


        public IDisposable registerHandler<TRequest, TResponse>(Func<TRequest, TResponse> handler, string source,
                                                                string transportId)
            where TResponse : class
        {
            if (m_Disposing.WaitOne(0))
                throw new InvalidOperationException("Engine is disposing");

            using (m_RequestsTracker.Track())
            {
                try
                {
                    var transport = m_TransportManager.GetTransport(transportId);
                    Sonic.Jms.Session session; // TODO[MT]: this out parameter only needed for handleRequest
                    MessageProducer replier = transport.CreateProducer(out session);

                    IDisposable requestSubscription = subscribe(source, transportId,
                                                                m => handleRequest(m, handler, session, transport, replier));

                    var sonicHandle = createSonicHandle(() =>
                                                            {
                                                                try
                                                                {
                                                                    replier.close();
                                                                    requestSubscription.Dispose();
                                                                    Disposable.Create(() => Logger.InfoFormat("Handler was unregistered. Transport: {0}, Queue: {1}", transportId, source));
                                                                }
                                                                catch (Exception e)
                                                                {
                                                                    Logger.WarnFormat(e, "Failed to unregister handler. Transport: {0}, Queue: {1}", transportId, source);
                                                                }
                                                            });
                    Logger.InfoFormat("Handler was successfully registered. Transport: {0}, Queue: {1}", transportId, source);
                    return sonicHandle;
                }
                catch (Exception e)
                {
                    Logger.ErrorFormat(e, "Failed to register handler. Transport: {0}, Queue: {1}", transportId, source);
                    throw;
                }
            }
        }


        public IDisposable SendRequestAsync<TRequest, TResponse>(TRequest request, string destination,
                                                                 string transportId, Action<TResponse> callback,
                                                                 Action<Exception> onFailure)
        {
            if (m_Disposing.WaitOne(0))
                throw new InvalidOperationException("Engine is disposing");

            using (m_RequestsTracker.Track())
            {
                try
                {
                    var transport = m_TransportManager.GetTransport(transportId);
                    Session session;
                    var sender = transport.CreateProducer(destination, out session);
                    
                    using (Disposable.Create(sender.close))
                    {
                        Destination tempDestination;
                        MessageConsumer receiver = transport.CreateConsumer(session, out tempDestination);
                        IDisposable handle = createSonicHandle(receiver.close);
                        receiver.setMessageListener(new GenericMessageListener(
                                                        message =>
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

                        Message requestMessage = serializeMessage(request, session, transport);
                        requestMessage.setJMSReplyTo(tempDestination);
                        sender.send(requestMessage, DeliveryMode.PERSISTENT,
                                    DefaultMessageProperties.DEFAULT_PRIORITY, MESSAGE_LIFESPAN);
                        return handle;
                    }
                }
                catch (Exception e)
                {
                    Logger.ErrorFormat(e, "Failed to register handler. Transport: {0}, Destination: {1}", transportId,
                                       destination);
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
                foreach (IDisposable sonicHandle in m_SonicHandles)
                {
                    sonicHandle.Dispose();
                }
            }
            m_TransportManager.Dispose();
        }

        #endregion
        
        private static Queue createSonicQueue(string name, Session sendSession)
        {
            if (name.StartsWith("queue://", true, CultureInfo.InvariantCulture))
                return sendSession.createQueue(name.Substring(8));
            throw new InvalidOperationException("Wrong queue name: " + name + ". Should start with 'queue://'");
        }

        private Message serializeMessage<TMessage>(TMessage message, Session session, Transport transport)
        {
            Message serializedMessage = m_SerializationManager.Serialize(message, session);
            
            return transport.JailMessage(serializedMessage);
        }

        private IDisposable subscribe(string source, string transportId, Action<Message> callback)
        {
            var transport = m_TransportManager.GetTransport(transportId);
            var receiver = transport.CreateConsumer(source);
            
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

        private void handleRequest<TRequest, TResponse>(Message message, Func<TRequest, TResponse> handler,
                                                        Session session, Transport transport, MessageProducer replier)
        {
            try
            {
                var request = m_SerializationManager.Deserialize<TRequest>(message);
                var replyQueue = message.getJMSReplyTo() as Queue;
                TResponse response = handler(request);
                Message responseMessage = serializeMessage(response, session, transport);
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

        private void processMessage<TMessage, TSonicMessage>(TSonicMessage sonicMessage, Action<TMessage> callback, string source, string transportId)
            where TSonicMessage : Message
        {
        	TMessage message = default(TMessage);
			try
			{
				message = m_SerializationManager.Deserialize<TMessage>(sonicMessage);
			}
			catch (Exception e)
			{
				Logger.ErrorFormat(e, "Failed to deserialize message. Transport: {0} Destination {1}. Message Type {2}.", transportId, source, typeof(TMessage).Name);
			}

        	try
			{
				callback(message);
			}
			catch (Exception e)
			{
				Logger.ErrorFormat(e, "Failed to handle message. Transport: {0} Destination {1}. Message Type {2}.", transportId, source, typeof(TMessage).Name);
			}
        }
    }
}