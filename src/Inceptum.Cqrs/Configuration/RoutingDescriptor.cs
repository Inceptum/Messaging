using System;
using System.Collections.Generic;

namespace Inceptum.Cqrs.Configuration
{
    public class RoutingDescriptor : IBoundedContextDescriptor
    {
        private readonly Dictionary<Type, string> m_EventRoutes;
        private readonly Dictionary<Type, string> m_CommandRoutes;

        public RoutingDescriptor(Dictionary<Type, string> eventRoutes, Dictionary<Type, string> commandRoutes)
        {
            m_CommandRoutes = commandRoutes;
            m_EventRoutes = eventRoutes;
        }

        public void Create(BoundedContext boundedContext)
        {
            boundedContext.CommandRoutes = m_CommandRoutes;
            boundedContext.EventRoutes = m_EventRoutes;
        }
    }
}