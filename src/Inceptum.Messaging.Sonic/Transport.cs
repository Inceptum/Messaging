using System;
using System.Collections.Generic;
using Inceptum.Messaging.Contract;
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
        private volatile bool m_IsDisposed;
        private Action m_OnFailure;
        private readonly MessageFormat m_MessageFormat;
        private readonly List<IProcessingGroup> m_ProcessingGroups = new List<IProcessingGroup>();
        private readonly string m_JailedTag;
        private readonly QueueConnection m_Connection;

        public Transport(TransportInfo transportInfo, Action onFailure, MessageFormat messageFormat)
        {
            if (onFailure == null) throw new ArgumentNullException("onFailure");
            m_OnFailure = onFailure;
            m_MessageFormat = messageFormat;
            m_JailedTag = (transportInfo.JailStrategy ?? JailStrategy.None).CreateTag();

            var factory = new QueueConnectionFactory();
            (factory as ConnectionFactory).setConnectionURLs(transportInfo.Broker);
            m_Connection = factory.createQueueConnection(transportInfo.Login, transportInfo.Password);
            ((Connection)m_Connection).setConnectionStateChangeListener(new GenericConnectionStateChangeListener(connectionStateHandler));
            ((Connection)m_Connection).setPingInterval(30);
            m_Connection.start();
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

        public IProcessingGroup CreateProcessingGroup(Action onFailure)
        {
            IProcessingGroup group;
            lock (m_SyncRoot)
            {
                group = new ProcessingGroupWrapper(m_Connection, m_JailedTag, m_MessageFormat);
                m_ProcessingGroups.Add(group);
            }
            return group;
        }

        public bool VerifyDestination(Destination destination, EndpointUsage usage, bool configureIfRequired, out string error)
        {
            throw new NotImplementedException();
        }

        #region IDisposable Members

        public void Dispose()
        {
            if (m_IsDisposed) return;
            lock (m_SyncRoot)
            {
                if (m_IsDisposed) return;
                m_OnFailure = () => { };
                foreach (var processingGroup in m_ProcessingGroups)
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
