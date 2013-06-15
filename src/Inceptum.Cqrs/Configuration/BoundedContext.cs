using System;
using System.Collections.Generic;
using Inceptum.Messaging.Contract;

namespace Inceptum.Cqrs.Configuration
{
     

    public interface IEndpointResolver
    {
        Endpoint Resolve(string endpoint);
    }


    public class BoundedContext
    {
        internal Dictionary<string, IEnumerable<Type>> EventsSubscriptions { get; set; }
        internal Dictionary<string, IEnumerable<Type>> CommandsSubscriptions { get; set; }
        internal Dictionary<Type, string> CommandRoutes { get; set; }
        internal Dictionary<Type, string> EventRoutes { get; set; }
        public string Name { get; set; }

        public static LocalBoundedContextRegistration Local(string name)
        {
            return new LocalBoundedContextRegistration(name);
        }

        public static RemoteBoundedContextRegistration Remote(string name)
        {
            return new RemoteBoundedContextRegistration(name);
        }

        static void test()
        {
            var registrations = new BoundedContextRegistration[]
                {
                    BoundedContext.Remote("remote")
                            .PublishingEvents(typeof (object)).To("eventsExhange")
                            .ListeningCommands().On("commandsQueue"),
                    BoundedContext.Local("local2")
                            .PublishingEvents(typeof (object)).To("eventsExhange").RoutedTo("eventsQueue")
                            .PublishingEvents(typeof (object)).To("eventsExhange").RoutedToSameEndpoint()
                            .PublishingEvents(typeof (object)).To("eventsExhange").NotRouted()
                            .ListeningCommands(typeof (int)).On("commandsExhange").RoutedFrom("commandsQueue")
                            .ListeningCommands(typeof (int)).On("commandsExhange").RoutedFromSameEndpoint()
                            .ListeningCommands(typeof (int)).On("commandsExhange").NotRouted()
                            //.WithEventStore()
                };
        }

       
    }
}