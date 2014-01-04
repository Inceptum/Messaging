using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using Inceptum.Core.Utils;
using Inceptum.Messaging.Contract;
using Inceptum.Messaging.Transports;
using NLog;

namespace Inceptum.Messaging
{
    internal class Subscription
    {
        
    }

    internal class SubscriptionManager:IDisposable
    {
        private readonly ITransportManager m_TransportManager;
        readonly List<Tuple<DateTime, Action>> m_DeferredAcknowledgements = new List<Tuple<DateTime, Action>>();
        readonly List<Tuple<DateTime, Action>> m_ResubscriptionSchedule = new List<Tuple<DateTime, Action>>();
        private readonly SchedulingBackgroundWorker m_DeferredAcknowledger;
        private readonly SchedulingBackgroundWorker m_Resubscriber;
        readonly Logger m_Logger = LogManager.GetCurrentClassLogger();
        private int m_ResubscriptionTimeout;
        readonly Dictionary<string, EngineProcessingGroup> m_ProcessingGroups=new Dictionary<string, EngineProcessingGroup>();
        readonly Dictionary<string, ProcessingGroupInfo> m_ProcessingGroupInfos = new Dictionary<string, ProcessingGroupInfo>();
 

        public SubscriptionManager(ITransportManager transportManager, Dictionary<string, ProcessingGroupInfo> processingGroups=null, int resubscriptionTimeout=60000)
        {
            m_ProcessingGroupInfos = processingGroups ?? new Dictionary<string, ProcessingGroupInfo>();
            m_TransportManager = transportManager;
            m_ResubscriptionTimeout = resubscriptionTimeout;
            //TODO:name threads by processing group name
            m_DeferredAcknowledger = new SchedulingBackgroundWorker("DeferredAcknowledgement", () => processDeferredAcknowledgements());
            m_Resubscriber = new SchedulingBackgroundWorker("Resubscription", () => processResubscription());
        }

        public IDisposable Subscribe(Endpoint endpoint, CallbackDelegate<BinaryMessage> callback, string messageType, string processingGroup, int priority)
        {
            //TODO: meaningful but still unique name
            //processingGroup = processingGroup ?? Guid.NewGuid().ToString();
            processingGroup = processingGroup ?? endpoint.Destination.Subscribe;
            var subscriptionHandler = new MultipleAssignmentDisposable();
            Action<int> doSubscribe = null;
            doSubscribe = attemptNumber =>
            {
                if (subscriptionHandler.IsDisposed)
                    return;
                if (attemptNumber > 0)
                    m_Logger.Info("Resubscribing for endpoint {0}. Attempt# {1}", endpoint, attemptNumber);
                else
                    m_Logger.Info("Subscribing for endpoint {0}", endpoint);
                try
                {
                    EngineProcessingGroup group;
                    lock (m_ProcessingGroups)
                    {
                        if (!m_ProcessingGroups.TryGetValue(processingGroup, out group))
                        {
                            ProcessingGroupInfo info;
                            if(!m_ProcessingGroupInfos.TryGetValue(processingGroup, out info))
                                info = new ProcessingGroupInfo { ConcurrencyLevel = 1 };
                            group=new EngineProcessingGroup(null,processingGroup,info);
                            m_ProcessingGroups.Add(processingGroup,group);
                        }
                    }


                    //TODO:store and manage dispose of PG (one messaging pg per engine pg and transport pair)
                    var procGroup = m_TransportManager.GetProcessingGroup(endpoint.TransportId, processingGroup??endpoint.Destination.ToString(),
                        () => {
                            m_Logger.Info("Subscription for endpoint {0} failure detected. Attempting subscribe again.", endpoint);
                            doSubscribe(0);
                        });


                    var subscription = group.Subscribe(procGroup,endpoint.Destination.Subscribe, (message, ack) => callback(message, createDeferredAcknowledge(ack)),messageType,priority);
                    var brokenSubscription = subscriptionHandler.Disposable;
                    subscriptionHandler.Disposable = subscription;
                    try
                    {
                        if (attemptNumber > 0)
                            brokenSubscription.Dispose();
                    }catch{}
                    m_Logger.Info("Subscribed for endpoint {0}", endpoint);
                }
                catch (Exception e)
                {
                    m_Logger.ErrorException(string.Format("Failed to subscribe for endpoint {0}. Attempt# {1}. Will retry in {2}ms", endpoint, attemptNumber,m_ResubscriptionTimeout),e);
                    scheduleSubscription(doSubscribe, attemptNumber + 1);
                }
            };
           
            doSubscribe(0);
            
            return subscriptionHandler;
        }

        private void scheduleSubscription(Action<int> subscribe, int attemptCount)
        {
            lock (m_ResubscriptionSchedule)
            {
                m_ResubscriptionSchedule.Add(Tuple.Create<DateTime, Action>(DateTime.Now.AddMilliseconds(attemptCount==0?100:m_ResubscriptionTimeout), () => subscribe(attemptCount)));
                m_Resubscriber.Schedule(m_ResubscriptionTimeout);
            }
        }


        private void processDeferredAcknowledgements(bool all = false)
        {
            Tuple<DateTime, Action>[] ready;
            lock (m_DeferredAcknowledgements)
            {
                ready = all
                                ? m_DeferredAcknowledgements.ToArray()
                                : m_DeferredAcknowledgements.Where(r => r.Item1 <= DateTime.Now).ToArray();
            }
            Array.ForEach(ready, r => r.Item2());

            lock (m_DeferredAcknowledgements)
            {
                Array.ForEach(ready, r => m_DeferredAcknowledgements.Remove(r));
            }
        }
        private void processResubscription(bool all = false)
        {
            Tuple<DateTime, Action>[] ready;
            lock (m_ResubscriptionSchedule)
            {
                ready = all
                                ? m_ResubscriptionSchedule.ToArray()
                                : m_ResubscriptionSchedule.Where(r => r.Item1 <= DateTime.Now).ToArray();
            }

            Array.ForEach(ready, r => r.Item2());

            lock (m_ResubscriptionSchedule)
            {
                Array.ForEach(ready, r => m_ResubscriptionSchedule.Remove(r));
            }
        }

        private AcknowledgeDelegate createDeferredAcknowledge(Action<bool> ack)
        {
            return (l, b) =>
            {
                if (l == 0)
                {
                    ack(b);
                    return;
                }

                lock (m_DeferredAcknowledgements)
                {
                    m_DeferredAcknowledgements.Add(Tuple.Create<DateTime, Action>(DateTime.Now.AddMilliseconds(l), () => ack(b)));
                    m_DeferredAcknowledger.Schedule(l);
                }
            };
        }

        public void Dispose()
        {
            processDeferredAcknowledgements(true);
            m_DeferredAcknowledger.Dispose();
            m_Resubscriber.Dispose();
        }
    }
}