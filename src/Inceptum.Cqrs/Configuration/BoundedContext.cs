using System;
using System.Collections.Generic;
using CommonDomain.Persistence;

namespace Inceptum.Cqrs.Configuration
{
    public class BoundedContext:IDisposable
    {
        internal Dictionary<Type, string> EventRoutes { get; set; }
        internal Dictionary<string, IEnumerable<Type>> EventsSubscriptions { get; set; }
        internal List<CommandSubscription> CommandsSubscriptions { get; set; }
        internal Dictionary<Tuple<Type, CommandPriority>, string> CommandRoutes { get; set; }
        internal EventsPublisher EventsPublisher { get; private set; }
        internal CommandDispatcher CommandDispatcher { get; private set; }
        internal EventDispatcher EventDispatcher { get; private set; }
        internal List<IProcess> Processes { get; private set; }
        internal IRepository Repository { get; set; }
        public string Name { get; set; }
        public int ThreadCount { get; set; }
        public long FailedCommandRetryDelay { get; set; }

        internal BoundedContext(CqrsEngine cqrsEngine, string name, int threadCount, long failedCommandRetryDelay)
        {
            ThreadCount = threadCount;
            FailedCommandRetryDelay = failedCommandRetryDelay;
            Name = name;
            EventsPublisher = new EventsPublisher(cqrsEngine, this);
            CommandDispatcher = new CommandDispatcher(Name, threadCount, failedCommandRetryDelay);
            EventDispatcher = new EventDispatcher(Name);
            Processes = new List<IProcess>();
        }

        public void Dispose()
        {
            CommandDispatcher.Dispose();
        }
    }
}