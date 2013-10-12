using System;
using System.Runtime.Serialization;

namespace Inceptum.Messaging
{
    public class ProcessingException : Exception
    {
        public ProcessingException()
        {
        }

        public ProcessingException(string message) : base(message)
        {
        }

        public ProcessingException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected ProcessingException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
