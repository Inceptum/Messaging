using System;
using System.Collections.Generic;
using Inceptum.Messaging.Contract;

namespace Inceptum.Cqrs.Configuration
{
     

    public interface IEndpointResolver
    {
        Endpoint Resolve(string endpoint);
    }


    public class BoundContext
    {
        internal Dictionary<string, IEnumerable<Type>> EventsSubscriptions { get; set; }
        internal Dictionary<string, IEnumerable<Type>> CommandsSubscriptions { get; set; }
        internal Dictionary<Type, string> CommandRoutes { get; set; }
        internal Dictionary<Type, string> EventRoutes { get; set; }
        public string Name { get; set; }

        public static LocalBoundContextRegistration Local(string name)
        {
            return new LocalBoundContextRegistration(name);
        }

        public static RemoteBoundContextRegistration Remote(string name)
        {
            return new RemoteBoundContextRegistration(name);
        }

        static void test()
        {
            var registrations = new BoundContextRegistration[]
                {
                    BoundContext.Remote("remote")
                            .PublishingEvents(typeof (object)).To("eventsExhange")
                            .ListeningCommands().On("commandsQueue"),
                    BoundContext.Local("local2")
                            .PublishingEvents(typeof (object)).To("eventsExhange").RoutedTo("eventsQueue")
                            .PublishingEvents(typeof (object)).To("eventsExhange").RoutedToSameEndpoint()
                            .PublishingEvents(typeof (object)).To("eventsExhange").NotRouted()
                            .ListeningCommands(typeof (int)).On("commandsExhange").RoutedFrom("commandsQueue")
                            .ListeningCommands(typeof (int)).On("commandsExhange").RoutedToSameEndpoint()
                            //.WithEventStore()
                };
        }

       
    }
}