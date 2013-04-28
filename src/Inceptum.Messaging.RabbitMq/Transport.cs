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
        private readonly List<ProcessingGroup> m_ProcessingGroups = new List<ProcessingGroup>();

        public Transport(string host, string username, string password)
        {
            if (host == null) throw new ArgumentNullException("host");
            m_Factory = new ConnectionFactory { HostName = host,UserName = username,Password = password};
        }
 
        public void Dispose()
        {
            lock (m_ProcessingGroups)
            {
                foreach (var processingGroup in m_ProcessingGroups.ToArray())
                {
                    processingGroup.Dispose();
                }
            }
        }

        public IProcessingGroup CreateProcessingGroup(string name, Action onFailure)
        {
            var connection = m_Factory.CreateConnection();
            var processingGroup = new ProcessingGroup(connection);
            connection.ConnectionShutdown += (connection1, reason) =>
                {
                    lock (m_ProcessingGroups)
                    {
                        m_ProcessingGroups.Remove(processingGroup);
                    }


                    if (reason.Cause!=null && onFailure != null)
                        onFailure();

                };

            lock (m_ProcessingGroups)
            {
                m_ProcessingGroups.Add(processingGroup);
            }
            return processingGroup;
        }
    }
}