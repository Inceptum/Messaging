using System;
using Inceptum.Messaging.Transports;

namespace Inceptum.Messaging.Sonic
{
    internal interface IProcessingGroup : IDisposable
    {
        IDisposable Subscribe(string destination, Action<BinaryMessage> callback, string messageType);
        void Send(string destination, BinaryMessage message, int ttl);
        RequestHandle SendRequest(string destination, BinaryMessage message, Action<BinaryMessage> callback);
        IDisposable RegisterHandler(string destination, Func<BinaryMessage, BinaryMessage> handler, string messageType);
    }
}