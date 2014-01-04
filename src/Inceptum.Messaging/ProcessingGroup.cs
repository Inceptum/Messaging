using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Inceptum.Messaging.Transports;
using Inceptum.Messaging.Utils;

namespace Inceptum.Messaging
{
    internal class ProcessingGroup : IDisposable 
    {
        private readonly QueuedTaskScheduler m_TaskScheduler;
        private readonly Dictionary<int,TaskFactory> m_TaskFactories=new Dictionary<int, TaskFactory>();
        public string Name { get; private set; }

        public ProcessingGroup(string name, ProcessingGroupInfo processingGroupInfo)
        {
            //TODO: 0 concurrency level  should be treated as same thread. Need to arrange prioritization (meaningless for same thread case)
            Name = name;
            var threadCount = Math.Max(processingGroupInfo.ConcurrencyLevel, 1);
            //TODO:name threads by processing group name
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
  
        public IDisposable Subscribe(IMessagingSession messagingSession,string destination, Action<BinaryMessage, Action<bool>> callback, string messageType,int priority)
        {
            var taskFactory = getTaskFactory(priority);
            return messagingSession.Subscribe(destination, (message, ack) => taskFactory.StartNew(() => callback(message, ack)), messageType);
        }

        public void Dispose()
        {
            m_TaskScheduler.Dispose();
        }
    }
}