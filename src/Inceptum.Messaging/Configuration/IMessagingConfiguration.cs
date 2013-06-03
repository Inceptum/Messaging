using System.Collections.Generic;
using Inceptum.Messaging.Contract;

namespace Inceptum.Messaging.Configuration
{
    public interface IMessagingConfiguration
    {
        Dictionary<string, TransportInfo> GetTransports();

        bool HasEndpoint(string name);
        Endpoint GetEndpoint(string name);
    }
}