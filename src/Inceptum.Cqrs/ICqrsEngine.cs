namespace Inceptum.Cqrs
{
    public interface ICqrsEngine
    {
        void SendCommand<T>(T command, string boundedContext);
    }
}