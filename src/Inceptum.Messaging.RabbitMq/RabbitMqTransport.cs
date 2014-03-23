using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Inceptum.Messaging.Contract;
using Inceptum.Messaging.Transports;
using NLog;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Inceptum.Messaging.RabbitMq
{

  

    internal class RabbitMqTransport : ITransport
    {
        private readonly ConnectionFactory[] m_Factories;
        private long m_FactoryCounter;
        private readonly List<RabbitMqSession> m_Sessions = new List<RabbitMqSession>();
        readonly ManualResetEvent m_IsDisposed=new ManualResetEvent(false);
        readonly Logger m_Logger = LogManager.GetCurrentClassLogger();

        public RabbitMqTransport(string broker, string username, string password)
        {
            if (broker == null) throw new ArgumentNullException("broker");
               
            var factories = broker.Split(',').Select(b => b.Trim()).Select(brokerName =>
            {

                var f = new ConnectionFactory();
                Uri uri = null;
                f.UserName = username;
                f.Password = password;

                if (Uri.TryCreate(brokerName, UriKind.Absolute, out uri))
                {
                    f.Uri = brokerName;
                }
                else
                {
                    f.HostName = brokerName;
                }
                return f;
            });

            m_Factories = factories.ToArray();

 

        }




        [MethodImpl(MethodImplOptions.Synchronized)]
        private IConnection createConnection()
        {
            Exception exception;
            var initial = m_FactoryCounter;
            do
            {
                try
                {
                    var connection = m_Factories[m_FactoryCounter].CreateConnection();
                    m_Logger.Info("Created rmq connection to {0}.", m_Factories[m_FactoryCounter].Endpoint.HostName);
                    return connection;
                }
                catch (Exception e)
                {
                    m_Logger.WarnException(string.Format("Failed to create rmq connection to {0}{1}: ", m_Factories[m_FactoryCounter].Endpoint.HostName, (m_FactoryCounter+1 != initial)?" (will try other known hosts)":""), e);
                    exception = e;
                }
                m_FactoryCounter = (m_FactoryCounter + 1) % m_Factories.Length;
            } while (m_FactoryCounter != initial);
            throw new TransportException("Failed to create rmq connection",exception);
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

            var connection = createConnection();
            var session = new RabbitMqSession(connection);
            connection.ConnectionShutdown += (connection1, reason) =>
                {
                    lock (m_Sessions)
                    {
                        m_Sessions.Remove(session);
                    }


                    if ((reason.Initiator != ShutdownInitiator.Application || reason.ReplyCode != 200) && onFailure != null)
                    {
                        m_Logger.Warn("Rmq session to {0} is broken. Reason: {1}", connection1.Endpoint.HostName, reason);
                        onFailure();
                    }
                    else
                    {
                        m_Logger.Debug("Rmq session to {0} is closed", connection1.Endpoint.HostName);
                    }
                };

            lock (m_Sessions)
            {
                m_Sessions.Add(session);
            }
            m_Logger.Debug("Rmq session to {0} is opened", connection.Endpoint.HostName);
            return session;
        }


        public bool VerifyDestination(Destination destination, EndpointUsage usage, bool configureIfRequired, out string error)
        {
            try
            {
                var publish = PublicationAddress.Parse(destination.Publish) ?? new PublicationAddress("topic", destination.Publish, ""); ;
                using (IConnection connection = createConnection())
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