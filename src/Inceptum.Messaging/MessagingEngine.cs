using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Threading;
using Inceptum.Core.Utils;
using Inceptum.Messaging.Contract;
using Inceptum.Messaging.Serialization;
using Inceptum.Messaging.Transports;
using NLog;

namespace Inceptum.Messaging
{
    public class MessagingEngine : IMessagingEngine
    {
        private const int DEFAULT_UNACK_DELAY = 60000;
        private const int MESSAGE_DEFAULT_LIFESPAN = 0; // forever // 1800000; // milliseconds (30 minutes)
        private readonly ManualResetEvent m_Disposing = new ManualResetEvent(false);
        private readonly CountingTracker m_RequestsTracker = new CountingTracker();
        private readonly ISerializationManager m_SerializationManager;
        private readonly List<IDisposable> m_MessagingHandles = new List<IDisposable>();
        private readonly TransportManager m_TransportManager;

        readonly Logger m_Logger = LogManager.GetCurrentClassLogger();

        readonly ConcurrentDictionary<Type, string> m_MessageTypeMapping = new ConcurrentDictionary<Type, string>();
        private readonly SchedulingBackgroundWorker m_RequestTimeoutManager;
        readonly Dictionary<RequestHandle, Action<Exception>> m_ActualRequests = new Dictionary<RequestHandle, Action<Exception>>();
        

        private readonly ProcessingGroupManager m_ProcessingGroupManager;

 

        public MessagingEngine(ITransportResolver transportResolver,IDictionary<string, ProcessingGroupInfo> processingGroups=null, params ITransportFactory[] transportFactories)
        {
            if (transportResolver == null) throw new ArgumentNullException("transportResolver");
            m_TransportManager = new TransportManager(transportResolver, transportFactories);
            m_ProcessingGroupManager = new ProcessingGroupManager(m_TransportManager,processingGroups);
            m_SerializationManager = new SerializationManager();
            m_RequestTimeoutManager = new SchedulingBackgroundWorker("RequestTimeoutManager", () => stopTimeoutedRequests());
            createMessagingHandle(() => stopTimeoutedRequests(true));
        }
        public MessagingEngine(ITransportResolver transportResolver, params ITransportFactory[] transportFactories)
            : this(transportResolver,null, transportFactories)
        {
        }

        public int ResubscriptionTimeout { 
            get { return m_ProcessingGroupManager.ResubscriptionTimeout; }
            set { m_ProcessingGroupManager.ResubscriptionTimeout = value; }
        }

         public void AddProcessingGroup(string name,ProcessingGroupInfo info)
        {
            m_ProcessingGroupManager.AddProcessingGroup(name,info);
        }

        public bool GetProcessingGroupInfo(string name, out ProcessingGroupInfo groupInfo)
        {
            return m_ProcessingGroupManager.GetProcessingGroupInfo(name, out groupInfo);
        }

        public string GetStatistics()
        {
            return m_ProcessingGroupManager.GetStatistics();
        }

        internal TransportManager TransportManager
        {
            get { return m_TransportManager; }
        }

        public ISerializationManager SerializationManager
        {
            get { return m_SerializationManager; }
        }
 

        #region IMessagingEngine Members

        public  bool VerifyEndpoint(Endpoint endpoint, EndpointUsage usage, bool configureIfRequired,out string error)
        {
            return m_TransportManager.VerifyDestination(endpoint.TransportId,endpoint.Destination,usage,configureIfRequired,out error);
        }

        public Destination CreateTemporaryDestination(string transportId,string processingGroup)
        {
            return m_TransportManager.GetMessagingSession(transportId,processingGroup??"default").CreateTemporaryDestination();
        }

        public IDisposable SubscribeOnTransportEvents(TransportEventHandler handler)
        {
            TransportEventHandler safeHandler = (transportId, @event) =>
                                                    {
                                                        try
                                                        {
                                                            handler(transportId, @event);
                                                        }
                                                        catch (Exception ex)
                                                        {
                                                            m_Logger.WarnException("transport events handler failed",ex);
                                                        }
                                                    };
            m_TransportManager.TransportEvents += safeHandler;
            return Disposable.Create(() => m_TransportManager.TransportEvents -= safeHandler);
        }

        public void Send<TMessage>(TMessage message, Endpoint endpoint, string processingGroup = null)
        {
            Send(message, endpoint, MESSAGE_DEFAULT_LIFESPAN,processingGroup);
        }

        private static string getProcessingGroup(Endpoint endpoint, string processingGroup)
        {
            //by default on processing group per destination
            return  processingGroup ?? endpoint.Destination.ToString();
        }

