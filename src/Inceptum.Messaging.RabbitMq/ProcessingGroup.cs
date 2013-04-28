using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using Inceptum.Messaging.Transports;
using RabbitMQ.Client;

namespace Inceptum.Messaging.RabbitMq
{
    internal class ProcessingGroup : DefaultBasicConsumer,IProcessingGroup
    {
        private readonly IConnection m_Connection;
        private readonly IModel m_Model;

        public ProcessingGroup(IConnection connection)
        {
            m_Connection = connection;
            m_Model = m_Connection.CreateModel();
        }

        readonly Dictionary<string, Consumer> m_Consumers = new Dictionary<string, Consumer>();

        public void Send(string destination, BinaryMessage message, int ttl)
        {
            //TODO: model is not thread safe
            var properties = m_Model.CreateBasicProperties();
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
                Consumer consumer;
                if (!m_Consumers.TryGetValue(destination, out consumer))
                {
                    consumer = new Consumer(m_Model);
                    m_Consumers[destination] = consumer;
                }

                consumer.AddCallback(callback, messageType);
                lock (m_Model)
                {
                    m_Model.BasicConsume(destination, false, consumer);
                } 
                
                return Disposable.Create(() =>
                    {
                        lock (m_Consumers)
                        {
                            if (!consumer.RemoveCallback(messageType))
                                m_Consumers.Remove(destination);
                        }
                    });
            
            }
        }


        public void Dispose()
        {
            lock (m_Consumers)
            {
                foreach (var consumer in m_Consumers.Values)
                {
                    consumer.Dispose();
                }
            }

            m_Model.Dispose();
            m_Connection.Dispose();
        }
    }
}