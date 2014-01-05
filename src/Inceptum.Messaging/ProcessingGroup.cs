using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using Inceptum.Messaging.Transports;
using Inceptum.Messaging.Utils;

namespace Inceptum.Messaging
{
    internal class ProcessingGroup : IDisposable 
    {
        private readonly QueuedTaskScheduler m_TaskScheduler;
        private readonly Dictionary<int,TaskFactory> m_TaskFactories=new Dictionary<int, TaskFactory>();
        private volatile bool m_IsDisposing;
        public string Name { get; private set; }
        private long m_TasksInProgress = 0;
        private long m_ReceivedMessages = 0;
        private long m_SentMessages = 0;

        public ProcessingGroup(string name, ProcessingGroupInfo processingGroupInfo)
        {
            //TODO: 0 concurrency level  should be treated as same thread. Need to arrange prioritization (meaningless for same thread case)
            Name = name;
            var threadCount = Math.Max(processingGroupInfo.ConcurrencyLevel, 1);
            //TODO:name threads by processing group name
            m_TaskScheduler = new QueuedTaskScheduler(threadCount);

            m_TaskFactories = new Dictionary<int, TaskFactory>();
        }

        public long ReceivedMessages
        {
            get { return Interlocked.Read(ref m_ReceivedMessages); }
        }

        public long SentMessages
        {
            get { return Interlocked.Read(ref m_SentMessages); }
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
            if(m_IsDisposing)
                throw new ObjectDisposedException("ProcessingGroup "+Name);
            var taskFactory = getTaskFactory(priority);
            var subscription=new SingleAssignmentDisposable();
            subscription.Disposable = messagingSession.Subscribe(destination, (message, ack) =>
            {
                Interlocked.Increment(ref m_TasksInProgress);
                taskFactory.StartNew(() =>
                {
                    //if subscription is disposed unack message immediately
                    if (subscription.IsDisposed)
                        ack(false);
                    else
                    {
                        callback(message, ack);
                        Interlocked.Increment(ref m_ReceivedMessages);
                    }
                    Interlocked.Decrement(ref m_TasksInProgress);
                });
            }, messageType);
            return subscription;
        }

        public void Dispose()
        {
            m_IsDisposing=true;
            while (Interlocked.Read(ref m_TasksInProgress)>0)
                Thread.Sleep(100);
            m_TaskScheduler.Dispose();
        }

        public void Send(IMessagingSession messagingSession, string publish, BinaryMessage message, int ttl)
        {
            messagingSession.Send(publish,message,ttl);
            Interlocked.Increment(ref m_SentMessages);
        }
    }
}