        public void Send<TMessage>(TMessage message, Endpoint endpoint, int ttl, string processingGroup = null)
        {
            var serializedMessage = serializeMessage(endpoint.SerializationFormat, message);
            send(serializedMessage,endpoint,ttl, processingGroup);
        }



        public void Send(object message, Endpoint endpoint, string processingGroup = null)
        {
            var type = getMessageType(message.GetType());
            var bytes = m_SerializationManager.SerializeObject(endpoint.SerializationFormat, message);
            var serializedMessage = new BinaryMessage { Bytes = bytes, Type = type };
            send(serializedMessage, endpoint, MESSAGE_DEFAULT_LIFESPAN, processingGroup);
        }
        
        private void send(BinaryMessage message, Endpoint endpoint, int ttl,string processingGroup)
        {
            if (endpoint.Destination == null) throw new ArgumentException("Destination can not be null");
            if (m_Disposing.WaitOne(0))
                throw new InvalidOperationException("Engine is disposing");
            

            using (m_RequestsTracker.Track())
            {
                try
                {
                    m_ProcessingGroupManager.Send(endpoint, message, ttl, getProcessingGroup(endpoint, processingGroup));
                }
                catch (Exception e)
                {
                    m_Logger.ErrorException(string.Format("Failed to send message. Transport: {0}, Queue: {1}", endpoint.TransportId, endpoint.Destination),e);
                    throw;
                }
            }
        }


		public IDisposable Subscribe<TMessage>(Endpoint endpoint, Action<TMessage> callback)
		{
            return Subscribe(endpoint, (TMessage message, AcknowledgeDelegate acknowledge,Dictionary<string,string> headers) =>
		        {
		            callback(message);
		            acknowledge(0,true);
		        });
		}
        public IDisposable Subscribe<TMessage>(Endpoint endpoint, CallbackDelegate<TMessage> callback, string processingGroup = null, int priority = 0)
        {
			if (endpoint.Destination == null) throw new ArgumentException("Destination can not be null");
            if (m_Disposing.WaitOne(0))
                throw new InvalidOperationException("Engine is disposing");

            using (m_RequestsTracker.Track())
            {
                try
                {
                    return subscribe(endpoint, (m, ack) => processMessage(m, typeof(TMessage), (message, headers) => callback((TMessage)message, ack, headers), ack, endpoint), endpoint.SharedDestination ? getMessageType(typeof(TMessage)) : null, processingGroup, priority);
                }
                catch (Exception e)
                {
                    m_Logger.ErrorException(string.Format( "Failed to subscribe. Transport: {0}, Queue: {1}",  endpoint.TransportId, endpoint.Destination),e);
                    throw;
                }
            }
        }

        public IDisposable Subscribe(Endpoint endpoint, Action<object> callback, Action<string> unknownTypeCallback,params Type[] knownTypes)
        {
            return Subscribe(endpoint, callback, unknownTypeCallback, null,0, knownTypes);
        }

        public IDisposable Subscribe(Endpoint endpoint, Action<object> callback, Action<string> unknownTypeCallback, string processingGroup, int priority, params Type[] knownTypes)
        {
            return Subscribe(endpoint,
                             (message, acknowledge,headers) =>
                                 {
                                     callback(message);
                                     acknowledge(0, true);
                                 },
                             (type, acknowledge) =>
                                 {
                                     unknownTypeCallback(type);
                                     acknowledge(0, true);
                                 },
                             processingGroup,priority,
                             knownTypes);
        }

