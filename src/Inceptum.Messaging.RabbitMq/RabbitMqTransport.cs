using System;
using System.Collections.Generic;
using System.Threading;
using Inceptum.Messaging.Contract;
using Inceptum.Messaging.Transports;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Inceptum.Messaging.RabbitMq
{
    internal class RabbitMqTransport : ITransport
    {
        private readonly ConnectionFactory m_Factory;
        private readonly List<RabbitMqSession> m_Sessions = new List<RabbitMqSession>();
        readonly ManualResetEvent m_IsDisposed=new ManualResetEvent(false);
        public RabbitMqTransport(string broker, string username, string password)
        {
            if (broker == null) throw new ArgumentNullException("broker");
            var f = new ConnectionFactory() { };

            Uri uri = null;
            f.UserName = username;
            f.Password = password;

            if (Uri.TryCreate(broker, UriKind.Absolute, out uri))
            {
                f.Uri = broker;
            }
            else
            {
                f.HostName = broker;
            }

            m_Factory = f;

 

        }
 
        public void Dispose()
        {
            m_IsDisposed.Set();
            RabbitMqSession[] sessions;
            lock (m_Sessions)
            {
                sessions = m_Sessions.ToArray();
            }
            foreach (var session in sessions)
            {
                session.Dispose();
            }

        }

        public IMessagingSession CreateSession(Action onFailure)
        {
            if(m_IsDisposed.WaitOne(0))
                throw new ObjectDisposedException("Transport is disposed");

            var connection = m_Factory.CreateConnection();
            var session = new RabbitMqSession(connection);
            connection.ConnectionShutdown += (connection1, reason) =>
                {
                    lock (m_Sessions)
                    {
                        m_Sessions.Remove(session);
                    }
                    

                    if ((reason.Initiator!=ShutdownInitiator.Application || reason.ReplyCode!=200) && onFailure != null)
                        onFailure();
                    //TODO: log
                };

            lock (m_Sessions)
            {
                m_Sessions.Add(session);
            }
            return session;
        }

        public bool VerifyDestination(Destination destination, EndpointUsage usage, bool configureIfRequired, out string error)
        {
            try
            {
                var publish = PublicationAddress.Parse(destination.Publish) ?? new PublicationAddress("topic", destination.Publish, ""); ;
                using (IConnection connection = m_Factory.CreateConnection())
                {
                    using (IModel channel = connection.CreateModel())
                    {
                        if ((usage & EndpointUsage.Publish) == EndpointUsage.Publish)
                        {
                            if (configureIfRequired)
                                channel.ExchangeDeclare(publish.ExchangeName, publish.ExchangeType, true);
                            else
                                channel.ExchangeDeclarePassive(publish.ExchangeName);

                        }

                        if ((usage & EndpointUsage.Subscribe) == EndpointUsage.Subscribe)
                        {
                            if (configureIfRequired)
                                channel.QueueDeclare(destination.Subscribe, true, false, false, null);
                            else
                                channel.QueueDeclarePassive(destination.Subscribe);

                            if (configureIfRequired)
                            {
                                channel.QueueBind(destination.Subscribe, publish.ExchangeName, publish.RoutingKey == "" ? "#" : publish.RoutingKey);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                if (!e.GetType().Namespace.StartsWith("RabbitMQ") || e.GetType().Assembly != typeof (OperationInterruptedException).Assembly)
                    throw;
                error = e.Message;
                return false;
            }
            error = null;
            return true;
        }
    }
}