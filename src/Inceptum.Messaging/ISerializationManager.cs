using System;
using Sonic.Jms;

namespace Inceptum.Messaging
{
    public interface ISerializationManager
    {
        Message Serialize<TMessage>(TMessage message, Session sendSession);
        TMessage Deserialize<TMessage>(Message message);
        void RegisterSerializer(Type targetType, object serializer);
        void RegisterSerializerFactory(ISerializerFactory serializerFactory);
    }
}
