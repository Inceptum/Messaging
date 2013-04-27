namespace Inceptum.Messaging.Contract
{
    public class TransportOutdatedException : TransportException
    {
        public TransportOutdatedException(string message, params object[] args) 
            : base(message, args)
        {
        }
    }
}