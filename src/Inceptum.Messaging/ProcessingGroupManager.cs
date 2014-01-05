using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Text;
using Inceptum.Core.Utils;
using Inceptum.Messaging.Contract;
using Inceptum.Messaging.Transports;
using NLog;

namespace Inceptum.Messaging
{
    internal class ProcessingGroupManager:IDisposable
    {
        private readonly ITransportManager m_TransportManager;
        readonly List<Tuple<DateTime, Action>> m_DeferredAcknowledgements = new List<Tuple<DateTime, Action>>();
        readonly List<Tuple<DateTime, Action>> m_ResubscriptionSchedule = new List<Tuple<DateTime, Action>>();
        private readonly SchedulingBackgroundWorker m_DeferredAcknowledger;
        private readonly SchedulingBackgroundWorker m_Resubscriber;
        readonly Logger m_Logger = LogManager.GetCurrentClassLogger();
        private readonly int m_ResubscriptionTimeout;
        readonly Dictionary<string, ProcessingGroup> m_ProcessingGroups=new Dictionary<string, ProcessingGroup>();
        readonly Dictionary<string, ProcessingGroupInfo> m_ProcessingGroupInfos = new Dictionary<string, ProcessingGroupInfo>();
        private volatile bool m_IsDisposing;

        public ProcessingGroupManager(ITransportManager transportManager, IDictionary<string, ProcessingGroupInfo> processingGroups=null, int resubscriptionTimeout=60000)
        {
            m_ProcessingGroupInfos = new Dictionary<string, ProcessingGroupInfo>(processingGroups ?? new Dictionary<string, ProcessingGroupInfo>());
            m_TransportManager = transportManager;
            m_ResubscriptionTimeout = resubscriptionTimeout;
            m_DeferredAcknowledger = new SchedulingBackgroundWorker("DeferredAcknowledgement", () => processDeferredAcknowledgements());
            m_Resubscriber = new SchedulingBackgroundWorker("Resubscription", () => processResubscription());
        }

        public IDisposable Subscribe(Endpoint endpoint, CallbackDelegate<BinaryMessage> callback, string messageType, string processingGroup, int priority)
        {
            if (string.IsNullOrEmpty(processingGroup)) throw new ArgumentNullException("processingGroup","should be not empty string");
            if (m_IsDisposing)
                throw new ObjectDisposedException(GetType().Name);
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
                    var group = getProcessingGroup(processingGroup);


                    var session = m_TransportManager.GetMessagingSession(endpoint.TransportId,group.Name,() => {
                            m_Logger.Info("Subscription for endpoint {0} failure detected. Attempting subscribe again.", endpoint);
                            doSubscribe(0);
                        });


                    var subscription = group.Subscribe(session,endpoint.Destination.Subscribe, (message, ack) => callback(message, createDeferredAcknowledge(ack)),messageType,priority);
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

        private ProcessingGroup getProcessingGroup(string processingGroup)
        {
            ProcessingGroup @group;
            lock (m_ProcessingGroups)
            {
                if (!m_ProcessingGroups.TryGetValue(processingGroup, out @group))
                {
                    ProcessingGroupInfo info;
                    if (!m_ProcessingGroupInfos.TryGetValue(processingGroup, out info))
                        info = new ProcessingGroupInfo();
                    @group = new ProcessingGroup(processingGroup, info);
                    m_ProcessingGroups.Add(processingGroup, @group);
                }
            }
            return @group;
        }

        public void Send(Endpoint endpoint, BinaryMessage message, int ttl, string processingGroup)
        {
            var group = getProcessingGroup(processingGroup);
            var session = m_TransportManager.GetMessagingSession(endpoint.TransportId,group.Name);

            group.Send(session,endpoint.Destination.Publish, message, ttl);

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


        public string GetStatistics()
        {
            var stats=new StringBuilder();
            int length = m_ProcessingGroups.Keys.Max(k=>k.Length);
            m_ProcessingGroups.Aggregate(stats,
                (builder, pair) => builder.AppendFormat("{0,-" + length + "}\tConcurrencyLevel:{1:-10}\tSent:{2}\tReceived:{3}\tProcessed:{4}" + Environment.NewLine, pair.Key, pair.Value.ConcurrencyLevel == 0 ? "[current thread]" : pair.Value.ConcurrencyLevel.ToString(), pair.Value.SentMessages, pair.Value.ReceivedMessages, pair.Value.ProcessedMessages));
            return stats.ToString();
        }

        public void Dispose()
        {
            m_IsDisposing = true;
            m_DeferredAcknowledger.Dispose();
            m_Resubscriber.Dispose();

            foreach (var processingGroup in m_ProcessingGroups.Values)
            {
                processingGroup.Dispose();
            }
            processDeferredAcknowledgements(true);
        }
    }
}