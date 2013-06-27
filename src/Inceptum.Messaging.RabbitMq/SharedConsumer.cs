using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Inceptum.Messaging.Transports;
using RabbitMQ.Client;

namespace Inceptum.Messaging.RabbitMq
{
    public class SharedConsumer : DefaultBasicConsumer,IDisposable
    {
        private readonly Dictionary<string, Action<IBasicProperties, byte[]>> m_Callbacks = new Dictionary<string, Action<IBasicProperties, byte[]>>();
        private readonly AutoResetEvent m_CallBackAdded = new AutoResetEvent(false);
        private readonly ManualResetEvent m_Stop = new ManualResetEvent(false);

        public SharedConsumer(IModel model) : base(model)
        {
        }

        public void AddCallback(Action<IBasicProperties, byte[]> callback, string messageType)
        {
            if (callback == null) throw new ArgumentNullException("callback");
            if (string.IsNullOrEmpty(messageType)) throw new ArgumentNullException("messageType");
            lock (m_Callbacks)
            {
                if (m_Callbacks.ContainsKey(messageType))
                    throw new InvalidOperationException("Attempt to subscribe for same destination twice.");
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
            stop();
            return false;
        }


        public override void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange,
                                                string routingKey,
                                                IBasicProperties properties, byte[] body)
        {
            bool waitForCallback = true;
            while (true)
            {
                Action<IBasicProperties, byte[]> callback;
                lock (m_Callbacks)
                {
                    if(waitForCallback)
                        m_CallBackAdded.Reset();
                    m_Callbacks.TryGetValue(properties.Type, out callback);
                }
                if (callback != null)
                {
                    try
                    {
                        callback(properties, body);
                        Model.BasicAck(deliveryTag, false);
                    }
                    catch (Exception e)
                    {
                        //TODO:log
                    }
                    return;
                }


                if (!waitForCallback || WaitHandle.WaitAny(new WaitHandle[] { m_CallBackAdded, m_Stop }) == 1)
                {
                    //The registered callback is not the one we are waiting for. (Nack message for later redelivery and free processing thread for it to process other callback registration)
                    //or subscription is canceling, returning message of unknown type to queue
                    Model.BasicNack(deliveryTag, false,true);
                    break;
                }
                    
                waitForCallback = false;
            } 
        }

        public void Dispose()
        {
            stop();
        }

        private void stop()
        {
            m_Stop.Set();
            lock (Model)
            {
                if (Model.IsOpen)
                    Model.BasicCancel(ConsumerTag);
            }
        }
    }
}