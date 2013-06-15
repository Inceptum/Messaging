using System;

namespace Inceptum.Cqrs
{
    public interface ICqrsEngine : IDisposable
    {
        void SendCommand<T>(T command, string boundedContext);
    }
}