using System;
using System.IO;
using ProtoBuf;
using Sonic.Jms;

namespace Inceptum.Messaging
{
    internal class ProtobufSerializer<TMessage> : IMessageSerializer<TMessage>
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
    }
}
