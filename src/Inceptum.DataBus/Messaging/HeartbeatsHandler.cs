/*using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using Inceptum.Core.Messaging;

namespace Inceptum.DataBus.Messaging
{
    /// <summary>
    /// TODO : Make internal in future
    /// </summary>
    public class HeartbeatsHandler<TData, THeartbeatMessage> : IDisposable ,IObservable<THeartbeatMessage>
        where THeartbeatMessage : class
    {
       

        
        private readonly IMessagingEngine m_MessagingEngine;
        private readonly string m_TransportId;
        private readonly string m_HeartBeatQueue;
        private readonly List<Subject<TData>> m_Feeds = new List<Subject<TData>>();
        private THeartbeatMessage m_Message;
        private DateTime m_LastMessageTime;
        private HeartbeatInfo m_HeartbeatInfo;
        private bool m_CanSubscribe;
        private IDisposable m_Check;
      
        readonly Subject<THeartbeatMessage> m_HbSubject = new Subject<THeartbeatMessage>();
        private long m_HbWaitersCount=0;
        private IDisposable m_HbSubscription;
        private readonly object m_SyncRoot=new object();



        public HeartbeatsHandler(IMessagingEngine messagingEngine, string transportId, string heartBeatQueue)
        {
            m_HeartBeatQueue = heartBeatQueue;
            m_TransportId = transportId;
            m_MessagingEngine = messagingEngine;
        }


        [MethodImpl(MethodImplOptions.Synchronized)]
        public IDisposable AddFeed(Subject<TData> dataFeed, HeartbeatInfo heartbeatInfo)
        {
            m_HeartbeatInfo = heartbeatInfo;
            m_CanSubscribe = true;
            bool subscriptionUpdated = heartbeatInfo != m_HeartbeatInfo;

            if (subscriptionUpdated)
            {
                m_HeartbeatInfo = heartbeatInfo;
                unsubscribeFromSource();
            }

            if (( m_HbWaitersCount==0 &&m_Feeds.Count == 0) || subscriptionUpdated )
            {
                subscribeToSource();
            }

            m_Feeds.Add(dataFeed);
            return Disposable.Create(() => removeFeed(dataFeed));
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void removeFeed(Subject<TData> dataFeed)
        {
            if (m_Feeds.Remove(dataFeed) && m_Feeds.Count == 0)
            {
                unsubscribeFromSource();
            }
        }

        private void subscribeToSource()
        {
            if(!m_CanSubscribe)
                return;

            var endpoint = new Endpoint(m_TransportId, m_HeartBeatQueue);
            lock (m_SyncRoot)
            {
                if (m_HbSubscription==null)
                     m_HbSubscription = m_MessagingEngine.Subscribe<THeartbeatMessage>(endpoint, processHeartbeat);
            }


            scheduleCheck(m_Message);
        }

        private void scheduleCheck(THeartbeatMessage message)
        {
            if (m_Check != null)
            {
                m_Check.Dispose();
                m_Check = null;
            }

                
            m_Check = Scheduler.TaskPool.Schedule(
                                                        TimeSpan.FromMilliseconds(2*_adjuster.ValidateHeartbeatInterval(message, m_HeartbeatInfo)), 
                                                        () => verifyHbAvailability(message)
                                                    );
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void verifyHbAvailability(THeartbeatMessage oldMessage)
        {
            if (ReferenceEquals(m_Message, oldMessage))
            {
                var now = DateTime.Now;
                unsubscribeFromSource();
                var feeds = m_Feeds.ToArray();
                m_Feeds.Clear();
                foreach (var feed in feeds)
                {
                    var errorMessage =
                        string.Format(
                            "Heartbeat was not received within double heartbeat interval. \r\nLast hb message received at: {0}\r\nHB loss detected at: {1}\r\nHB interval {2}\r\nHB subject{3}",
                            m_LastMessageTime, now, m_HeartbeatInfo.Interval, m_HeartbeatInfo.Destination);
                    feed.OnError(new HeartbeatLostException(errorMessage));
                }
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void processHeartbeat(THeartbeatMessage message)
        {
            
                m_HbSubject.OnNext(message);
                m_LastMessageTime = DateTime.Now;
                m_Message = message;
                if (m_Feeds.Count > 0)
                {
                    scheduleCheck(message);
                }
          
        }

        private void unsubscribeFromSource()
        {
            lock (m_SyncRoot)
            {
                if (m_HbSubscription != null)
                    m_HbSubscription.Dispose();
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Dispose()
        {
            if (m_Check != null)
            {
                m_Check.Dispose();
                m_Check = null;
            }

            m_Feeds.Clear();
            unsubscribeFromSource();
        }

        public IDisposable Subscribe(IObserver<THeartbeatMessage> observer)
        {
            return m_HbSubject.Subscribe(observer);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IDisposable WaitForHb(Action callback, long timeout)
        {

            if (m_HbWaitersCount++ == 1 && m_Feeds.Count == 0)
            {
                subscribeToSource();
            }

            Action<object> report = o =>
                                        {
                                            lock (this)
                                            {
                                                if (m_HbWaitersCount-- <= 0 && m_Feeds.Count == 0)
                                                {
                                                    unsubscribeFromSource();
                                                }
                                            }
                                            callback();
                                        };
            return m_HbSubject.Take(1).Timeout(TimeSpan.FromMilliseconds(timeout)).Subscribe(report, report);
        }
    }
}*/