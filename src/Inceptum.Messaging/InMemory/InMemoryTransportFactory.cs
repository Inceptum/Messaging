using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using Inceptum.Messaging.Contract;
using Inceptum.Messaging.Transports;

namespace Inceptum.Messaging.InMemory
{
    internal class InMemoryTransportFactory : ITransportFactory
    {
        readonly Dictionary<TransportInfo,InMemoryTransport> m_Transports=new Dictionary<TransportInfo, InMemoryTransport>(); 
        public string Name { get { return "InMemory"; } }
        public ITransport Create(TransportInfo transportInfo, Action onFailure)
        {
            lock (m_Transports)
            {
                InMemoryTransport transport;
                if (!m_Transports.TryGetValue(transportInfo, out transport))
                {
                    transport=new InMemoryTransport();
                    m_Transports.Add(transportInfo,transport);
                }
                return transport;
            }
        }
    }

    internal class InMemoryTransport : ITransport
    {
        readonly Dictionary<string,Subject<BinaryMessage>> m_Topics=new Dictionary<string, Subject<BinaryMessage>>();
        readonly List<InMemoryProcessingGroup> m_ProcessingGroups = new List<InMemoryProcessingGroup>();


        public Subject<BinaryMessage> this[string name]
        {
            get
            {
                lock (m_Topics)
                {
                    Subject<BinaryMessage> topic;
                    if (!m_Topics.TryGetValue(name, out topic))
                    {
                        topic=new Subject<BinaryMessage>();
                        m_Topics[name] = topic;
                    }
                    return topic;
                }
            }
        }

     

        public IDisposable CreateTemporary(string name)
        {
            lock (m_Topics)
            {
                Subject<BinaryMessage> topic;
                if (m_Topics.TryGetValue(name, out topic))
                {
                    throw new ArgumentException("topic already exists", "name");
                }
                topic = new Subject<BinaryMessage>();
                m_Topics[name] = topic;
                return Disposable.Create(() =>
                    {
                        lock (m_Topics)
                        {
                            m_Topics.Remove(name);
                        }
                    });
            }
        }
        public void Dispose()
        {
            lock (m_ProcessingGroups)
            {
                foreach (var processingGroup in m_ProcessingGroups)
                {
                    processingGroup.Dispose();
                }
                m_ProcessingGroups.Clear();
            }
            
        }

        public IProcessingGroup CreateProcessingGroup(Action onFailure)
        {
            var processingGroup = new InMemoryProcessingGroup(this);
            lock (m_ProcessingGroups)
            {
                m_ProcessingGroups.Add(processingGroup);
                return processingGroup;
            }
        }

        public bool VerifyDestination(Destination destination, EndpointUsage usage, bool configureIfRequired, out string error)
        {
            error = null;
            return true;
        }
    }

     
 
    internal class InMemoryProcessingGroup : IProcessingGroup
    {
        private readonly InMemoryTransport m_Transport;
        readonly EventLoopScheduler m_Scheduler=new EventLoopScheduler(ts => new Thread(ts){Name = "inmemory transport"});
        readonly CompositeDisposable m_Subscriptions=new CompositeDisposable();
        private bool m_IsDisposed=false;

        public InMemoryProcessingGroup(InMemoryTransport queues)
        {
            m_Transport = queues;
        }

        public Destination CreateTemporaryDestination()
        {
            var name = Guid.NewGuid().ToString();
            m_Transport.CreateTemporary(name);
            return name;
        }
      

        public void Send(string destination, BinaryMessage message, int ttl)
        {
            m_Transport[destination].OnNext(message);
        }

        public IDisposable Subscribe(string destination, Action<BinaryMessage, Action<bool>> callback, string messageType)
        {
            var subscribe = m_Transport[destination].Where(m => m.Type == messageType || messageType == null).ObserveOn(m_Scheduler)
                //NOTE:InMemory messaging desnot support acknowledge 
                .Subscribe(message => callback(message, b => { }));
            m_Subscriptions.Add(subscribe);
            return subscribe;
        }

        public RequestHandle SendRequest(string destination, BinaryMessage message, Action<BinaryMessage> callback)
        {
            var replyTo = Guid.NewGuid().ToString();
            var responseTopic = m_Transport.CreateTemporary(replyTo);

            var request = new RequestHandle(callback, responseTopic.Dispose, cb => Subscribe(replyTo, (binaryMessage, acknowledge) => cb(binaryMessage), null));
            message.Headers["ReplyTo"] = replyTo;
            Send(destination,message,0);
            return request;
        }

        public IDisposable RegisterHandler(string destination, Func<BinaryMessage, BinaryMessage> handler, string messageType)
        {
            var subscription = Subscribe(destination, (request,acknowledge) =>
                {
                    string replyTo;
                    request.Headers.TryGetValue("ReplyTo",out replyTo);
                    if(replyTo==null)
                        return;

                    var response = handler(request);
                    string correlationId;
                    if(request.Headers.TryGetValue("ReplyTo", out correlationId))
                        response.Headers["CorrelationId"] = correlationId;
                    Send(replyTo, response,0);
            }, messageType);
            return subscription;
        }



        public void Dispose()
        {
            if (m_IsDisposed)
                return;
            var finishedProcessing=new ManualResetEvent(false);
            m_Subscriptions.Dispose();
            m_Scheduler.Schedule(() => finishedProcessing.Set());
            finishedProcessing.WaitOne();
            m_Scheduler.Dispose();
            m_IsDisposed = true;
        }
      
    }
}