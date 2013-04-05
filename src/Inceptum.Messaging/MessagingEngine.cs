using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Threading;
using Castle.Core.Logging;
using Inceptum.Core.Messaging;
using Inceptum.Core.Utils;
using Inceptum.Messaging.Transports;

namespace Inceptum.Messaging
{
    public class MessagingEngine : IMessagingEngine
    {
        internal const int MESSAGE_DEFAULT_LIFESPAN = 0; // forever // 1800000; // milliseconds (30 minutes)
        private readonly ManualResetEvent m_Disposing = new ManualResetEvent(false);
        private readonly CountingTracker m_RequestsTracker = new CountingTracker();
        private readonly ISerializationManager m_SerializationManager;
        private readonly List<IDisposable> m_SonicHandles = new List<IDisposable>();
        private readonly TransportManager m_TransportManager;

        //TODO: verify logging. I've added param but never tested
        private ILogger m_Logger = NullLogger.Instance;
        readonly ConcurrentDictionary<Type, string> m_MessageTypeMapping = new ConcurrentDictionary<Type, string>();
        private readonly SchedulingBackgroundWorker m_RequestTimeoutManager;
        readonly Dictionary<RequestHandle, Action<Exception>> m_ActualRequests = new Dictionary<RequestHandle, Action<Exception>>();

        /// <summary>
        /// ctor for tests
        /// </summary>
        /// <param name="transportManager"></param>
        /// <param name="serializationManager"></param>
        internal MessagingEngine(TransportManager transportManager, ISerializationManager serializationManager)
        {
            m_TransportManager = transportManager;
            m_SerializationManager = serializationManager;
            m_RequestTimeoutManager = new SchedulingBackgroundWorker("RequestTimeoutManager", () => stopTimeoutedRequests());
            createSonicHandle(() => stopTimeoutedRequests(true));
        }

        public MessagingEngine(ITransportResolver transportResolver, ISerializationManager serializationManager,params ITransportFactory[] transportFactories)
            : this(new TransportManager(transportResolver, transportFactories), serializationManager)
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

        public void Send<TMessage>(TMessage message, Endpoint endpoint)
        {
            Send(message, endpoint, MESSAGE_DEFAULT_LIFESPAN);
        }

        public void Send<TMessage>(TMessage message, Endpoint endpoint, int ttl)
        {
            if (endpoint.Destination == null) throw new ArgumentException("Destination can not be null");
            if (m_Disposing.WaitOne(0))
                throw new InvalidOperationException("Engine is disposing");

            using (m_RequestsTracker.Track())
            {
                try
                {
                    var transport = m_TransportManager.GetTransport(endpoint.TransportId);
                    var serializedMessage = serializeMessage(message);
                    transport.Send(endpoint.Destination, serializedMessage, ttl);
                }
                catch (Exception e)
                {
					Logger.ErrorFormat(e, "Failed to send message. Transport: {0}, Queue: {1}", endpoint.TransportId, endpoint.Destination);
                    throw;
                }
            }
        }


		public IDisposable Subscribe<TMessage>(Endpoint endpoint, Action<TMessage> callback)
        {
			if (endpoint.Destination == null) throw new ArgumentException("Destination can not be null");
            if (m_Disposing.WaitOne(0))
                throw new InvalidOperationException("Engine is disposing");

            using (m_RequestsTracker.Track())
            {
                try
                {
					return subscribe(endpoint.Destination, endpoint.TransportId, m => processMessage(m, callback, endpoint), endpoint.SharedDestination ? getMessageType(typeof(TMessage)) : null);
                }
                catch (Exception e)
                {
                    Logger.ErrorFormat(e, "Failed to subscribe. Transport: {0}, Queue: {1}",  endpoint.TransportId, endpoint.Destination);
                    throw;
                }
            }
        }


