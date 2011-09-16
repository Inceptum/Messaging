using System;
using System.IO;
using ProtoBuf;
using Sonic.Jms;

namespace Inceptum.Messaging
{
    internal class ProtobufSerializer<TMessage> : IMessageSerializer<TMessage>
    {
        public Message Serialize(TMessage message, Session sendSession)
        {
            BytesMessage m = sendSession.createBytesMessage();
            var s = new MemoryStream();
            Serializer.Serialize(s, message);
            m.writeBytes(s.ToArray());
            return m;
        }

        public TMessage Deserialize(Message message)
        {
            if (message == null) throw new ArgumentNullException("message");
            var bytesMessage = message as BytesMessage;
            if (bytesMessage == null) throw new ArgumentException("message is expected to caontain BytesMessage", "message");
            var buf = new byte[bytesMessage.getBodyLength()];
            bytesMessage.readBytes(buf);

            var memStream = new MemoryStream();
            memStream.Write(buf, 0, buf.Length);
            memStream.Seek(0, SeekOrigin.Begin);
            return Serializer.Deserialize<TMessage>(memStream);
        }
    }
}