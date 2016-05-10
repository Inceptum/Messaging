using System.Collections.Generic;
using Inceptum.Messaging.Contract;

namespace Inceptum.Messaging.Configuration
{
    public interface IMessagingConfiguration
    {
        IDictionary<string, TransportInfo> GetTransports();
        IDictionary<string, Endpoint> GetEndpoints();
        IDictionary<string, ProcessingGroupInfo> GetProcessingGroups();
    }
}