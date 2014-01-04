using System;
using System.Collections.Generic;
using System.Threading;
using Inceptum.Messaging.Transports;

namespace Inceptum.Messaging.RabbitMq
{
    public class RabbitMqTransportFactory : ITransportFactory
    {
        public string Name
        {
            get { return "RabbitMq"; }
        }

        public ITransport Create(TransportInfo transportInfo, Action onFailure)
        {
            return new RabbitMqTransport(transportInfo.Broker,transportInfo.Login,transportInfo.Password);
        }
    }
}