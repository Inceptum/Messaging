using System;
using System.Collections.Generic;
using System.Threading;
using Inceptum.Messaging.Transports;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Inceptum.Messaging.RabbitMq
{
    internal class Transport : ITransport
    {
        private readonly ConnectionFactory m_Factory;
        private readonly List<ProcessingGroup> m_ProcessingGroups = new List<ProcessingGroup>();
        ManualResetEvent m_IsDisposed=new ManualResetEvent(false);
        public Transport(string host, string username, string password)
        {
            if (host == null) throw new ArgumentNullException("host");
            m_Factory = new ConnectionFactory { HostName = host,UserName = username,Password = password};
        }
 
        public void Dispose()
        {
            m_IsDisposed.Set();
            ProcessingGroup[] processingGroups;
            lock (m_ProcessingGroups)
            {
                processingGroups = m_ProcessingGroups.ToArray();
            }
            foreach (var processingGroup in processingGroups)
            {
                processingGroup.Dispose();
            }

        }

        public IProcessingGroup CreateProcessingGroup(string name, Action onFailure)
        {
            if(m_IsDisposed.WaitOne(0))
                throw new ObjectDisposedException("Transport is disposed");

            var connection = m_Factory.CreateConnection();
            var processingGroup = new ProcessingGroup(connection);
            connection.ConnectionShutdown += (connection1, reason) =>
                {
                    lock (m_ProcessingGroups)
                    {
                        m_ProcessingGroups.Remove(processingGroup);
                    }
                    

                    if ((reason.Initiator!=ShutdownInitiator.Application || reason.ReplyCode!=200) && onFailure != null)
                        onFailure();
                    //TODO: log
                };

            lock (m_ProcessingGroups)
            {
                m_ProcessingGroups.Add(processingGroup);
            }
            return processingGroup;
        }
    }
}