namespace Inceptum.Core.Messaging
{
    public class TransportOutdatedException : TransportException
    {
        public TransportOutdatedException(string message, params object[] args) 
            : base(message, args)
        {
        }
    }
}