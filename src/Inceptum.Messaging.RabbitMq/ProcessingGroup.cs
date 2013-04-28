using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reactive.Disposables;
using Inceptum.Messaging.Transports;
using RabbitMQ.Client;

namespace Inceptum.Messaging.RabbitMq
{
    internal class ProcessingGroup : IProcessingGroup
    {
        private readonly IConnection m_Connection;
        private readonly IModel m_Model;
        private readonly CompositeDisposable m_Subscriptions = new CompositeDisposable();


        public ProcessingGroup(IConnection connection)
        {
            m_Connection = connection;
            m_Model = m_Connection.CreateModel();
            connection.ConnectionShutdown += (connection1, reason) =>
                {
                    lock (m_Consumers)
                    {
                        foreach (IDisposable consumer in m_Consumers.Values)
                        {
                            consumer.Dispose();
                        }
                    }
                };
        }

        readonly Dictionary<string, DefaultBasicConsumer> m_Consumers = new Dictionary<string, DefaultBasicConsumer>();

        public void Send(string destination, BinaryMessage message, int ttl)
        {
            send(destination, message, properties =>
                {
                    if (ttl > 0) properties.Expiration = ttl.ToString(CultureInfo.InvariantCulture);
                });
        }

        private void send(string destination, BinaryMessage message, Action<IBasicProperties> tuneMessage = null)
        {
            var publicationAddress = PublicationAddress.Parse(destination) ?? new PublicationAddress("direct", destination, "");
            send(publicationAddress, message, tuneMessage);
        }
        private void send(PublicationAddress destination, BinaryMessage message, Action<IBasicProperties> tuneMessage = null)
        {
            var properties = m_Model.CreateBasicProperties();
            if (message.Type != null)
                properties.Type = message.Type;
            if (tuneMessage != null)
                tuneMessage(properties);
            lock(m_Model)
                m_Model.BasicPublish(destination, properties, message.Bytes);
        }

        public RequestHandle SendRequest(string destination, BinaryMessage message, Action<BinaryMessage> callback)
        {
            string queue;
            lock(m_Model)
                queue = m_Model.QueueDeclare().QueueName;
            var request = new RequestHandle(callback, () => { }, cb => Subscribe(queue, cb, null));
            m_Subscriptions.Add(request);
// ReSharper disable ImplicitlyCapturedClosure
            send(destination, message, p => p.ReplyTo = new PublicationAddress("direct", "", queue).ToString());
// ReSharper restore ImplicitlyCapturedClosure
            return request;
 
        }

        public IDisposable RegisterHandler(string destination, Func<BinaryMessage, BinaryMessage> handler, string messageType)
        {
           
            var subscription = subscribe(destination, (properties, bytes) =>
            {
                var correlationId = properties.CorrelationId;
                var responseBytes = handler(toBinaryMessage(properties, bytes));
                //If replyTo is not parsable we treat it as queue name and message is sent via default exchange  (http://www.rabbitmq.com/tutorials/amqp-concepts.html#exchange-default)
                var publicationAddress = PublicationAddress.Parse(properties.ReplyTo) ?? new PublicationAddress("direct", "", properties.ReplyTo);
                send(publicationAddress, responseBytes, p =>
                    {
                        if (correlationId != null)
                            p.CorrelationId = correlationId;
                    });
            }, messageType);

            return subscription;
        }

        public IDisposable Subscribe(string destination, Action<BinaryMessage> callback, string messageType)
        {
            return subscribe(destination, (properties, bytes) => callback(toBinaryMessage(properties, bytes)),messageType);
        }

        private BinaryMessage toBinaryMessage(IBasicProperties properties, byte[] bytes)
        {
            return new BinaryMessage {Bytes = bytes, Type = properties.Type};
        }

        private IDisposable subscribe(string destination, Action<IBasicProperties, byte[]> callback, string messageType)
        {

            lock (m_Consumers)
            {
                DefaultBasicConsumer basicConsumer;
                m_Consumers.TryGetValue(destination, out basicConsumer);
                if (messageType == null)
                {
                    if (basicConsumer is SharedConsumer)
                        throw new InvalidOperationException("Attempt to subscribe for shared destination without specifying message type. It should be a bug in MessagingEngine");
                    if (basicConsumer != null)
                        throw new InvalidOperationException("Attempt to subscribe for same destination twice.");
                    return subscribeNonShared(destination, callback);
                }

                if (basicConsumer is Consumer)
                    throw new InvalidOperationException("Attempt to subscribe for non shared destination with specific message type. It should be a bug in MessagingEngine");

                return subscribeShared(destination, callback, messageType, basicConsumer as SharedConsumer);
            }
        }
        
        private IDisposable subscribeShared(string destination, Action<IBasicProperties, byte[]> callback, string messageType, SharedConsumer consumer)
        {
            if (consumer == null)
            {
                consumer = new SharedConsumer(m_Model);
                m_Consumers[destination] = consumer;
            }

            consumer.AddCallback(callback, messageType);

            lock (m_Model)
                m_Model.BasicConsume(destination, false, consumer);
            return Disposable.Create(() =>
                {
                    lock (m_Consumers)
                    {
                        if (!consumer.RemoveCallback(messageType))
                            m_Consumers.Remove(destination);
                    }
                });
        }

        private IDisposable subscribeNonShared(string destination, Action<IBasicProperties, byte[]> callback)
        {
            var consumer = new Consumer(m_Model, callback);
            lock (m_Model)
                m_Model.BasicConsume(destination, false, consumer);
            m_Consumers[destination] = consumer;
            // ReSharper disable ImplicitlyCapturedClosure
            return Disposable.Create(() =>
                {
                    lock (m_Consumers)
                    {
                        consumer.Dispose();
                        m_Consumers.Remove(destination);
                    }
                });
            // ReSharper restore ImplicitlyCapturedClosure
        }


        public void Dispose()
        {
            lock (m_Consumers)
            {
                foreach (IDisposable consumer in m_Consumers.Values)
                {
                    consumer.Dispose();
                }
            }
            lock (m_Model)
                m_Model.Dispose();
            m_Connection.Dispose();
        }
    }
}