using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using Inceptum.Core.Messaging;
using Sonic.Jms;

namespace Inceptum.Messaging
{
   

    internal class TransportManager : IDisposable
    {
        private class ResolvedTransport
        {
            readonly List<string> m_KnownIds=new List<string>();
            public IEnumerable<string> KnownIds { get { return m_KnownIds.ToArray(); } }
            public Transport Transport { get; private set; }

            public void AssignTransport(Transport transport)
            {
                
                if (transport == null) throw new ArgumentNullException("transport");
                Transport = transport;
            }

            public void AddId(string transportId)
            {
                if (string.IsNullOrEmpty(transportId)) throw new ArgumentNullException("transportId");
                if(!m_KnownIds.Contains(transportId))
                    m_KnownIds.Add(transportId);
            }
        }


        private readonly Dictionary<TransportInfo, ResolvedTransport> m_Connections = new Dictionary<TransportInfo, ResolvedTransport>();
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
                foreach (var transport in m_Connections.Values.Select(t=>t.Transport).Distinct())
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
            ResolvedTransport transport;

            bool newTransport = false;

            if (!m_Connections.TryGetValue(transportInfo, out transport) || transport == null || transport.Transport.IsDisposed)
            {
                lock (m_Connections)
                {
                    if (!m_Connections.TryGetValue(transportInfo, out transport) || transport == null || transport.Transport.IsDisposed)
                    {
                        transport = new ResolvedTransport();
                        if (m_Connections.ContainsKey(transportInfo))
                            m_Connections.Remove(transportInfo);
                        m_Connections.Add(transportInfo, transport);
                    }
                }
                newTransport = true;
            }

            lock (transport)
            {
                if (newTransport)
                    transport.AssignTransport(new Transport(transportInfo, () => ProcessTransportFailure(transportInfo)));
                transport.AddId(transportId);
                return transport.Transport;
            }
        }

        internal virtual void ProcessTransportFailure(TransportInfo transportInfo)
        {
            ResolvedTransport transport;
            lock (m_Connections)
            {
                if(!m_Connections.TryGetValue(transportInfo, out transport))
                    return;
                m_Connections.Remove(transportInfo);
            }

            var handler = TransportEvents;
            if (handler == null) return;

            lock (transport)
            {
                foreach (var transportId in transport.KnownIds)
                {
                    handler(transportId, Core.Messaging.TransportEvents.Failure);
                }
            }
        }
    }
}
