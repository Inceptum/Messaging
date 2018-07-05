using System.IO;
using System.Linq;
using ProtoBuf;

namespace Inceptum.Messaging.Serialization.ProtobufNet
{
    internal class ProtobufSerializer<TMessage> : IMessageSerializer<TMessage>, IMessageTypeStringProvider
    {
        public byte[] Serialize(TMessage message)
        {
            var s = new MemoryStream();
            Serializer.Serialize(s, message);
            return s.ToArray();

        }

        public TMessage Deserialize(byte[] message)
        {
            var memStream = new MemoryStream();
            memStream.Write(message, 0, message.Length);
            memStream.Seek(0, SeekOrigin.Begin);
            return Serializer.Deserialize<TMessage>(memStream);
        }

        public string GetMessageTypeString()
        {
            var typeName = typeof(TMessage).GetCustomAttributes(false)
                .Select(attribute => attribute as ProtoContractAttribute)
                .Where(attribute => attribute != null)
                .Select(attribute => attribute.Name)
                .FirstOrDefault();
            return typeName;
        }
    }
}
