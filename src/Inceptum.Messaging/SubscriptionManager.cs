using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using Castle.Core.Logging;
using Inceptum.Core.Utils;
using Inceptum.Messaging.Contract;
using Inceptum.Messaging.Transports;

namespace Inceptum.Messaging
{
    internal class SubscriptionManager:IDisposable
    {
        private readonly ITransportManager m_TransportManager;
        readonly List<Tuple<DateTime, Action>> m_DeferredAcknowledgements = new List<Tuple<DateTime, Action>>();
        readonly List<Tuple<DateTime, Action>> m_ResubscriptionSchedule = new List<Tuple<DateTime, Action>>();
        private readonly SchedulingBackgroundWorker m_DeferredAcknowledger;
        private readonly SchedulingBackgroundWorker m_Resubscriber;
        private ILogger m_Logger = NullLogger.Instance;
        private int m_ResubscriptionTimeout;

        public ILogger Logger
        {
            get { return m_Logger; }
            set { m_Logger = value??NullLogger.Instance; }
        }

        public SubscriptionManager(ITransportManager transportManager, int resubscriptionTimeout=60000)
        {
            m_TransportManager = transportManager;
            m_ResubscriptionTimeout = resubscriptionTimeout;
            m_DeferredAcknowledger = new SchedulingBackgroundWorker("DeferredAcknowledgement", () => processDefferredAcknowledgements());
            m_Resubscriber = new SchedulingBackgroundWorker("Resubscrription", () => processResubscription());
        }

        public IDisposable Subscribe(Endpoint endpoint, CallbackDelegate<BinaryMessage> callback, string messageType)
        {
            var subscriptionHandler = new MultipleAssignmentDisposable();
            Action<int> doSubscribe = null;
            doSubscribe = attemptNumber =>
            {
                if (subscriptionHandler.IsDisposed)
                    return;
                if (attemptNumber > 0)
                    m_Logger.InfoFormat("Resubscribing for endpoint {0}. Attempt# {1}", endpoint, attemptNumber);
                else
                    m_Logger.InfoFormat("Subscribing for endpoint {0}", endpoint);
                try
                {
                    var processingGroup = m_TransportManager.GetProcessingGroup(endpoint.TransportId, endpoint.Destination,
                        () => {
                            m_Logger.InfoFormat("Subscription for endpoint {0} failure detected. Attempting subscribe again.", endpoint);
                            doSubscribe(0);
                        });
                    var subscription = processingGroup.Subscribe(endpoint.Destination, (message, ack) => callback(message, createDeferredAcknowledge(ack)),
                        messageType);
                    subscriptionHandler.Disposable = subscription;
                    m_Logger.InfoFormat("Subscribed for endpoint {0}", endpoint);
                }
                catch (Exception e)
                {
                    m_Logger.ErrorFormat(e, "Failed to subscribe for endpoint {0}. Attempt# {1}. Will retry in {2}ms", endpoint, attemptNumber,m_ResubscriptionTimeout);
                    scheduleSubscription(doSubscribe, attemptNumber + 1);
                }
            };
           
            doSubscribe(0);
            
            return subscriptionHandler;

/*

            var processingGroup = m_TransportManager.GetProcessingGroup(endpoint.TransportId, endpoint.Destination);
            IDisposable subscription = processingGroup.Subscribe(endpoint.Destination, (message, ack) => callback(message, createDeferredAcknowledge(ack)), messageType);
            return subscription;
*/
        }

        private void scheduleSubscription(Action<int> subscribe, int attemptCount)
        {
            lock (m_ResubscriptionSchedule)
            {
                m_ResubscriptionSchedule.Add(Tuple.Create<DateTime, Action>(DateTime.Now.AddMilliseconds(attemptCount==0?100:m_ResubscriptionTimeout), () => subscribe(attemptCount)));
                m_Resubscriber.Schedule(m_ResubscriptionTimeout);
            }
        }


        private void processDefferredAcknowledgements(bool all = false)
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
            processDefferredAcknowledgements(true);
            m_DeferredAcknowledger.Dispose();
            m_Resubscriber.Dispose();
        }
    }
}