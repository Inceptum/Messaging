using ProtoBuf;

namespace Inceptum.Messaging.Serialization.ProtobufNet
{
    public class ProtobufSerializerFactory : ISerializerFactory
    {
        public string SerializationFormat
        {
            get { return "protobuf"; }
        }

        public IMessageSerializer<TMessage> Create<TMessage>()
        {
            //TODO: may affect performance
            if (typeof(TMessage).GetCustomAttributes(typeof(ProtoContractAttribute), false).Length > 0)
                return new ProtobufSerializer<TMessage>();
            return null;
        }
    }
}