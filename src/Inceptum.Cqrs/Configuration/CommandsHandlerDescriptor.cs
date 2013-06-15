using System;

namespace Inceptum.Cqrs.Configuration
{
    public class CommandsHandlerDescriptor : DescriptorWithDependencies
    {
        public CommandsHandlerDescriptor(params object[] handlers):base(handlers)
        {
        }

        public CommandsHandlerDescriptor(params Type[] handlers):base(handlers)
        {

        }


        protected override void Create(BoundedContext boundedContext)
        {
            foreach (var handler in ResolvedDependencies)
            {
                boundedContext.CommandDispatcher.Wire(handler);
                
            }
        }

    }
}