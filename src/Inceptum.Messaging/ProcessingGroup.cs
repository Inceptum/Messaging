using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Inceptum.Messaging.Transports;
using Inceptum.Messaging.Utils;

namespace Inceptum.Messaging
{

    public class InvalidSubscriptionException:InvalidOperationException
    {
        public InvalidSubscriptionException()
        {
        }

        public InvalidSubscriptionException(string message) : base(message)
        {
        }

        public InvalidSubscriptionException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected InvalidSubscriptionException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
    interface ISchedulingStrategy:IDisposable
    {
        TaskFactory GetTaskFactory(int priority);
    }

    class QueuedSchedulingStrategy : ISchedulingStrategy
    {
        private readonly QueuedTaskScheduler m_TaskScheduler;
        private readonly Dictionary<int, TaskFactory> m_TaskFactories = new Dictionary<int, TaskFactory>();

        public QueuedSchedulingStrategy(uint threadCount,uint capacity,string name)
        {
            m_TaskScheduler = new QueuedTaskScheduler((int)threadCount,(int)capacity,name);
            m_TaskFactories = new Dictionary<int, TaskFactory>();
        }

        public TaskFactory GetTaskFactory(int priority)
        {
            if (priority < 0)
                throw new ArgumentException("priority should be >0", "priority");
            lock (m_TaskFactories)
            {
                TaskFactory factory;
                if (!m_TaskFactories.TryGetValue(priority, out factory))
                {
                    var scheduler = m_TaskScheduler.ActivateNewQueue(priority);
                    factory = new TaskFactory(scheduler);
                    m_TaskFactories.Add(priority, factory);
                }
                return factory;
            }
        }

        public void Dispose()
        {
            m_TaskScheduler.Dispose();
        }
    }

    internal class CurrentThreadSchedulingStrategy : ISchedulingStrategy
    {
        public void Dispose()
        {
        }

        public TaskFactory GetTaskFactory(int priority)
        {
            if (priority != 0)
                throw new InvalidSubscriptionException("Priority other then 0 is not applicable for processing group with zero concurrencyLevel (messages are processed on consuming thread)");

            return new TaskFactory(new CurrentThreadTaskScheduler());
        }
    }

    internal class ProcessingGroup : IDisposable
    {
        private readonly ISchedulingStrategy m_SchedulingStrategy;
        private volatile bool m_IsDisposing;
        public string Name { get; private set; }
        private long m_TasksInProgress = 0;
        private long m_ReceivedMessages = 0;
        private long m_ProcessedMessages = 0;
        private long m_SentMessages = 0;
        private readonly uint m_ConcurrencyLevel;

        public ProcessingGroup(string name, ProcessingGroupInfo processingGroupInfo)
        {
            Name = name;
            m_ConcurrencyLevel = Math.Max(processingGroupInfo.ConcurrencyLevel, 0);

            m_SchedulingStrategy = (m_ConcurrencyLevel == 0) 
                ? (ISchedulingStrategy) new CurrentThreadSchedulingStrategy()
                : (ISchedulingStrategy) new QueuedSchedulingStrategy(m_ConcurrencyLevel,processingGroupInfo.QueueCapacity,string.Format("ProcessingGroup '{0}' thread",Name));
        }

        public uint ConcurrencyLevel
        {
            get { return m_ConcurrencyLevel; }
        }

        public long ReceivedMessages
        {
            get { return Interlocked.Read(ref m_ReceivedMessages); }
        }

        public long ProcessedMessages
        {
            get { return Interlocked.Read(ref m_ProcessedMessages); }
        }

        public long SentMessages
        {
            get { return Interlocked.Read(ref m_SentMessages); }
        }
 
  
        public IDisposable Subscribe(IMessagingSession messagingSession,string destination, Action<BinaryMessage, Action<bool>> callback, string messageType,int priority)
        {
            if(m_IsDisposing)
                throw new ObjectDisposedException("ProcessingGroup "+Name);
            var taskFactory = m_SchedulingStrategy.GetTaskFactory(priority);
            var subscription=new SingleAssignmentDisposable();
            subscription.Disposable = messagingSession.Subscribe(destination, (message, ack) =>
            {
                Interlocked.Increment(ref m_TasksInProgress);
                taskFactory.StartNew(() =>
                {
                    Interlocked.Increment(ref m_ReceivedMessages);
                    //if subscription is disposed unack message immediately
                    if (subscription.IsDisposed)
                        ack(false);
                    else
                    {
                        callback(message, ack);
                        Interlocked.Increment(ref m_ProcessedMessages);
                    }
                    Interlocked.Decrement(ref m_TasksInProgress);
                },TaskCreationOptions.HideScheduler);
            }, messageType);
            return subscription;
        }

        public void Dispose()
        {
            m_IsDisposing=true;
            while (Interlocked.Read(ref m_TasksInProgress)>0)
                Thread.Sleep(100);
            m_SchedulingStrategy.Dispose();
        }

        public void Send(IMessagingSession messagingSession, string publish, BinaryMessage message, int ttl)
        {
            messagingSession.Send(publish,message,ttl);
            Interlocked.Increment(ref m_SentMessages);
        }
    }
}