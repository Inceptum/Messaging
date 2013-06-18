using System;

namespace Inceptum.Cqrs.Configuration
{
    public interface IProcess : IDisposable
    {
        void Start(ICqrsEngine cqrsEngine, IEventPublisher eventPublisher);
    }
}
