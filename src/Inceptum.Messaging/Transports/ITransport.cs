using System;
using Inceptum.Messaging.Contract;

namespace Inceptum.Messaging.Transports
{

    public interface ITransport : IDisposable
    {
        IProcessingGroup CreateProcessingGroup(Action onFailure);
        void EnsureDestination(Destination destination);
    }

    public interface IProcessingGroup : IDisposable
    {
        void Send(string destination, BinaryMessage message, int ttl);
        RequestHandle SendRequest(string destination, BinaryMessage message, Action<BinaryMessage> callback);
        IDisposable RegisterHandler(string destination, Func<BinaryMessage, BinaryMessage> handler, string messageType);
        IDisposable Subscribe(string destination, Action<BinaryMessage, Action<bool>> callback, string messageType);
        Destination CreateTemporaryDestination();
    }
}