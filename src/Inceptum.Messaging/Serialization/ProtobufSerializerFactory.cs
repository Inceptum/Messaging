using System.Linq;
using ProtoBuf;

namespace Inceptum.Messaging.Serialization
{
    public class ProtobufSerializerFactory:ISerializerFactory
    {
        public IMessageSerializer<TMessage> Create<TMessage>()
        {
            //TODO: may affect performance
            if (
                typeof(TMessage).GetCustomAttributes(typeof(ProtoContractAttribute),false).Any()
                )
                return new ProtobufSerializer<TMessage>() ;
            return null;
        }
    }
}
