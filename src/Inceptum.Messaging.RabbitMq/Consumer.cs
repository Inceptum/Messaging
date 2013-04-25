using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Runtime.CompilerServices;
using System.Threading;
using Inceptum.Messaging.Transports;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Util;

namespace Inceptum.Messaging.RabbitMq
{
   

    internal class ProcessingGroup : DefaultBasicConsumer,IDisposable
    {
        private readonly IConnection m_Connection;
        private readonly IModel m_Model;

        public ProcessingGroup(IConnection connection)
        {
            m_Connection = connection;
            m_Model = m_Connection.CreateModel();
        }

        readonly Dictionary<string, Consumer> m_Consumers = new Dictionary<string, Consumer>();

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


    public class Consumer : DefaultBasicConsumer,IDisposable
    {
        private readonly IModel m_Model;
        private readonly Dictionary<string, Action<BinaryMessage>> m_Callbacks = new Dictionary<string, Action<BinaryMessage>>();
        private readonly AutoResetEvent m_CallBackAdded = new AutoResetEvent(false);
        private readonly ManualResetEvent m_Stop = new ManualResetEvent(false);

        public Consumer(IModel model)
        {
            m_Model = model;
        }

        public void AddCallback(Action<BinaryMessage> callback, string messageType)
        {
            if (callback == null) throw new ArgumentNullException("callback");
            if (messageType == null) throw new ArgumentNullException("messageType");
            lock (m_Callbacks)
            {
                //TODO: handle case where susbceiption for all message types is requested for shared destination
                if (messageType == null && m_Callbacks.Any())
                    throw new InvalidOperationException();
                if (m_Callbacks.ContainsKey(messageType))
                    throw new InvalidOperationException();
                m_Callbacks[messageType] = callback;
                m_CallBackAdded.Set();
            }
        }

        public bool RemoveCallback(string messageType)
        {
            lock (m_Callbacks)
            {

                if (!m_Callbacks.Remove(messageType))
                    throw new InvalidOperationException("Unsibscribe from not subscribed message type");
                if (m_Callbacks.Any())
                    return true;
            }
            m_Stop.Set();
            m_Model.BasicCancel(ConsumerTag);
            return false;
        }


        public override void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange,
                                                string routingKey,
                                                IBasicProperties properties, byte[] body)
        {
            while (true)
            {
                Action<BinaryMessage> callback;
                lock (m_Callbacks)
                {
                    m_Callbacks.TryGetValue(properties.Type, out callback);
                }
                if (callback != null)
                {
                    try
                    {
                        callback(new BinaryMessage {Bytes = body, Type = properties.Type});
                        m_Model.BasicAck(deliveryTag, false);
                    }
                    catch (Exception e)
                    {
                        //TODO:log
                    }
                    return;
                }
                if (WaitHandle.WaitAny(new WaitHandle[] {m_CallBackAdded, m_Stop}) == 1)
                {
                    //subscription is canceling, returning message of unknown type to queue
                    m_Model.BasicNack(deliveryTag, false,true);
                    break;
                }
            } 
        }

        public void Dispose()
        {
            m_Stop.Set();
            m_Model.BasicCancel(ConsumerTag);
        }
    }
}