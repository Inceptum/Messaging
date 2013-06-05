using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using Inceptum.Messaging.Transports;
using Sonic.Jms;

namespace Inceptum.Messaging.Sonic
{
    internal class ProcessingGroupWrapper:IProcessingGroup
    {
        private IProcessingGroup m_Instance;
        private static QueueConnection m_Connection;
        private static string m_JailedTag;
        private bool m_IsQueueGroup;
        

        public ProcessingGroupWrapper(QueueConnection connection, string jailedTag)
        {
            
            m_JailedTag = jailedTag;
            m_Connection = connection;
        }
 
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Dispose()
        {
            if(m_Instance!=null)
                m_Instance.Dispose();
        }

        public IDisposable Subscribe(string destination, Action<BinaryMessage> callback, string messageType)
        {
            ensureProcessingGroupIsCreated(destination);
            return m_Instance.Subscribe(destination, callback, messageType);
        }

        public void Send(string destination, BinaryMessage message, int ttl)
        {
            ensureProcessingGroupIsCreated(destination);
            m_Instance.Send(destination, message, ttl);
        }

        public RequestHandle SendRequest(string destination, BinaryMessage message, Action<BinaryMessage> callback)
        {
            ensureProcessingGroupIsCreated(destination);
            return m_Instance.SendRequest(destination, message, callback);
        }

        public IDisposable RegisterHandler(string destination, Func<BinaryMessage, BinaryMessage> handler, string messageType)
        {
            ensureProcessingGroupIsCreated(destination);
            return m_Instance.RegisterHandler(destination, handler, messageType);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void ensureProcessingGroupIsCreated(string destination)
        {
            if (m_Instance != null)
            {
                if(m_IsQueueGroup != isQueue(destination))
                    throw new InvalidOperationException(string.Format("Can not process  {0} {1} as it is already used for {2} processing. Sonic does not support processing topic and queue in same thread", m_IsQueueGroup ? "Topic" : "Queue", destination,  m_IsQueueGroup ? "Queue" : "Topic"));
                    //throw new InvalidOperationException(string.Format("Can not process  {0} {1} in processing group {2} as it is already used for {3} processing. Sonic does not support processing topic and queue in same thread", m_IsQueueGroup ? "Topic" : "Queue", destination, m_Name, m_IsQueueGroup ? "Queue" : "Topic"));
                return;
            }
            m_IsQueueGroup = isQueue(destination);
            if (m_IsQueueGroup)
                m_Instance=new QueueProcessingGroup(m_Connection, m_JailedTag);
            else
                m_Instance= new TopicProcessingGroup(m_Connection, m_JailedTag);
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
    }
}