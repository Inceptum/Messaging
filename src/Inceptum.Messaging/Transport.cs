using System;
using System.Globalization;
using Sonic.Jms;
using Sonic.Jms.Ext;
using Connection = Sonic.Jms.Ext.Connection;
using MessageConsumer = Sonic.Jms.MessageConsumer;
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

        private const string JAILED_PROPERTY_NAME = "JAILED_TAG";
        private readonly string m_JailedTag;
        internal string JailedSelector { get; private set; }

        public Transport(TransportInfo transportInfo, Action onFailure)
        {
            if (onFailure == null) throw new ArgumentNullException("onFailure");
            m_OnFailure = onFailure;
            m_TransportInfo = transportInfo;

            m_JailedTag = (m_TransportInfo.JailStrategy ?? JailStrategy.None).CreateTag();
            JailedSelector = JAILED_PROPERTY_NAME + " = \'" + m_JailedTag + "\'";
        }

        public bool Jailed
        {
            get { return m_JailedTag != null; }
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

        private Sonic.Jms.Session getSession(bool topic)
        {
            if (m_Connection == null)
            {
                lock (m_SyncRoot)
                {
                    if (m_Connection == null)
                    {

                        var factory = new Sonic.Jms.Cf.Impl.QueueConnectionFactory();
                        (factory as Sonic.Jms.Ext.ConnectionFactory).setConnectionURLs(m_TransportInfo.Broker);
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

        private Sonic.Jms.Session createDestination(string destination, out Destination sendDestination)
        {
            Sonic.Jms.Session result;
            if (destination.StartsWith("queue://", true, CultureInfo.InvariantCulture))
            {
                result = getSession(false);
                sendDestination = result.createQueue(destination.Substring(8));
            }
            else if (destination.StartsWith("topic://", true, CultureInfo.InvariantCulture))
            {
                result = getSession(true);
                sendDestination = result.createTopic(destination.Substring(8));
            }
            else
                throw new InvalidOperationException("Wrong destination name: " + destination + ". Should start with 'queue://' or 'topic://'");

            return result;
        }

        public Sonic.Jms.MessageConsumer CreateConsumer(Sonic.Jms.Session session, out Destination tempDestination)
        {
            tempDestination = session.CreateTempDestination();
            return createConsumer(tempDestination, session);
        }

        public Sonic.Jms.MessageConsumer CreateConsumer(string destination)
        {
            Destination sonicSource;
            var session = createDestination(destination, out sonicSource);

            MessageConsumer receiver = createConsumer(sonicSource, session);

            return receiver;
        }

        private MessageConsumer createConsumer(Destination sonicSource, Sonic.Jms.Session session)
        {
            MessageConsumer receiver;
            if (Jailed)
                receiver = session.createConsumer(sonicSource, JailedSelector);
            else
                receiver = session.createConsumer(sonicSource);
            return receiver;
        }

        public Sonic.Jms.MessageProducer CreateProducer(out Sonic.Jms.Session session)
        {
            session = getSession(false);
            return session.createProducer(null);
        }

        //TODO[MT]: out parameter should be removed!!!
        public Sonic.Jms.MessageProducer CreateProducer(string destination, out Sonic.Jms.Session session)
        {
            Destination sonicDestination;
            session = createDestination(destination, out sonicDestination);
            return session.createProducer(sonicDestination);
        }

        public TMessage JailMessage<TMessage>(TMessage message)
            where TMessage : Sonic.Jms.Message
        {
            if (Jailed)
                message.setStringProperty(JAILED_PROPERTY_NAME, m_JailedTag);
            return message;
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
