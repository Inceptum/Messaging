using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Inceptum.Messaging.Contract;
using Inceptum.Messaging.Transports;
using Inceptum.Messaging.Utils;

namespace Inceptum.Messaging
{
    internal class ProcessingGroupWrapper : IDisposable, IPrioritizedProcessingGroup
    {
        private readonly QueuedTaskScheduler m_TaskScheduler;
        private readonly Dictionary<int,TaskFactory> m_TaskFactories=new Dictionary<int, TaskFactory>();
        public string TransportId { get; private set; }
        public string Name { get; private set; }
        private IProcessingGroup ProcessingGroup { get;  set; }
        public event Action OnFailure;

        public ProcessingGroupWrapper(string transportId, string name, ProcessingGroupInfo processingGroupInfo)
        {
            TransportId = transportId;
            Name = name;
            var threadCount = Math.Max(processingGroupInfo.ConcurrencyLevel, 1);
            m_TaskScheduler = new QueuedTaskScheduler(threadCount);
            m_TaskFactories = new Dictionary<int, TaskFactory>();
        }

        private TaskFactory getTaskFactory(int priority)
        {
            if(priority<0)
                throw new ArgumentException("priority should be >0","priority");
            lock (m_TaskFactories)
            {
                TaskFactory factory;
                if (!m_TaskFactories.TryGetValue(priority, out factory))
                {
                    var scheduler = m_TaskScheduler.ActivateNewQueue(priority);
                    factory=new TaskFactory(scheduler);
                    m_TaskFactories.Add(priority,factory);
                }
                return factory;
            }
        }

        public void SetProcessingGroup(IProcessingGroup processingGroup)
        {
            ProcessingGroup = processingGroup;
        }

        public void ReportFailure()
        {
            if (OnFailure == null)
                return;

            foreach (var handler in OnFailure.GetInvocationList())
            {
                try
                {
                    handler.DynamicInvoke();
                }
                catch (Exception)
                {
                    //TODO: log
                }
            }
        }



        public void Dispose()
        {
            if (ProcessingGroup == null) 
                return;
            ProcessingGroup.Dispose();
            ProcessingGroup = null;
        }


        public void Send(string destination, BinaryMessage message, int ttl)
        {
            ProcessingGroup.Send(destination,message, ttl);
        }

        public RequestHandle SendRequest(string destination, BinaryMessage message, Action<BinaryMessage> callback)
        {
            return ProcessingGroup.SendRequest(destination, message, callback);
        }

        public IDisposable RegisterHandler(string destination, Func<BinaryMessage, BinaryMessage> handler, string messageType)
        {
            return ProcessingGroup.RegisterHandler(destination, handler, messageType);
        }

        public IDisposable Subscribe(string destination, Action<BinaryMessage, Action<bool>> callback, string messageType,int priority)
        {
            var taskFactory = getTaskFactory(priority);
            return ProcessingGroup.Subscribe(destination, (message, ack) => taskFactory.StartNew(() => callback(message, ack)), messageType);
        }

        public Destination CreateTemporaryDestination()
        {
            return ProcessingGroup.CreateTemporaryDestination();
        }
    }
}