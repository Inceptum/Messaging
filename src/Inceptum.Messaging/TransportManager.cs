using System;
using System.Collections.Generic;
using System.Configuration;
using Inceptum.Core.Messaging;
using Sonic.Jms;

namespace Inceptum.Messaging
{
    internal class TransportManager : IDisposable
    {
        private readonly Dictionary<TransportInfo, Transport> m_Connections = new Dictionary<TransportInfo, Transport>();
        private readonly ITransportResolver m_TransportResolver;

        public TransportManager(ITransportResolver transportResolver)
        {
            if (transportResolver == null) throw new ArgumentNullException("transportResolver");
            m_TransportResolver = transportResolver;
        }

        #region IDisposable Members

        public void Dispose()
        {
            lock (m_Connections)
            {
                foreach (var transport in m_Connections.Values)
                {
                    transport.Dispose();
                }
            }
        }

        #endregion

        public event TrasnportEventHandler TransportEvents;

        //public Session GetSession(string transportId, bool topic = false)
        //{
        //    //TODO: need to introduce TransportOutdated event when failover is required
        //    //TODO: need to move resolving to engine - only once connection should be created for transport registered several time wiith different ids
        //    var transport = GetTransport(transportId);
        //    return transport.GetSession(topic);
        //}
        
        public Transport GetTransport(string transportId)
        {
            var transportInfo = m_TransportResolver.GetTransport(transportId);
            
            if (transportInfo == null)
                throw new ConfigurationErrorsException(string.Format("Transport '{0}' is not resolvable", transportId));
            Transport transport;

            if (!m_Connections.TryGetValue(transportInfo, out transport) || transport == null || transport.IsDisposed)
            {
                lock (m_Connections)
                {
                    if (!m_Connections.TryGetValue(transportInfo, out transport) || transport == null || transport.IsDisposed)
                    {
                        transport = new Transport(transportInfo, () => ProceesTarnsportFailure(transportId, transportInfo));
                        if (m_Connections.ContainsKey(transportInfo))
                            m_Connections.Remove(transportInfo);
                        m_Connections.Add(transportInfo, transport);
                    }
                }
            }
            return transport;
        }

        internal virtual void ProceesTarnsportFailure(string transportId, TransportInfo transportInfo)
        {
            lock (m_Connections)
            {
                //m_Connections[transportInfo] = null;
                m_Connections.Remove(transportInfo);
            }
            var handler = TransportEvents;
            if (handler != null) handler(transportId, Core.Messaging.TransportEvents.Failure);
        }
    }
}
