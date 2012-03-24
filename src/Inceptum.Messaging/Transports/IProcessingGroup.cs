using System;
using Sonic.Jms;

namespace Inceptum.Messaging.Transports
{
    internal interface IProcessingGroup : IDisposable
    {
        IDisposable Subscribe(string destination, Action<Message> callback);
        void Send(string destination, byte[] message);
        IDisposable SendRequest(string destination, byte[] message, Action<Message> callback);
        IDisposable RegisterHandler(string destination, Func<Message, byte[]> handler);
    }
}