using System;

namespace Inceptum.Messaging
{
    public class MessagingSessionClosedException : Exception
    {
        public MessagingSessionClosedException()
        {
        }

        public MessagingSessionClosedException(string message) : base(message)
        {
        }

        public MessagingSessionClosedException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}