using System;
using System.Runtime.Serialization;

namespace Inceptum.Messaging.Contract
{
    //TODO: use instead of processing exception in messaging
    public class TransportException : Exception
    {
        public TransportException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public TransportException()
        {
        }

        public TransportException(string message)
            : base(message)
        {
        }

        public TransportException(string message, params object[] args)
            : base(string.Format(message, args))
        {
        }

        protected TransportException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }

}