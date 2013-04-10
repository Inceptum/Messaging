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
        private readonly IConnection m_Connection;
        private readonly IModel m_Channel;

        public Transport(string host, string username, string password)
        {
            if (host == null) throw new ArgumentNullException("host");

            m_Factory = new ConnectionFactory { HostName = host,UserName = username,Password = password};
            m_Connection = m_Factory.CreateConnection();
            m_Channel = m_Connection.CreateModel();
        }

        public void Send(string destination, BinaryMessage message, int ttl, string processingGroup = null)
        {
            m_Channel.BasicPublish(destination, "", null, message.Bytes);
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
            var consumer = new QueueingBasicConsumer(m_Channel);
            //String consumerTag = m_Channel.BasicConsume(destination, false, "consumer",new Dictionary<string,string>(),consumer);
            String consumerTag = m_Channel.BasicConsume(destination, false, consumer);
            while (true)
            {
                try
                {
                    var e = (RabbitMQ.Client.Events.BasicDeliverEventArgs)consumer.Queue.Dequeue();
                    var props = e.BasicProperties;
                    byte[] body = e.Body;
                    try
                    {
                        callback(new BinaryMessage{Bytes = body,Type = props.Type});
                        m_Channel.BasicAck(e.DeliveryTag, false);
                    }
                    catch (Exception exception)
                    {
                       //TODO:log
                        //m_Channel.BasicNack(e.DeliveryTag, false, true);
                    }
                }
                catch (OperationInterruptedException ex)
                {
                    // The consumer was removed, either through
                    // channel or connection closure, or through the
                    // action of IModel.BasicCancel().
                    break;
                }
            }
            throw new NotImplementedException();
        }



        public void Dispose()
        {
            m_Channel.Dispose();
            m_Connection.Dispose();
        }
    }

    public class RabbitMqTransportFactory : ITransportFactory
    {
        public string Name
        {
            get { return "RabbitMq"; }
        }

        public ITransport Create(TransportInfo transportInfo, Action onFailure)
        {
            return new Transport(transportInfo.Broker,transportInfo.Login,transportInfo.Password);
        }
    }
}