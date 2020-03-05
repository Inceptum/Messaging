using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reactive.Disposables;
using System.Text;
using System.Threading;
using Inceptum.Core.Utils;
using Inceptum.Messaging.Contract;
using Inceptum.Messaging.Transports;
using Inceptum.Messaging.Utils;
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
        readonly Dictionary<string, ProcessingGroup> m_ProcessingGroups=new Dictionary<string, ProcessingGroup>();
        readonly Dictionary<string, ProcessingGroupInfo> m_ProcessingGroupInfos = new Dictionary<string, ProcessingGroupInfo>();
        private volatile bool m_IsDisposing;

        public ProcessingGroupManager(ITransportManager transportManager, IDictionary<string, ProcessingGroupInfo> processingGroups=null, int resubscriptionTimeout=60000)
        {
            m_ProcessingGroupInfos = new Dictionary<string, ProcessingGroupInfo>(processingGroups ?? new Dictionary<string, ProcessingGroupInfo>());
            m_TransportManager = transportManager;
            ResubscriptionTimeout = resubscriptionTimeout;
            m_DeferredAcknowledger = new SchedulingBackgroundWorker("DeferredAcknowledgement", () => processDeferredAcknowledgements());
            m_Resubscriber = new SchedulingBackgroundWorker("Resubscription", () => processResubscription());
        }

        public int ResubscriptionTimeout { get; set; }
        public void AddProcessingGroup(string name,ProcessingGroupInfo info)
        {
            lock (m_ProcessingGroups)
            {
                if (m_ProcessingGroups.ContainsKey(name))
                    throw new InvalidOperationException(string.Format("Can not add processing group '{0}'. It already exists.",name));

                m_ProcessingGroupInfos.Add(name, info);
            }
        }

        public bool GetProcessingGroupInfo(string name,out ProcessingGroupInfo  groupInfo)
        {
            ProcessingGroupInfo info;
            if (m_ProcessingGroupInfos.TryGetValue(name, out info))
            {
                groupInfo = new ProcessingGroupInfo(info);
                return true;
            }
            

            groupInfo = null;
            return false;
        }

        public IDisposable Subscribe(Endpoint endpoint, Action<BinaryMessage, AcknowledgeDelegate> callback, string messageType, string processingGroup, int priority)
        {
            if (string.IsNullOrEmpty(processingGroup)) throw new ArgumentNullException("processingGroup","should be not empty string");
            if (m_IsDisposing)
                throw new ObjectDisposedException(GetType().Name);
            var subscriptionHandler = new MultipleAssignmentDisposable();
            Action<int> doSubscribe = null;
            doSubscribe = attemptNumber =>
            {
                string processingGroupName=null;
                if (subscriptionHandler.IsDisposed)
                    return;
               try
                {
                    var group = getProcessingGroup(processingGroup);
                    processingGroupName = @group.Name;
                    if (attemptNumber > 0)
                        m_Logger.Info("Resubscribing for endpoint {0} within processing group '{1}'. Attempt# {2}", endpoint, processingGroupName, attemptNumber);
                    else
                        m_Logger.Info("Subscribing for endpoint {0} within processing group '{1}'", endpoint, processingGroupName);
 
                    var sessionName = getSessionName(@group, priority);

                    var session = m_TransportManager.GetMessagingSession(endpoint.TransportId, sessionName, Helper.CallOnlyOnce(() =>
                    {
                        m_Logger.Info("Subscription for endpoint {0} within processing group '{1}' failure detected. Attempting subscribe again.", endpoint, processingGroupName);
                        doSubscribe(0);
                    }));

   
                    var subscription = group.Subscribe(session, endpoint.Destination.Subscribe,
                        (message, ack) => callback(message, createDeferredAcknowledge(ack)), messageType, priority);
                    var brokenSubscription = subscriptionHandler.Disposable;
                    subscriptionHandler.Disposable = subscription;
                    try
                    {
                        if (attemptNumber > 0)
                            brokenSubscription.Dispose();
                    }
                    catch
                    {
                    }
                    m_Logger.Info("Subscribed for endpoint {0} in processingGroup '{1}' using session {2}", endpoint, processingGroupName, sessionName);
                }
                catch (InvalidSubscriptionException e)
                {
                    m_Logger.ErrorException(string.Format("Failed to subscribe for endpoint {0} within processing group '{1}'", endpoint, processingGroupName), e);
                    throw;
                }
                catch (Exception e)
                {
                    m_Logger.ErrorException(string.Format("Failed to subscribe for endpoint {0} within processing group '{1}'. Attempt# {2}. Will retry in {3}ms", endpoint, processingGroupName, attemptNumber, ResubscriptionTimeout), e);
                    scheduleSubscription(doSubscribe, attemptNumber + 1);
                }
            };
           
            doSubscribe(0);
            
            return subscriptionHandler;
        }


        private string getSessionName(ProcessingGroup processingGroup, int priority)
        {
            if (processingGroup.ConcurrencyLevel == 0)
                return processingGroup.Name;
            return string.Format("{0} priority{1}", processingGroup.Name, priority);
        }

        private ProcessingGroup getProcessingGroup(string processingGroup)
        {
            ProcessingGroup @group;
            lock (m_ProcessingGroups)
            {
                if (m_ProcessingGroups.TryGetValue(processingGroup, out @group)) 
                    return @group;

                ProcessingGroupInfo info;
                if (!m_ProcessingGroupInfos.TryGetValue(processingGroup, out info))
                {
                    info = new ProcessingGroupInfo();
                    m_ProcessingGroupInfos.Add(processingGroup, info);
                }
                @group = new ProcessingGroup(processingGroup, info);
                m_ProcessingGroups.Add(processingGroup, @group);
            }
            return @group;
        }

        public void Send(Endpoint endpoint, BinaryMessage message, int ttl, string processingGroup)
        {
            var group = getProcessingGroup(processingGroup);
            var session = m_TransportManager.GetMessagingSession(endpoint.TransportId, getSessionName(group, 0));

            group.Send(session,endpoint.Destination.Publish, message, ttl);

        }


        private void scheduleSubscription(Action<int> subscribe, int attemptCount)
        {
            lock (m_ResubscriptionSchedule)
            {
                m_ResubscriptionSchedule.Add(Tuple.Create<DateTime, Action>(DateTime.Now.AddMilliseconds(attemptCount == 0 ? 100 : ResubscriptionTimeout), () => subscribe(attemptCount)));
                m_Resubscriber.Schedule(ResubscriptionTimeout);
            }
        }


        private void processDeferredAcknowledgements(bool all = false)
        {
            Tuple<DateTime, Action>[] ready;
            var remove = new List<Tuple<DateTime, Action>>();
            lock (m_DeferredAcknowledgements)
            {
                ready = all
                                ? m_DeferredAcknowledgements.ToArray()
                                : m_DeferredAcknowledgements.Where(r => r.Item1 <= DateTime.Now).ToArray();
            }

            foreach (var t in ready)
            {
                try
                {
                    t.Item2();
                    remove.Add(t);
                }
                catch (MessagingSessionClosedException e)
                {
                    m_Logger.WarnException("Deferred acknowledge failed due to session close.", e);
                    remove.Add(t);
                }
                catch (Exception e)
                {
                    m_Logger.WarnException("Deferred acknowledge failed. Will retry later.", e);
                }
            }

            lock (m_DeferredAcknowledgements)
            {
                Array.ForEach(remove.ToArray(), r => m_DeferredAcknowledgements.Remove(r));
            }
        }


        private void processResubscription(bool all = false)
        {
            Tuple<DateTime, Action>[] ready;
            var succeeded = new List<Tuple<DateTime, Action>>();
            lock (m_ResubscriptionSchedule)
            {
                ready = all
                                ? m_ResubscriptionSchedule.ToArray()
                                : m_ResubscriptionSchedule.Where(r => r.Item1 <= DateTime.Now).ToArray();
            }

            foreach (var t in ready)
            {
                try
                {
                    t.Item2();
                    succeeded.Add(t);
                }
                catch (Exception e)
                {
                    m_Logger.DebugException("Resubscription failed. Will retry later.", e);

                }
            }


            lock (m_ResubscriptionSchedule)
            {
                Array.ForEach(succeeded.ToArray(), r => m_ResubscriptionSchedule.Remove(r));
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