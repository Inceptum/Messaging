using System;
using Inceptum.Messaging.Transports;
using RabbitMQ.Client;

namespace Inceptum.Messaging.RabbitMq
{
    class Consumer:DefaultBasicConsumer,IDisposable
    {
        private readonly Action<IBasicProperties, byte[]> m_Callback;

        public Consumer(IModel model, Action<IBasicProperties, byte[]> callback)
            : base(model)
        {
            if (callback == null) throw new ArgumentNullException("callback");
            m_Callback = callback;
        }

        public override void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange,
                                                string routingKey,
                                                IBasicProperties properties, byte[] body)
        {
            try
            {
                m_Callback(properties,body);
                Model.BasicAck(deliveryTag, false);
            }
            catch (Exception e)
            {
                //TODO:log
            }
        }

        public void Dispose()
        {
            lock (Model)
                Model.BasicCancel(ConsumerTag);
        }
    }
}