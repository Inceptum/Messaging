using System;

namespace Inceptum.Cqrs.Configuration
{
    public interface IProcess : IDisposable
    {
        void Start(ICommandSender commandSender, IEventPublisher eventPublisher);
    }
}
