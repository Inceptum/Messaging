using System;
using System.Collections.Generic;
using System.Globalization;
using Sonic.Jms.Ext;
using Connection = Sonic.Jms.Ext.Connection;
using QueueConnection = Sonic.Jms.QueueConnection;


namespace Inceptum.Messaging.Transports
{
    internal class Transport : IDisposable
    {
        private readonly object m_SyncRoot = new object();
        private readonly TransportInfo m_TransportInfo;
        private volatile bool m_IsDisposed;
        private Action m_OnFailure;
        private readonly Dictionary<Tuple<string,bool>,IProcessingGroup> m_ProcessingGroups=new Dictionary<Tuple<string, bool>, IProcessingGroup>();



        
        private readonly string m_JailedTag;
        private readonly QueueConnection m_Connection;
        internal string JailedSelector { get; private set; }

        public Transport(TransportInfo transportInfo, Action onFailure)
        {
            if (onFailure == null) throw new ArgumentNullException("onFailure");
            m_OnFailure = onFailure;
            m_TransportInfo = transportInfo;

            m_JailedTag = (m_TransportInfo.JailStrategy ?? JailStrategy.None).CreateTag();
            JailedSelector = MessagingEngine.JAILED_PROPERTY_NAME + " = \'" + m_JailedTag + "\'";

            var factory = new Sonic.Jms.Cf.Impl.QueueConnectionFactory();
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
         
 
        
        
        private bool isQueue(string destination)
        {
            if (destination.StartsWith("queue://", true, CultureInfo.InvariantCulture))
            {
                return true;
            }
            
            if (destination.StartsWith("topic://", true, CultureInfo.InvariantCulture))
            {
                return false;
            }
            
            throw new InvalidOperationException("Wrong destination name: " + destination + ". Should start with 'queue://' or 'topic://'");
        }


        public void Send(string destination, BinaryMessage message, string processingGroup = null)
        {
            var group = getProcessingGroup(destination, processingGroup);
            group.Send(destination, message);
        }

        public RequestHandle SendRequest(string destination, BinaryMessage message, Action<BinaryMessage> callback, string processingGroup = null)
        {
            var group = getProcessingGroup(destination, processingGroup);
            return group.SendRequest(destination, message, callback);
        }

        public IDisposable RegisterHandler(string destination, Func<BinaryMessage, BinaryMessage> handler, string messageType, string processingGroup = null)
        { 
            var group = getProcessingGroup(destination, processingGroup);
            return group.RegisterHandler(destination, handler,messageType);
        }

        public IDisposable Subscribe(string destination, Action<BinaryMessage> callback, string messageType, string processingGroup = null)
        {
            var group = getProcessingGroup(destination, processingGroup);
            return group.Subscribe(destination, callback, messageType);
        }

        private IProcessingGroup getProcessingGroup(string destination, string processingGroup)
        {
            var groupName = processingGroup ?? destination;
            var isQueueGroup = isQueue(destination);
            var groupKey = Tuple.Create(groupName, isQueueGroup);
            IProcessingGroup group;
            lock (m_ProcessingGroups)
            {
                if (!m_ProcessingGroups.TryGetValue(groupKey, out group))
                {
                    group = ProcessingGroup.Create(m_Connection, isQueueGroup,m_JailedTag);
                    m_ProcessingGroups.Add(groupKey, group);
                }
            }
            return group;
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
