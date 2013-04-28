using System;
using Inceptum.Messaging.Transports;
using RabbitMQ.Client;

namespace Inceptum.Messaging.RabbitMq
{
    class Consumer:DefaultBasicConsumer,IDisposable
    {
        private readonly Action<BinaryMessage> m_Callback;

        public Consumer(IModel model,Action<BinaryMessage> callback) : base(model)
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
                m_Callback(new BinaryMessage {Bytes = body, Type = properties.Type});
                Model.BasicAck(deliveryTag, false);
            }
            catch (Exception e)
            {
                //TODO:log
            }
        }

        public void Dispose()
        {
            Model.BasicCancel(ConsumerTag);
        }
    }
}