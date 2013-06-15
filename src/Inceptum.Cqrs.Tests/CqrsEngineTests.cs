using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading;
using Castle.Core.Logging;
using Inceptum.Cqrs.Configuration;
using Inceptum.Messaging;
using Inceptum.Messaging.Contract;
using Inceptum.Messaging.RabbitMq;
using Inceptum.Messaging.Serialization;
using NUnit.Framework;
using Rhino.Mocks;

namespace Inceptum.Cqrs.Tests
{
    class CommandHandler
    {
        private readonly ICqrsEngine m_Engine;
        private int counter = 0;

        public CommandHandler(ICqrsEngine engine)
        {
            m_Engine = engine;
        }

        public void Handle(string command)
        {
            Console.WriteLine("command recived:" +command);
            m_Engine.PublishEvent(++counter, "local");
        } 
        
        public void Handle(DateTime command)
        {
            Console.WriteLine("command recived:" +command);
            m_Engine.PublishEvent(++counter,"local");
        }
    }

    class EventsListener
    {
        public void Handle(int e,string boundedContext)
        {
            Console.WriteLine(boundedContext+":"+e);
        }  
    }

    [TestFixture]
    public class CqrsEngineTests
    {

        [Test]
        [ExpectedException(typeof(ConfigurationErrorsException),ExpectedMessage = "Can not register System.String as command in bound context test, it is already registered as event")]
        public void BoundedContextCanNotHaveEvetAndCommandOfSameType()
        {
            new CqrsEngine(BoundedContext.Local("bc")
                                                     .PublishingEvents(typeof(string)).To("eventExchange").RoutedTo("eventQueue")
                                                     .ListeningCommands(typeof (string)).On("commandExchange").RoutedFrom("commandQueue"));
        }


        [Test]
        [ExpectedException(typeof(ConfigurationErrorsException), ExpectedMessage = "Command handlers registered for unknown bound contexts: unknownBc1,unknownBc2")]
        public void HandlerForUnknownCommandTest()
        {
            var cqrsEngine = new CqrsEngine(BoundedContext.Local("bc").ListeningCommands(typeof (int)).On("commandExchange").RoutedFrom("commandQueue"));
            cqrsEngine.WireCommandsHandler(new CommandsHandler(), "unknownBc1");
            cqrsEngine.WireCommandsHandler(new CommandsHandler(), "unknownBc2");
            cqrsEngine.Init();
        }


        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void EventsListenerWiringIsNotAllowedWhenEngineIsInitilizedTest()
        {
            var cqrsEngine = new CqrsEngine();
            cqrsEngine.Init();
            cqrsEngine.WireEventsListener(new CommandsHandler());
            
        }
        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void CommandsHandlerWiringIsNotAllowedWhenEngineIsInitilizedTest()
        {
            var cqrsEngine = new CqrsEngine(BoundedContext.Local("bc").ListeningCommands(typeof(int)).On("commandExchange").RoutedFrom("commandQueue"));
            cqrsEngine.Init();
            cqrsEngine.WireCommandsHandler(new CommandsHandler(), "bc");
            
        }

        [Test]
        [Ignore("investigation test")]
        public void CqrsEngineTest()
        {
            var serializationManager = new SerializationManager();
            serializationManager.RegisterSerializerFactory(new JsonSerializerFactory());
            var transportResolver = new TransportResolver(new Dictionary<string, TransportInfo> { { "test", new TransportInfo("localhost", "guest", "guest", null, "RabbitMq") } });
            var messagingEngine = new MessagingEngine(transportResolver, new RabbitMqTransportFactory())
            {
                Logger = new ConsoleLogger(LoggerLevel.Debug)
            };


            var cqrsEngine = new CqrsEngine(messagingEngine, new FakeEndpointResolver(), BoundedContext.Local("integration")
                                                   .PublishingEvents(typeof(int)).To("eventExchange").RoutedTo("eventQueue")
                                                   .ListeningCommands(typeof(string)).On("commandExchange").RoutedFrom("commandQueue")
                //.ListeningCommands(typeof(string)).locally()
                                                   );
            /* var c=new CqrsEngine(messagingEngine, BoundedContext.Remote("integration")
                                                    .ListeningCommands(typeof(TestCommand)).On(new Endpoint())
                                                    .PublishingEvents(typeof(TransferCreatedEvent)).To(new Endpoint()),
                                                    BoundedContext.Local("testBC")
                                                    .ListeningCommands(typeof(TestCommand)).On(new Endpoint("test", "unistream.u1.commands", true))
                                                    .ListeningCommands(typeof(int)).On(new Endpoint("test", "unistream.u1.commands", true))
                                                    .PublishingEvents(typeof (int)).To(new Endpoint()).RoutedTo(new Endpoint())
                                                    .PublishingEvents(typeof (string)).To(new Endpoint())
                                                    .WithEventStore(dispatchCommits => Wireup.Init()
                                                                                             .LogToOutputWindow()
                                                                                             .UsingInMemoryPersistence()
                                                                                             .InitializeStorageEngine()
                                                                                             .UsingJsonSerialization()
                                                                                             .UsingSynchronousDispatchScheduler()
                                                                                                 .DispatchTo(dispatchCommits))
                                                ); */


            cqrsEngine.WireEventsListener(new EventListener());
            cqrsEngine.WireCommandsHandler(new CommandsHandler(), "integration");
            cqrsEngine.Init();
            //  messagingEngine.Send("test", new Endpoint("test", "unistream.u1.commands", true,"json"));
            cqrsEngine.SendCommand("test", "integration");
            Thread.Sleep(3000);
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
        [Test]
        public void Method_Scenario_Expected()
        {
            var engine = new CqrsEngine(BoundedContext.Local("local")
                                                    .PublishingEvents(typeof (int)).To("events").RoutedTo("eventQueue")//RoutedToSameEndpoint()
                                                    .ListeningCommands(typeof(string)).On("commands1").RoutedFromSameEndpoint()
                                                    .ListeningCommands(typeof(DateTime)).On("commands2").RoutedFromSameEndpoint()/*
                                                    .WithEventSource()
                                                    .WithAggregates()
                                                    .WithDocumentStore()*/);
            engine
                .WireCommandsHandler(new CommandHandler(engine),"local")
                .WireEventsListener(new EventsListener());
            engine.Init();
            
            engine.SendCommand("test","local");
            engine.SendCommand(DateTime.Now,"local");

            Thread.Sleep(500);
        }
    }

}