using System;
using System.Globalization;
using Inceptum.Messaging.Contract;
using Inceptum.Messaging.Transports;
using Sonic.Jms;
using Destination = Inceptum.Messaging.Contract.Destination;

namespace Inceptum.Messaging.Sonic
{
    internal class SonicSessionWrapper : IMessagingSession
    {
        private readonly QueueConnection m_Connection;
        private readonly string m_JailedTag;
        private readonly MessageFormat m_MessageFormat;
        private volatile IMessagingSession m_Instance;
        private volatile bool m_IsQueueGroup;
        private readonly object m_SyncRoot = new object();

        public SonicSessionWrapper(QueueConnection connection, string jailedTag, MessageFormat messageFormat)
        {
            m_JailedTag = jailedTag;
            m_MessageFormat = messageFormat;
            m_Connection = connection;
        }

        public void Dispose()
        {
            if (m_Instance != null)
            {
                lock (m_SyncRoot)
                {
                    if (m_Instance != null)
                    {
                        m_Instance.Dispose();
                        m_Instance = null;
                    }
                }
            }
        }

        public IDisposable Subscribe(string destination, Action<BinaryMessage, Action<bool>> callback, string messageType)
        {
            ensureSessionIsCreated(destination);
            return m_Instance.Subscribe(destination, callback, messageType);
        }

        public void Send(string destination, BinaryMessage message, int ttl)
        {
            ensureSessionIsCreated(destination);
            m_Instance.Send(destination, message, ttl);
        }

        public RequestHandle SendRequest(string destination, BinaryMessage message, Action<BinaryMessage> callback)
        {
            ensureSessionIsCreated(destination);
            return m_Instance.SendRequest(destination, message, callback);
        }

        public IDisposable RegisterHandler(string destination, Func<BinaryMessage, BinaryMessage> handler, string messageType)
        {
            ensureSessionIsCreated(destination);
            return m_Instance.RegisterHandler(destination, handler, messageType);
        }

        private void ensureSessionIsCreated(string destination)
        {
            if (m_Instance != null)
            {
                if (m_IsQueueGroup != isQueue(destination))
                    throw new InvalidOperationException(string.Format("Can not process {0} {1} as it is already used for {2} processing. Sonic does not support processing topic and queue in same thread", m_IsQueueGroup ? "Topic" : "Queue", destination, m_IsQueueGroup ? "Queue" : "Topic"));
                return;
            }

            lock (m_SyncRoot)
            {
                if (m_Instance == null)
                {
                    m_IsQueueGroup = isQueue(destination);
                    if (m_IsQueueGroup)
                        m_Instance = new QueueSonicSession(m_Connection, m_JailedTag, m_MessageFormat);
                    else
                        m_Instance = new TopicSonicSession(m_Connection, m_JailedTag, m_MessageFormat);
                }
            }
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

        public Destination CreateTemporaryDestination()
        {
            return m_Instance.CreateTemporaryDestination();
        }


        public void Send(string destination, BinaryMessage message, int ttl, ReplyTo replyTo)
        {
            ensureSessionIsCreated(destination);
            m_Instance.Send(destination, message, ttl, replyTo);
        }
    }
}