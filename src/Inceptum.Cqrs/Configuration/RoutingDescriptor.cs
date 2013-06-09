using System;
using System.Collections.Generic;

namespace Inceptum.Cqrs.Configuration
{
    public class RoutingDescriptor : IBoundContextDescriptor
    {
        private readonly Dictionary<Type, string> m_EventRoutes;
        private readonly Dictionary<Type, string> m_CommandRoutes;

        public RoutingDescriptor(Dictionary<Type, string> eventRoutes, Dictionary<Type, string> commandRoutes)
        {
            m_CommandRoutes = commandRoutes;
            m_EventRoutes = eventRoutes;
        }

        public void Create(BoundContext boundContext)
        {
            boundContext.CommandRoutes = m_CommandRoutes;
            boundContext.EventRoutes = m_EventRoutes;
        }
    }
}