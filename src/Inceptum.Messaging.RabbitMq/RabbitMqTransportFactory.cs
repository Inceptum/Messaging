using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Inceptum.Messaging.Transports;

namespace Inceptum.Messaging.RabbitMq
{
    public class RabbitMqTransportFactory : ITransportFactory
    {
        public RabbitMqTransportFactory():this(true)
        {
            
        }
        internal RabbitMqTransportFactory(bool shuffleBrokers)
        {
            m_ShuffleBrokers = shuffleBrokers;
        }

        private readonly bool m_ShuffleBrokers;

        public string Name
        {
            get { return "RabbitMq"; }
        }

        public ITransport Create(TransportInfo transportInfo, Action onFailure)
        {
            var brokers =transportInfo.Broker.Split(',').Select(b => b.Trim()).ToArray();
            return new RabbitMqTransport(brokers, transportInfo.Login, transportInfo.Password, m_ShuffleBrokers);
        }
    }
}