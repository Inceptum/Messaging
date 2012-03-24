using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Threading;
using Castle.Core.Logging;
using Inceptum.Core.Messaging;
using Inceptum.Core.Utils;
using Inceptum.Messaging.Transports;
using Sonic.Jms;

namespace Inceptum.Messaging
{
    public class MessagingEngine : IMessagingEngine
    {
        internal const int MESSAGE_LIFESPAN = 0; // forever // 1800000; // milliseconds (30 minutes)
        internal const string JAILED_PROPERTY_NAME = "JAILED_TAG";
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


        public ILogger Logger
        {
            get { return m_Logger; }
            set { m_Logger = value; }
        }

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
                    Session session;
                    Transport transport = m_TransportManager.GetTransport(transportId);
                    byte[] serializedMessage = m_SerializationManager.Serialize(message);
                    transport.Send(destination, serializedMessage);
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
                                                                              registerHandlerWithRetry(handler, source,
                                                                                                       transportId,
                                                                                                       handle);
                                                                          });

            registerHandlerWithRetry(handler, source, transportId, handle);

            return new CompositeDisposable(transportWatcher, handle);
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
                    Transport transport = m_TransportManager.GetTransport(transportId);
                    IDisposable subscription = transport.SendRequest(destination,
                                                                     m_SerializationManager.Serialize(request),
                                                                     message =>
                                                                         {
                                                                             try
                                                                             {
                                                                                 var responseMessage =
                                                                                     deserializeMessage<TResponse>(
                                                                                         message);
                                                                                 callback(responseMessage);
                                                                             }
                                                                             catch (Exception e)
                                                                             {
                                                                                 onFailure(e);
                                                                             }
                                                                         });
                    return createSonicHandle(subscription.Dispose);
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

        public void registerHandlerWithRetry<TRequest, TResponse>(Func<TRequest, TResponse> handler,
                                                                  string source, string transportId,
                                                                  SerialDisposable handle)
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
                    Logger.InfoFormat("Scheduling register handler attempt in 1 minute. Transport: {0}, Queue: {1}",
                                      transportId, source);
                    handle.Disposable = Scheduler.ThreadPool.Schedule(DateTimeOffset.Now.AddMinutes(1),
                                                                      () =>
                                                                          {
                                                                              lock (handle)
                                                                              {
                                                                                  registerHandlerWithRetry(handler,
                                                                                                           source,
                                                                                                           transportId,
                                                                                                           handle);
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
                    Transport transport = m_TransportManager.GetTransport(transportId);
                    IDisposable subscription = transport.RegisterHandler(source, requestMessage =>
                                                                                     {
                                                                                         var message =
                                                                                             deserializeMessage
                                                                                                 <TRequest>(
                                                                                                     requestMessage);
                                                                                         TResponse response =
                                                                                             handler(message);
                                                                                         return
                                                                                             m_SerializationManager.
                                                                                                 Serialize(response);
                                                                                     });
                    IDisposable sonicHandle = createSonicHandle(() =>
                                                                    {
                                                                        try
                                                                        {
                                                                            subscription.Dispose();
                                                                            Disposable.Create(
                                                                                () =>
                                                                                Logger.InfoFormat(
                                                                                    "Handler was unregistered. Transport: {0}, Queue: {1}",
                                                                                    transportId, source));
                                                                        }
                                                                        catch (Exception e)
                                                                        {
                                                                            Logger.WarnFormat(e,
                                                                                              "Failed to unregister handler. Transport: {0}, Queue: {1}",
                                                                                              transportId, source);
                                                                        }
                                                                    });
                    Logger.InfoFormat("Handler was successfully registered. Transport: {0}, Queue: {1}", transportId,
                                      source);
                    return sonicHandle;
                }
                catch (Exception e)
                {
                    Logger.ErrorFormat(e, "Failed to register handler. Transport: {0}, Queue: {1}", transportId, source);
                    throw;
                }
            }
        }


        private TMessage deserializeMessage<TMessage>(Message message)
        {
            Debug.Assert(message != null);
            if (message == null) throw new ArgumentNullException("message");
            var bytesMessage = message as BytesMessage;
            if (bytesMessage == null)
                throw new ArgumentException("message is expected to contain BytesMessage", "message");
            var buf = new byte[bytesMessage.getBodyLength()];
            bytesMessage.readBytes(buf);

            return m_SerializationManager.Deserialize<TMessage>(buf);
        }

        private IDisposable subscribe(string source, string transportId, Action<Message> callback)
        {
            Transport transport = m_TransportManager.GetTransport(transportId);
            IDisposable subscription = transport.Subscribe(source, callback);
            return createSonicHandle(subscription.Dispose);
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


        private void processMessage<TMessage, TSonicMessage>(TSonicMessage sonicMessage, Action<TMessage> callback,
                                                             string source, string transportId)
            where TSonicMessage : Message
        {
            TMessage message = default(TMessage);
            try
            {
                message = deserializeMessage<TMessage>(sonicMessage);
            }
            catch (Exception e)
            {
                Logger.ErrorFormat(e, "Failed to deserialize message. Transport: {0} Destination {1}. Message Type {2}.",
                                   transportId, source, typeof (TMessage).Name);
            }

            try
            {
                callback(message);
            }
            catch (Exception e)
            {
                Logger.ErrorFormat(e, "Failed to handle message. Transport: {0} Destination {1}. Message Type {2}.",
                                   transportId, source, typeof (TMessage).Name);
            }
        }
    }
}