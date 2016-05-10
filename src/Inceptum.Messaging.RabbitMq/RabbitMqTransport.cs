﻿using System;
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
        private readonly List<RabbitMqSession> m_Sessions = new List<RabbitMqSession>();
        readonly ManualResetEvent m_IsDisposed=new ManualResetEvent(false);
        readonly Logger m_Logger = LogManager.GetCurrentClassLogger();
        private readonly bool m_ShuffleBrokersOnSessionCreate;
        private static readonly Random m_Random = new Random((int)DateTime.Now.Ticks & 0x0000FFFF);

        internal long SessionsCount
        {
            get { return m_Sessions.Count; }
        }
        public RabbitMqTransport(string broker, string username, string password) : this(new[] {broker}, username, password)
        {
            
        }
        public RabbitMqTransport(string[] brokers, string username, string password, bool shuffleBrokersOnSessionCreate=true)
        {
            m_ShuffleBrokersOnSessionCreate = shuffleBrokersOnSessionCreate&& brokers.Length>1;
            if (brokers == null) throw new ArgumentNullException("brokers");
            if (brokers.Length == 0) throw new ArgumentException("brokers list is empty", "brokers");

            var factories = brokers.Select(brokerName =>
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
            Exception exception=null;
            var factories = m_Factories;
            if (m_ShuffleBrokersOnSessionCreate)
            {
                
                factories = factories.OrderBy(x => m_Random.Next()).ToArray();
            }

            for (int i = 0; i < factories.Length; i++)
            {
                try
                {
                    var connection = factories[i].CreateConnection();
                    m_Logger.Info("Created rmq connection to {0}.", factories[i].Endpoint.HostName);
                    return connection;
                }
                catch (Exception e)
                {
                    m_Logger.WarnException(string.Format("Failed to create rmq connection to {0}{1}: ", factories[i].Endpoint.HostName, (i + 1 != factories.Length) ? " (will try other known hosts)" : ""), e);
                    exception = e;
                }
            }
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

        public IMessagingSession CreateSession(Action onFailure, bool confirmedSending)
        {
            if(m_IsDisposed.WaitOne(0))
                throw new ObjectDisposedException("Transport is disposed");

            var connection = createConnection();
            var session = new RabbitMqSession(connection, confirmedSending, (rabbitMqSession, destination,exception) =>
            {
                lock (m_Sessions)
                {
                    m_Sessions.Remove(rabbitMqSession);
                    m_Logger.WarnException(string.Format("Failed to send message to destination '{0}' broker '{1}'. Treating session as broken. ", destination, connection.Endpoint.HostName), exception); 
                }
            });
            connection.ConnectionShutdown += (c, reason) =>
                {
                    lock (m_Sessions)
                    {
                        m_Sessions.Remove(session);
                    }
                    

                    if ((reason.Initiator != ShutdownInitiator.Application || reason.ReplyCode != 200) && onFailure != null)
                    {
                        m_Logger.Warn("Rmq session to {0} is broken. Reason: {1}", connection.Endpoint.HostName, reason);
                        onFailure();
                    }
                    else
                    {
                        m_Logger.Debug("Rmq session to {0} is closed", connection.Endpoint.HostName);
                    }
                    
                };

            lock (m_Sessions)
            {
                m_Sessions.Add(session);
            }
            m_Logger.Debug("Rmq session to {0} is opened", connection.Endpoint.HostName);
            return session;
        }


        public IMessagingSession CreateSession(Action onFailure)
        {
            return CreateSession(onFailure, false);
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

                        if (publish.ExchangeName == "" && publish.ExchangeType.ToLower() == "direct")
                        {
                            //default exchange should not be verified since it always exists and publication to it is always possible
                        }
                        else
                        {
                            if (configureIfRequired)
                                channel.ExchangeDeclare(publish.ExchangeName, publish.ExchangeType, true);
                            else
                                channel.ExchangeDeclarePassive(publish.ExchangeName);
                        }


                        //temporary queue should not be verified since it is not supported by rmq client
                        if((usage & EndpointUsage.Subscribe) == EndpointUsage.Subscribe && !destination.Subscribe.ToLower().StartsWith("amq."))
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