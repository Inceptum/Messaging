using System;
using Sonic.Jms;

namespace Inceptum.Messaging.Transports
{
    class BinaryMessage
    {
        public byte[] Bytes { get; set; }
        public string Type { get; set; }

        public BinaryMessage()
        {
        }

        public BinaryMessage(Message sonicMessage)
        {
            var bytesMessage=sonicMessage as BytesMessage;
            if(bytesMessage==null)
                throw new InvalidCastException("Message of unsupported type was received. Only binary messages are supported");
            Bytes = new byte[bytesMessage.getBodyLength()];
            bytesMessage.readBytes(Bytes);
            Type = bytesMessage.getJMSType();
        }
    }
}