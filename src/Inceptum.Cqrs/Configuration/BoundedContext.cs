using System;
using System.Collections.Generic;

namespace Inceptum.Cqrs.Configuration
{
    public class BoundedContext
    {
        internal Dictionary<Type, string> EventRoutes { get; set; }
        internal Dictionary<string, IEnumerable<Type>> EventsSubscriptions { get; set; }
        internal Dictionary<string, IEnumerable<Type>> CommandsSubscriptions { get; set; }
        internal Dictionary<Type, string> CommandRoutes { get; set; }
        internal EventsPublisher EventsPublisher { get; private set; }
        internal CommandDispatcher CommandDispatcher { get; private set; }
        internal EventDispatcher EventDispatcher { get; private set; }
        public string Name { get; set; }

        internal BoundedContext(CqrsEngine cqrsEngine)
        {
            EventsPublisher = new EventsPublisher(cqrsEngine, this);
            CommandDispatcher = new CommandDispatcher(this);
            EventDispatcher = new EventDispatcher(this);
        }
         
    }
}