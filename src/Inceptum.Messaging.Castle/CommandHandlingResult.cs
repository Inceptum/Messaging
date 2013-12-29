namespace Inceptum.Messaging.Castle
{
    //TODO: use this class in CQRS engine (there is a copy paste)
    public class CommandHandlingResult
    {
        public long RetryDelay { get; set; }
        public bool Retry { get; set; }
    }
}