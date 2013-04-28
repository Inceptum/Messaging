using System;
using System.Collections.Generic;
using Inceptum.Messaging.Transports;
using Sonic.Jms.Ext;
using Connection = Sonic.Jms.Ext.Connection;
using QueueConnection = Sonic.Jms.QueueConnection;
using QueueConnectionFactory = Sonic.Jms.Cf.Impl.QueueConnectionFactory;

namespace Inceptum.Messaging.Sonic
{
    internal class Transport :  ITransport
    {
        private readonly object m_SyncRoot = new object();
        private readonly TransportInfo m_TransportInfo;
        private volatile bool m_IsDisposed;
        private Action m_OnFailure;
        private readonly Dictionary<Tuple<string,bool>,IProcessingGroup> m_ProcessingGroups=new Dictionary<Tuple<string, bool>, IProcessingGroup>();



        
        private readonly string m_JailedTag;
        private readonly QueueConnection m_Connection;

        public Transport(TransportInfo transportInfo, Action onFailure)
        {
            if (onFailure == null) throw new ArgumentNullException("onFailure");
            m_OnFailure = onFailure;
            m_TransportInfo = transportInfo;

            m_JailedTag = (m_TransportInfo.JailStrategy ?? JailStrategy.None).CreateTag();

            var factory = new QueueConnectionFactory();
            (factory as ConnectionFactory).setConnectionURLs(m_TransportInfo.Broker);
            m_Connection = factory.createQueueConnection(m_TransportInfo.Login, m_TransportInfo.Password);
            ((Connection)m_Connection).setConnectionStateChangeListener(
                new GenericConnectionStateChangeListener(connectionStateHandler));
            ((Connection)m_Connection).setPingInterval(30);
            m_Connection.start();
        }

        public bool IsDisposed
        {
            get { return m_IsDisposed; }
        }
         
 
 
        private void connectionStateHandler(int state)
        {
            lock (m_SyncRoot)
            {
                switch (state)
                {
                    case Constants.FAILED:
                    case Constants.CLOSED:
                        m_OnFailure();
                        Dispose();
                        break;
                }
            }
        }


        public IProcessingGroup CreateProcessingGroup(string name, Action onFailure)
        {
            return new ProcessingGroupWrapper(name, m_Connection, m_JailedTag);
        }

        #region IDisposable Members

        public void Dispose()
        {
            if (m_IsDisposed) return;
            lock (m_SyncRoot)
            {
                if (m_IsDisposed) return;
                m_OnFailure = () => { };
                foreach (var processingGroup in m_ProcessingGroups.Values)
                {
                    processingGroup.Dispose();
                }

                if (m_Connection != null)
                {
                    m_Connection.close();
                }
                m_IsDisposed = true;
            }
        }


        #endregion
    }
}
