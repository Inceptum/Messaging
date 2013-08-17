using System;
using System.Collections.Generic;
using System.Linq;
using Inceptum.Core.Utils;
using Inceptum.Messaging.Contract;
using Inceptum.Messaging.Transports;

namespace Inceptum.Messaging
{
    internal class SubscriptionManager:IDisposable
    {
        private readonly TransportManager m_TransportManager;
        readonly List<Tuple<DateTime, Action>> m_DeferredAcknowledgements = new List<Tuple<DateTime, Action>>();
        private readonly SchedulingBackgroundWorker m_DeferredAcknowledgementManager;



        public SubscriptionManager(TransportManager transportManager)
        {
            m_TransportManager = transportManager;
            m_DeferredAcknowledgementManager = new SchedulingBackgroundWorker("DeferredAcknowledgementManager", () => processDefferredAcknowledgements());
        }

        public IDisposable Subscribe(Endpoint endpoint, CallbackDelegate<BinaryMessage> callback, string messageType)
        {
            var processingGroup = m_TransportManager.GetProcessingGroup(endpoint.TransportId, endpoint.Destination);
            IDisposable subscription = processingGroup.Subscribe(endpoint.Destination, (message, ack) => callback(message, createDeferredAcknowledge(ack)), messageType);
            return subscription;
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
                    m_DeferredAcknowledgementManager.Schedule(l);
                }
            };
        }

        public void Dispose()
        {
            processDefferredAcknowledgements(true);
            m_DeferredAcknowledgementManager.Dispose();
        }
    }
}