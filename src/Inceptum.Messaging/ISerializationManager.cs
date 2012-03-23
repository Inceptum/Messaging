using System;
using Sonic.Jms;

namespace Inceptum.Messaging
{
    public interface ISerializationManager
    {
        byte[] Serialize<TMessage>(TMessage message);
        TMessage Deserialize<TMessage>(byte[] message);
        void RegisterSerializer(Type targetType, object serializer);
        void RegisterSerializerFactory(ISerializerFactory serializerFactory);
    }
}