        //NOTE: send via topic waits only first response.
        public TResponse SendRequest<TRequest, TResponse>(TRequest request, Endpoint endpoint, long timeout)
        {
            if (m_Disposing.WaitOne(0))
                throw new InvalidOperationException("Engine is disposing");

            using (m_RequestsTracker.Track())
            {
                var responseRecieved = new ManualResetEvent(false);
                TResponse response = default(TResponse);
                Exception exception = null;

				using (SendRequestAsync<TRequest, TResponse>(request, endpoint,
                                                             r =>
                                                                 {
                                                                     response = r;
                                                                     responseRecieved.Set();
                                                                 },
                                                             ex =>
                                                                 {
                                                                     exception = ex;
                                                                     responseRecieved.Set();
                                                                 },timeout))
                {
                    int waitResult = WaitHandle.WaitAny(new WaitHandle[] {m_Disposing, responseRecieved});
                    switch (waitResult)
                    {
                        case 1:
                            if (exception == null)
                                return response;
                            if(exception is TimeoutException)
                                throw exception;//StackTrace is replaced bat it is ok here.
                            throw new ProcessingException("Failed to process response", exception);
                        case 0:
                            throw new ProcessingException("Request was cancelled due to engine dispose", exception);
 
                        default:
                            throw new InvalidOperationException();
                    }
                }
            }
        }

 

        private void stopTimeoutedRequests(bool stopAll=false)
        {
            lock (m_ActualRequests)
            {
                var timeouted = stopAll
                            ?m_ActualRequests .ToArray()
                            :m_ActualRequests.Where(r => r.Key.DueDate <= DateTime.Now || r.Key.IsComplete).ToArray();

                Array.ForEach(timeouted, r =>
                {
                    r.Key.Dispose();
                    if (!r.Key.IsComplete)
                    {
                        r.Value(new TimeoutException("Request has timed out")); 
                    }
                    m_ActualRequests.Remove(r.Key);
                });
            }
        }

 


        public IDisposable SendRequestAsync<TRequest, TResponse>(TRequest request, Endpoint endpoint, Action<TResponse> callback, Action<Exception> onFailure, long timeout)
        {
            if (m_Disposing.WaitOne(0))
                throw new InvalidOperationException("Engine is disposing");

            using (m_RequestsTracker.Track())
            {
                try
                {
                    var transport = m_TransportManager.GetTransport( endpoint.TransportId);
                    RequestHandle requestHandle = transport.SendRequest(endpoint.Destination, serializeMessage(request),
                                                                     message =>
                                                                     {
                                                                         try
                                                                         {
                                                                             var responseMessage = deserializeMessage<TResponse>(message);
                                                                             callback(responseMessage);
                                                                         }
                                                                         catch (Exception e)
                                                                         {
                                                                             onFailure(e);
                                                                         }
                                                                         finally
                                                                         {
                                                                             m_RequestTimeoutManager.Schedule(1);
                                                                         }
                                                                     });


                    lock (m_ActualRequests)
                    {
                        requestHandle.DueDate = DateTime.Now.AddMilliseconds(timeout);
                        m_ActualRequests.Add(requestHandle, onFailure);
                        m_RequestTimeoutManager.Schedule(timeout);
                    }
                    return requestHandle;

                }
                catch (Exception e)
                {
                    Logger.ErrorFormat(e, "Failed to register handler. Transport: {0}, Destination: {1}",  endpoint.TransportId,
                                       endpoint.Destination);
                    throw;
                }
            }
        }

        public IDisposable RegisterHandler<TRequest, TResponse>(Func<TRequest, TResponse> handler, Endpoint endpoint)
			where TResponse : class
		{
			var handle = new SerialDisposable();
			IDisposable transportWatcher = SubscribeOnTransportEvents((id, @event) =>
			                                                          	{
			                                                          		if (@event != TransportEvents.Failure)
			                                                          			return;
			                                                          		registerHandlerWithRetry(handler, endpoint, handle);
			                                                          	});

			registerHandlerWithRetry(handler, endpoint, handle);

			return new CompositeDisposable(transportWatcher, handle);
		}


        public void Dispose()
        {
            m_Disposing.Set();
            m_RequestTimeoutManager.Dispose();
            m_RequestsTracker.WaitAll();
            lock (m_SonicHandles)
            {

                while (m_SonicHandles.Any())
                {
                    m_SonicHandles.First().Dispose();
                }
            }
            m_TransportManager.Dispose();
        }

        #endregion

