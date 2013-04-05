using System;
using Inceptum.Messaging.Transports;

namespace Inceptum.Messaging
{
    public interface ITransportFactory
    {
        string Name { get; }
        ITransport Create(TransportInfo transportInfo, Action onFailure);
    }
}