using System;
using CommonDomain.Persistence;

namespace Inceptum.Cqrs.Configuration
{
    class CommandsHandlerDescriptor : DescriptorWithDependencies
    {
        public CommandsHandlerDescriptor(params object[] handlers):base(handlers)
        {
        }

        public CommandsHandlerDescriptor(params Type[] handlers):base(handlers)
        {

        }
        
        public override void Process(BoundedContext boundedContext, CqrsEngine cqrsEngine)
        {
            foreach (var handler in ResolvedDependencies)
            {
                boundedContext.CommandDispatcher.Wire(handler,
                                                      new OptionalParameter<IEventPublisher>(boundedContext.EventsPublisher),
                                                      new OptionalParameter<IRepository>(boundedContext.Repository)
                    );
            }
        }

    }
}