        public IDisposable Subscribe(Endpoint endpoint, CallbackDelegate<object> callback, Action<string, AcknowledgeDelegate> unknownTypeCallback,
            params Type[] knownTypes)
        {
            return Subscribe(endpoint, callback, unknownTypeCallback, null,0, knownTypes);
        }
        public IDisposable Subscribe(Endpoint endpoint, CallbackDelegate<object> callback, Action<string, AcknowledgeDelegate> unknownTypeCallback, string processingGroup, int priority = 0, params Type[] knownTypes)
        {
            if (endpoint.Destination == null) throw new ArgumentException("Destination can not be null");
            if (m_Disposing.WaitOne(0))
                throw new InvalidOperationException("Engine is disposing");

            using (m_RequestsTracker.Track())
            {
                try
                {
                    var dictionary = knownTypes.ToDictionary(getMessageType);

                    return subscribe(endpoint, (m,ack) =>
                        {
                            Type messageType;
                            if (!dictionary.TryGetValue(m.Type, out messageType))
                            {
                                try
                                {
                                    unknownTypeCallback(m.Type, ack);
                                }
                                catch (Exception e)
                                {
                                    m_Logger.ErrorException(string.Format("Failed to handle message of unknown type. Transport: {0}, Queue {1}, Message Type: {2}",
                                   endpoint.TransportId, endpoint.Destination, m.Type),e);
                                }
                                return;
                            }
                            processMessage(m, messageType, (message,headers) => callback(message, ack,headers), ack, endpoint);
                        }, null, processingGroup, priority);
                }
                catch (Exception e)
                {
                     m_Logger.ErrorException(string.Format("Failed to subscribe. Transport: {0}, Queue: {1}", endpoint.TransportId, endpoint.Destination),e);
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
                var responseReceived = new ManualResetEvent(false);
                TResponse response = default(TResponse);
                Exception exception = null;

				using (SendRequestAsync<TRequest, TResponse>(request, endpoint,
                                                             r =>
                                                                 {
                                                                     response = r;
                                                                     responseReceived.Set();
                                                                 },
                                                             ex =>
                                                                 {
                                                                     exception = ex;
                                                                     responseReceived.Set();
                                                                 },timeout))
                {
                    int waitResult = WaitHandle.WaitAny(new WaitHandle[] {m_Disposing, responseReceived});
                    switch (waitResult)
                    {
                        case 1:
                            if (exception == null)
                                return response;
                            if(exception is TimeoutException)
                                throw exception;//StackTrace is replaced bat it is ok here.
                            throw new ProcessingException("Failed to process response", exception);
                        case 0:
                            throw new ProcessingException("Request was canceled due to engine dispose", exception);
 
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
                            ?m_ActualRequests.ToArray()
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





        public IDisposable SendRequestAsync<TRequest, TResponse>(TRequest request, Endpoint endpoint, Action<TResponse> callback, Action<Exception> onFailure, long timeout, string processingGroup = null)
        {
            if (m_Disposing.WaitOne(0))
                throw new InvalidOperationException("Engine is disposing");

            using (m_RequestsTracker.Track())
            {
                try
                {
                    var session = m_TransportManager.GetMessagingSession(endpoint.TransportId, getProcessingGroup(endpoint, processingGroup));
                    RequestHandle requestHandle = session.SendRequest(endpoint.Destination.Publish, serializeMessage(endpoint.SerializationFormat, request),
                                                                     message =>
                                                                     {
                                                                         try
                                                                         {
                                                                             var responseMessage = m_SerializationManager.Deserialize<TResponse>(endpoint.SerializationFormat, message.Bytes);  
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
                     m_Logger.ErrorException(string.Format( "Failed to register handler. Transport: {0}, Destination: {1}",  endpoint.TransportId,
                                       endpoint.Destination),e);
                    throw;
                }
            }
        }

        public IDisposable RegisterHandler<TRequest, TResponse>(Func<TRequest, TResponse> handler, Endpoint endpoint)
			where TResponse : class
		{
			var handle = new SerialDisposable();
            IDisposable transportWatcher = SubscribeOnTransportEvents((transportId, @event) =>
			                                                          	{
			                                                          		if (transportId == endpoint.TransportId || @event != TransportEvents.Failure)
			                                                          			return;
			                                                          		registerHandlerWithRetry(handler, endpoint, handle);
			                                                          	});

			registerHandlerWithRetry(handler, endpoint, handle);

			return new CompositeDisposable(transportWatcher, handle);
		}


        public void Dispose()
        {
            m_Logger.Debug("Disposing");
            m_Disposing.Set();
            m_RequestsTracker.WaitAll();
            lock (m_MessagingHandles)
            {

                while (m_MessagingHandles.Any())
                {
                    m_MessagingHandles.First().Dispose();
                }
            }
            m_RequestTimeoutManager.Dispose();
            m_ProcessingGroupManager.Dispose();
            m_TransportManager.Dispose();
        }

        #endregion

        private void registerHandlerWithRetry<TRequest, TResponse>(Func<TRequest, TResponse> handler, Endpoint endpoint, SerialDisposable handle)
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
                    m_Logger.Info("Scheduling register handler attempt in 1 minute. Transport: {0}, Queue: {1}",
                                       endpoint.TransportId, endpoint.Destination);
                	handle.Disposable = Scheduler.Default.Schedule(DateTimeOffset.Now.AddMinutes(1),
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


        private IDisposable registerHandler<TRequest, TResponse>(Func<TRequest, TResponse> handler, Endpoint endpoint, string processingGroup = null)
            where TResponse : class
        {
            //BUG: registering handler while disposing causes endless loop
            if (m_Disposing.WaitOne(0))
                throw new InvalidOperationException("Engine is disposing");

            using (m_RequestsTracker.Track())
            {
                try
                {
                    var session = m_TransportManager.GetMessagingSession(endpoint.TransportId, getProcessingGroup(endpoint, processingGroup));
                    var subscription = session.RegisterHandler(endpoint.Destination.Subscribe,
                	                                                     requestMessage =>
                	                                                     	{
                                                                                var message = m_SerializationManager.Deserialize<TRequest>(endpoint.SerializationFormat, requestMessage.Bytes); 
                	                                                     		TResponse response = handler(message);
                	                                                     		return serializeMessage(endpoint.SerializationFormat,response);
                	                                                     	},
                	                                                     endpoint.SharedDestination
                	                                                     	? getMessageType(typeof (TRequest))
                	                                                     	: null
                		);
                	var messagingHandle = createMessagingHandle(() =>
                	                                            	{
                	                                            		try
                	                                            		{
                	                                            			subscription.Dispose();
                	                                            			Disposable.Create(() => m_Logger.Info("Handler was unregistered. Transport: {0}, Queue: {1}", endpoint.TransportId, endpoint.Destination));
                	                                            		}
                	                                            		catch (Exception e)
                	                                            		{
                	                                            			 m_Logger.WarnException(string.Format("Failed to unregister handler. Transport: {0}, Queue: {1}", endpoint.TransportId, endpoint.Destination),e);
                	                                            		}
                	                                            	});

                    m_Logger.Info("Handler was successfully registered. Transport: {0}, Queue: {1}",  endpoint.TransportId, endpoint.Destination);
                    return messagingHandle;
                }
                catch (Exception e)
                {
                     m_Logger.ErrorException(string.Format("Failed to register handler. Transport: {0}, Queue: {1}",  endpoint.TransportId, endpoint.Destination),e);
                    throw;
                }
            }
        }


        private BinaryMessage serializeMessage<TMessage>(string format,TMessage message)
        {
            var type = getMessageType(typeof(TMessage));
            var bytes = m_SerializationManager.Serialize(format,message);
            return new BinaryMessage{Bytes=bytes,Type=type};
        }
    

        private string getMessageType(Type type)
        {
        	return m_MessageTypeMapping.GetOrAdd(type, clrType =>
        	                                           	{
                                                            //TODO: type should be determined by serializer
        	                                           		var typeName = clrType.GetCustomAttributes(false)
        	                                           			.Select(a => a as ProtoBuf.ProtoContractAttribute)
        	                                           			.Where(a => a != null)
        	                                           			.Select(a => a.Name)
        	                                           			.FirstOrDefault();
        	                                           		return typeName ?? clrType.Name;
        	                                           	});
        }


        private IDisposable subscribe(Endpoint endpoint, Action<BinaryMessage, AcknowledgeDelegate> callback, string messageType, string processingGroup, int priority)
        {
            var subscription = m_ProcessingGroupManager.Subscribe(endpoint, callback, messageType, getProcessingGroup(endpoint,processingGroup),priority);

            return createMessagingHandle(() =>
            {
                subscription.Dispose();
                m_Logger.Info("Unsubscribed from endpoint {0}", endpoint);
            });
        }


        private IDisposable createMessagingHandle(Action destroy)
        {
            IDisposable handle = null;

            handle = Disposable.Create(() =>
                                           {
                                               destroy();
                                               lock (m_MessagingHandles)
                                               {
// ReSharper disable AccessToModifiedClosure
                                                   m_MessagingHandles.Remove(handle);
// ReSharper restore AccessToModifiedClosure
                                               }
                                           });
            lock (m_MessagingHandles)
            {
                m_MessagingHandles.Add(handle);
            }
            return handle;
        }



        private void processMessage(BinaryMessage binaryMessage,Type type, Action<object,Dictionary<string,string>> callback, AcknowledgeDelegate ack, Endpoint endpoint)
        {
            object message = null;
            try
            {
                message = m_SerializationManager.Deserialize(endpoint.SerializationFormat, binaryMessage.Bytes, type);
            }
            catch (Exception e)
            {
                m_Logger.ErrorException(string.Format("Failed to deserialize message. Transport: {0}, Destination: {1}, Message Type: {2}",
                                   endpoint.TransportId, endpoint.Destination, type.Name),e);
                //TODO: need to unack without requeue
                ack(DEFAULT_UNACK_DELAY, false);
            }

            try
            {
                callback(message, binaryMessage.Headers);
            }
            catch (Exception e)
            {
                 m_Logger.ErrorException(string.Format("Failed to handle message. Transport: {0}, Destination: {1}, Message Type: {2}",
                                   endpoint.TransportId, endpoint.Destination, type.Name),e);
                ack(DEFAULT_UNACK_DELAY, false);
            }
        }
    }
}