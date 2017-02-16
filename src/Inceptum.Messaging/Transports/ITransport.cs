﻿using System;
using Inceptum.Messaging.Contract;

namespace Inceptum.Messaging.Transports
{

    public interface ITransport : IDisposable
    {
        IMessagingSession CreateSession(Action onFailure);
        bool VerifyDestination(Destination destination, EndpointUsage usage, bool configureIfRequired,out string error);
    }

    public interface IMessagingSession : IDisposable
    {
        void Send(string destination, BinaryMessage message, int ttl);
        void Send(string destination, BinaryMessage message, int ttl, ReplyTo replyTo);
        RequestHandle SendRequest(string destination, BinaryMessage message, Action<BinaryMessage> callback);
        IDisposable RegisterHandler(string destination, Func<BinaryMessage, BinaryMessage> handler, string messageType);
        IDisposable Subscribe(string destination, Action<BinaryMessage, Action<bool>> callback, string messageType);
        Destination CreateTemporaryDestination();
    }
}