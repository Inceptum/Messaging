using System;
using System.Collections.Generic;
using Inceptum.Messaging.Contract;

namespace Inceptum.Cqrs.Configuration
{
    public class Message
    {
        Dictionary<string, string> Headers { get; set; }  
        object Body { get; set; }  
    }

    public interface IMessagingAdapter
    {
        IDisposable Subscribe(string endpoint,Action<Message> action);
        void Send(Message message, string endpoint);
    }


    public class BC
    {
        internal Dictionary<Endpoint, IEnumerable<Type>> EventsSubscriptions { get; set; }
        internal Dictionary<Endpoint, IEnumerable<Type>> CommandsSubscriptions { get; set; }
        internal Dictionary<Type,Endpoint> CommandRoutes { get; set; }
        internal Dictionary<Type,Endpoint> EventRoutes { get; set; }


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
                    BC.Remote("remote")
                            .PublishingEvents(typeof (object)).To(new Endpoint())
                            .ListeningCommands().On(new Endpoint()),
                    BC.Local("local")
                            .PublishingEvents(typeof (object)).To(new Endpoint()).RoutedTo(new Endpoint())
                            .PublishingEvents(typeof (object)).To(new Endpoint()).RoutedToSameEndpoint()
                            .PublishingEvents(typeof (string)).Locally()
                            .ListeningCommands(typeof (int)).On(new Endpoint()).RoutedFrom(new Endpoint())
                            .ListeningCommands(typeof (int)).On(new Endpoint()).RoutedToSameEndpoint()
                            .ListeningCommands(typeof (long)).Locally()
                            //.WithEventStore()
                };
        }

       
    }
}