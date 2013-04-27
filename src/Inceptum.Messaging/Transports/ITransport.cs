﻿using System;

namespace Inceptum.Messaging.Transports
{
    public interface ITransport:IDisposable
    {
        void Send(string destination, BinaryMessage message, int ttl, string processingGroup = null);
        RequestHandle SendRequest(string destination, BinaryMessage message, Action<BinaryMessage> callback, string processingGroup = null);
        IDisposable RegisterHandler(string destination, Func<BinaryMessage, BinaryMessage> handler, string messageType, string processingGroup = null);
        IDisposable Subscribe(string destination, Action<BinaryMessage> callback, string messageType, string processingGroup = null);
    }
}