using System;
using System.Collections.Generic;
using Inceptum.Messaging.Transports;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Inceptum.Messaging.RabbitMq
{
    internal class Transport : ITransport
    {
        private readonly ConnectionFactory m_Factory;
        private readonly IConnection m_SendConnection;
        private readonly IModel m_SendChannel;
        private readonly Dictionary<string, ProcessingGroup> m_ProcessingGroups = new Dictionary<string, ProcessingGroup>();



        public Transport(string host, string username, string password)
        {
            if (host == null) throw new ArgumentNullException("host");

            m_Factory = new ConnectionFactory { HostName = host,UserName = username,Password = password};
            //TODO: may it makes sense to send via processing group
            m_SendConnection = m_Factory.CreateConnection();
            m_SendChannel = m_SendConnection.CreateModel();
        }

        public void Send(string destination, BinaryMessage message, int ttl, string processingGroup = null)
        {
            //TODO: model is not thread safe
            var properties = m_SendChannel.CreateBasicProperties();
            properties.Type = message.Type;
            m_SendChannel.BasicPublish(destination, "", properties, message.Bytes);
        }

        public RequestHandle SendRequest(string destination, BinaryMessage message, Action<BinaryMessage> callback,
                                         string processingGroup = null)
        {
            throw new NotImplementedException();
        }

        public IDisposable RegisterHandler(string destination, Func<BinaryMessage, BinaryMessage> handler,
                                           string messageType, string processingGroup = null)
        {
            throw new NotImplementedException();
        }

        public IDisposable Subscribe(string destination, Action<BinaryMessage> callback, string messageType,
                                     string processingGroup = null)
        {
            var group = getProcessingGroup(processingGroup ?? destination);
            return group.Subscribe(destination, callback, messageType);
        }



        private ProcessingGroup getProcessingGroup(string name)
        {
            ProcessingGroup group;
            lock (m_ProcessingGroups)
            {
                if (!m_ProcessingGroups.TryGetValue(name, out group))
                {
                    group = new ProcessingGroup(m_Factory.CreateConnection());
                    m_ProcessingGroups.Add(name, group);
                }
            }
            return group;
        }



        public void Dispose()
        {
            lock (m_ProcessingGroups)
            {
                foreach (var processingGroup in m_ProcessingGroups.Values)
                {
                    processingGroup.Dispose();
                }
            }
            m_SendChannel.Dispose();
            m_SendConnection.Dispose();
        }
    }
}