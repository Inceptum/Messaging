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

    internal interface IProcessingGroup : IDisposable
    {
        IDisposable Subscribe(string destination, Action<BinaryMessage> callback, string messageType);
        void Send(string destination, BinaryMessage message);
        IDisposable SendRequest(string destination, BinaryMessage message, Action<BinaryMessage> callback);
        IDisposable RegisterHandler(string destination, Func<BinaryMessage, BinaryMessage> handler, string messageType);
    }
}