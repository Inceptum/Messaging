using System;
using Inceptum.Messaging.Contract;

namespace Inceptum.Cqrs
{
    internal class MessageRouting
    {
        public MessageRouting(Type[] types, Endpoint? publishEndpoint, Endpoint? subscribeEndpoint)
        {
            Types = types;
            SubscribeEndpoint = subscribeEndpoint;
            PublishEndpoint = publishEndpoint;
        }

        public Type[] Types { get; private set; }
        public Endpoint? PublishEndpoint { get; private set; }
        public Endpoint? SubscribeEndpoint { get; private set; }
    }
}