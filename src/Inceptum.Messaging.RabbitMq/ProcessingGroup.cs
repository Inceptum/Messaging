using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using Inceptum.Messaging.Transports;
using RabbitMQ.Client;

namespace Inceptum.Messaging.RabbitMq
{
    internal class ProcessingGroup : IProcessingGroup
    {
        private readonly IConnection m_Connection;
        private readonly IModel m_Model;

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
            //TODO: model is not thread safe
            var properties = m_Model.CreateBasicProperties();
            if(message.Type!=null)
                properties.Type = message.Type;
            m_Model.BasicPublish(destination, "", properties, message.Bytes);
        }

        public RequestHandle SendRequest(string destination, BinaryMessage message, Action<BinaryMessage> callback)
        {
            throw new NotImplementedException();
        }

        public IDisposable RegisterHandler(string destination, Func<BinaryMessage, BinaryMessage> handler, string messageType)
        {
            throw new NotImplementedException();
        }

        public IDisposable Subscribe(string destination, Action<BinaryMessage> callback, string messageType)
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

        private IDisposable subscribeShared(string destination, Action<BinaryMessage> callback, string messageType,
                                            SharedConsumer consumer)
        {
            if (consumer == null)
            {
                consumer = new SharedConsumer(m_Model);
                m_Consumers[destination] = consumer;
            }

            consumer.AddCallback(callback, messageType);


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

        private IDisposable subscribeNonShared(string destination, Action<BinaryMessage> callback)
        {
            var consumer = new Consumer(m_Model, callback);
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

            m_Model.Dispose();
            m_Connection.Dispose();
        }
    }
}