        public void registerHandlerWithRetry<TRequest, TResponse>(Func<TRequest, TResponse> handler, Endpoint endpoint, SerialDisposable handle)
            where TResponse : class
        {
            lock (handle)
            {
                try
                {
                    handle.Disposable = registerHandler(handler, endpoint);
                }
                catch
                {
                    Logger.InfoFormat("Scheduling register handler attempt in 1 minute. Transport: {0}, Queue: {1}",
                                       endpoint.TransportId, endpoint.Destination);
                	handle.Disposable = Scheduler.ThreadPool.Schedule(DateTimeOffset.Now.AddMinutes(1),
                	                                                  () =>
                	                                                  	{
                	                                                  		lock (handle)
                	                                                  		{
                	                                                  			registerHandlerWithRetry(handler, endpoint, handle);
                	                                                  		}
                	                                                  	});
                }
            }
        }


		public IDisposable registerHandler<TRequest, TResponse>(Func<TRequest, TResponse> handler, Endpoint endpoint)
            where TResponse : class
        {
            if (m_Disposing.WaitOne(0))
                throw new InvalidOperationException("Engine is disposing");

            using (m_RequestsTracker.Track())
            {
                try
                {
                    var transport = m_TransportManager.GetTransport( endpoint.TransportId);
                	var subscription = transport.RegisterHandler(endpoint.Destination,
                	                                                     requestMessage =>
                	                                                     	{
                	                                                     		var message = deserializeMessage<TRequest>(requestMessage);
                	                                                     		TResponse response = handler(message);
                	                                                     		return serializeMessage(response);
                	                                                     	},
                	                                                     endpoint.SharedDestination
                	                                                     	? getMessageType(typeof (TRequest))
                	                                                     	: null
                		);
                	var sonicHandle = createSonicHandle(() =>
                	                                            	{
                	                                            		try
                	                                            		{
                	                                            			subscription.Dispose();
                	                                            			Disposable.Create(() => Logger.InfoFormat("Handler was unregistered. Transport: {0}, Queue: {1}", endpoint.TransportId, endpoint.Destination));
                	                                            		}
                	                                            		catch (Exception e)
                	                                            		{
                	                                            			Logger.WarnFormat(e, "Failed to unregister handler. Transport: {0}, Queue: {1}", endpoint.TransportId, endpoint.Destination);
                	                                            		}
                	                                            	});

                    Logger.InfoFormat("Handler was successfully registered. Transport: {0}, Queue: {1}",  endpoint.TransportId, endpoint.Destination);
                    return sonicHandle;
                }
                catch (Exception e)
                {
                    Logger.ErrorFormat(e, "Failed to register handler. Transport: {0}, Queue: {1}",  endpoint.TransportId, endpoint.Destination);
                    throw;
                }
            }
        }


        private BinaryMessage serializeMessage<TMessage>(TMessage message)
        {
            var type = getMessageType(typeof(TMessage));
            var bytes = m_SerializationManager.Serialize(message);
            return new BinaryMessage{Bytes=bytes,Type=type};
        }

        private string getMessageType(Type type)
        {
        	return m_MessageTypeMapping.GetOrAdd(type, clrType =>
        	                                           	{
        	                                           		var typeName = clrType.GetCustomAttributes(false)
        	                                           			.Select(a => a as ProtoBuf.ProtoContractAttribute)
        	                                           			.Where(a => a != null)
        	                                           			.Select(a => a.Name)
        	                                           			.FirstOrDefault();
        	                                           		return typeName ?? clrType.Name;
        	                                           	});
        }

        private TMessage deserializeMessage<TMessage>(BinaryMessage message)
        {
            return m_SerializationManager.Deserialize<TMessage>(message.Bytes);
        }

        private IDisposable subscribe(string destination, string transportId, Action<BinaryMessage> callback, string messageType)
        {
            var transport = m_TransportManager.GetTransport(transportId);
            IDisposable subscription = transport.Subscribe(destination, callback, messageType);
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


        private void processMessage<TMessage>(BinaryMessage sonicMessage, Action<TMessage> callback, Endpoint endpoint)
        {
            TMessage message = default(TMessage);
            try
            {
                message = deserializeMessage<TMessage>(sonicMessage);
            }
            catch (Exception e)
            {
                Logger.ErrorFormat(e, "Failed to deserialize message. Transport: {0} Destination {1}. Message Type {2}.",
								    endpoint.TransportId, endpoint.Destination, typeof(TMessage).Name);
            }

            try
            {
                callback(message);
            }
            catch (Exception e)
            {
                Logger.ErrorFormat(e, "Failed to handle message. Transport: {0} Destination {1}. Message Type {2}.",
									endpoint.TransportId, endpoint.Destination, typeof(TMessage).Name);
            }
        }
    }
}