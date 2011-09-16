using System;
using System.Runtime.Serialization;

namespace Inceptum.DataBus
{
    public class DataBusException : Exception
    {
        public DataBusException()
        {
        }

        public DataBusException(string message) : base(message)
        {
        }

        public DataBusException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected DataBusException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}