using System;
using System.Collections.Generic;

namespace Inceptum.Cqrs.Configuration
{
    class NameDescriptor : IBoundedContextDescriptor
    {
        private string m_Name;

        public NameDescriptor(string name)
        {
            m_Name = name;
        }

        public IEnumerable<Type> GetDependedncies()
        {
            return new Type[0];
        }

        public void Create(BoundedContext boundedContext, Func<Type, object> resolve)
        {
            boundedContext.Name = m_Name;
        }

        public void Process(BoundedContext boundedContext, CqrsEngine cqrsEngine)
        {
            

        }
    }
}