using Inceptum.Messaging.Contract;

namespace Inceptum.Cqrs.Configuration
{
    public interface IEndpointResolver
    {
        Endpoint Resolve(string endpoint);
    }
}