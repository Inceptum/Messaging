using System;
using System.Collections.Generic;

namespace Inceptum.Cqrs.Configuration
{
    public interface IBoundedContextDescriptor
    {
        IEnumerable<Type> GetDependedncies();
        void Create(BoundedContext boundedContext, Func<Type, object> resolve);
        void Process(BoundedContext boundedContext, CommandSender commandSender);
    }
}