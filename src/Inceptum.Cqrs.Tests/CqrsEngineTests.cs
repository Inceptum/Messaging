using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading;
using Castle.Core.Logging;
using CommonDomain.Persistence;
using EventStore;
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

        public CommandHandler()
        {
        }

        public void Handle(decimal command, IEventPublisher eventPublisher, IRepository repository)
        {
            Console.WriteLine("command recived:" + command);
            eventPublisher.PublishEvent(++counter);
        }

        public void Handle(string command,IEventPublisher eventPublisher)
        {
            Console.WriteLine("command recived:" +command);
            eventPublisher.PublishEvent(++counter);
        }

        public void Handle(DateTime command, IEventPublisher eventPublisher)
        {
            Console.WriteLine("command recived:" +command);
            eventPublisher.PublishEvent(++counter);
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
        [ExpectedException(typeof(ConfigurationErrorsException),ExpectedMessage = "Can not register System.String as command in bound context bc, it is already registered as event")]
        public void BoundedContextCanNotHaveEventAndCommandOfSameType()
        {
            new CqrsEngine(LocalBoundedContext.Named("bc")
                                                     .PublishingEvents(typeof(string)).To("eventExchange").RoutedTo("eventQueue")
                                                     .ListeningCommands(typeof (string)).On("commandExchange").RoutedFrom("commandQueue"));
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
            using (messagingEngine)
            {


                var cqrsEngine = new CqrsEngine(Activator.CreateInstance, messagingEngine, new FakeEndpointResolver(), LocalBoundedContext.Named("integration")
                    .PublishingEvents(typeof (int)).To("eventExchange").RoutedTo("eventQueue")
                    .ListeningCommands(typeof (string)).On("commandExchange").RoutedFrom("commandQueue")
                    .WithCommandsHandler<CommandsHandler>(),
                    LocalBoundedContext.Named("bc").WithProjection<EventListener>("integration")

                                                   
                    //.ListeningCommands(typeof(string)).locally()
                    );
                /* var c=new CqrsEngine(messagingEngine, RemoteBoundedContext.Named("integration")
                                                    .ListeningCommands(typeof(TestCommand)).On(new Endpoint())
                                                    .PublishingEvents(typeof(TransferCreatedEvent)).To(new Endpoint()),
                                                    LocalBoundedContext.Named("testBC")
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



                //  messagingEngine.Send("test", new Endpoint("test", "unistream.u1.commands", true,"json"));
                cqrsEngine.SendCommand("test", "integration");
                Thread.Sleep(3000);
            }
        }

      
        [Test]
        public void Method_Scenario_Expected()
        {
            using (var engine = new CqrsEngine(
                LocalBoundedContext.Named("local")
                                   .PublishingEvents(typeof (int)).To("events").RoutedTo("events")
                                   .ListeningCommands(typeof (string)).On("commands1").RoutedFromSameEndpoint()
                                   .ListeningCommands(typeof (DateTime)).On("commands2").RoutedFromSameEndpoint()
                                   .WithCommandsHandler<CommandHandler>()
                                   .WithProcess<TestProcess>()
                                   .WithEventStore(dispatchCommits => Wireup.Init()
                                                                            .LogToOutputWindow()
                                                                            .UsingInMemoryPersistence()
                                                                            .InitializeStorageEngine()
                                                                            .UsingJsonSerialization()
                                                                            .UsingSynchronousDispatchScheduler()
                                                                            .DispatchTo(dispatchCommits))
                ,
                LocalBoundedContext.Named("projections")
                                   .WithProjection<EventsListener>("local"),
                RemoteBoundedContext.Named("remote")
                                    .ListeningCommands(typeof (object)).On("remoteCommands")
                                    .PublishingEvents(typeof (int)).To("remoteEvents"),
                Saga<TestSaga>.Listening("local", "projections"),
                Saga.Instance(new TestSaga()).Listening("local", "projections")
                ))
            {
                /*
                                                                                .WithEventSource()
                                                                                .WithAggregates()
                                                                                .WithDocumentStore());
                             * */

                engine.SendCommand("test", "local");
                engine.SendCommand(DateTime.Now, "local");

                Thread.Sleep(500);
                Console.WriteLine("Disposing...");
            }
            Console.WriteLine("Dispose completed.");
        }
    }

    public class TestProcess:IProcess
    {
        public void Start(ICqrsEngine cqrsEngine, IEventPublisher eventPublisher)
        {
            Console.WriteLine("Test process started");
        }

        public void Dispose()
        {
            Console.WriteLine("Test process disposed");
        }
    }

    public class TestSaga
    {
        private void Handle(int @event , ICqrsEngine engine, string boundedContext)
        {
            Console.WriteLine("Event cought by saga:"+@event);
        }
    }
}