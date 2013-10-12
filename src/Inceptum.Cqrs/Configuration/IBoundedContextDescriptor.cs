using System;
using System.Collections.Generic;

namespace Inceptum.Cqrs.Configuration
{
    public interface IBoundedContextDescriptor
    {
        IEnumerable<Type> GetDependencies();
        void Create(BoundedContext boundedContext, Func<Type, object> resolve);
        void Process(BoundedContext boundedContext, CqrsEngine cqrsEngine);
    }
}