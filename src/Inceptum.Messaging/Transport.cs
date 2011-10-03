using System;
using Sonic.Jms.Ext;
using QueueConnection = Sonic.Jms.QueueConnection;
using QueueConnectionFactory = Sonic.Jms.QueueConnectionFactory;
using QueueSession = Sonic.Jms.Ext.QueueSession;
using Session = Sonic.Jms.Ext.Session;
using SessionMode = Sonic.Jms.SessionMode;

namespace Inceptum.Messaging
{
    internal class Transport : IDisposable
    {
        private readonly object m_SyncRoot = new object();
        private readonly TransportInfo m_TransportInfo;
        private volatile QueueConnection m_Connection;
        private volatile bool m_IsDisposed;
        private QueueSession m_Session;
        private Session m_TopicSession;
        private Action m_OnFailure;

        public Transport(TransportInfo transportInfo, Action onFailure)
        {
            if (onFailure == null) throw new ArgumentNullException("onFailure");
            m_OnFailure = onFailure;
            m_TransportInfo = transportInfo;
        }

        #region IDisposable Members

        public void Dispose()
        {
            if (m_IsDisposed) return;
            lock (m_SyncRoot)
            {
                if (m_IsDisposed) return;
                m_OnFailure = () => { };
                if (m_Connection != null)
                {
                    m_Connection.close();
                }
                m_IsDisposed = true;
            }
        }

        #endregion

        public bool IsDisposed
        {
            get { return m_IsDisposed; }
        }

        public Sonic.Jms.Session GetSession(bool topic)
        {
            if (m_Connection == null)
            {
                lock (m_SyncRoot)
                {
                    if (m_Connection == null)
                    {
                        QueueConnectionFactory factory = (new Sonic.Jms.Cf.Impl.QueueConnectionFactory(m_TransportInfo.Broker));
                        m_Connection = factory.createQueueConnection(m_TransportInfo.Login, m_TransportInfo.Password);
                        ((Connection)m_Connection).setConnectionStateChangeListener(new GenericConnectionStateChangeListener(connectionStateHandler));
                        ((Connection)m_Connection).setPingInterval(30);
                        //TODO: handle connection close
                        //TODO: there should be more then 1 session per connectionl. Current implementation is temporary
                        m_Connection.start();
                        m_Session = (QueueSession) m_Connection.createQueueSession(false, SessionMode.AUTO_ACKNOWLEDGE);
                        m_Session.setFlowControlDisabled(true);

                        m_TopicSession = (Session) m_Connection.createSession(false, SessionMode.AUTO_ACKNOWLEDGE);
                        m_TopicSession.setFlowControlDisabled(true);
                    
                    }
                }
            }
            return topic ? m_TopicSession : m_Session;
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
                    case Constants.ACTIVE:
                    case Constants.RECONNECTING:
                        break;
                    default:
                        break;
                }
            }
        }
    }
}
