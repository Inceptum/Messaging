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
        public static List<object> AcceptedCommands=new List<object>(); 
        private readonly ICommandSender m_Engine;
        private int counter = 0;
        private int m_ProcessingTimeout;

        public CommandHandler(int processingTimeout=0)
        {
            m_ProcessingTimeout = processingTimeout;
        }

        public void Handle(decimal command, IEventPublisher eventPublisher, IRepository repository)
        {
            Thread.Sleep(m_ProcessingTimeout);
            Console.WriteLine("command recived:" + command);
            eventPublisher.PublishEvent(++counter);
            AcceptedCommands.Add(command);
        }

        public void Handle(string command,IEventPublisher eventPublisher)
        {
            Thread.Sleep(m_ProcessingTimeout);
            Console.WriteLine("command recived:" + command);
            eventPublisher.PublishEvent(++counter);
            AcceptedCommands.Add(command);
        }

        public void Handle(DateTime command, IEventPublisher eventPublisher)
        {
            Thread.Sleep(m_ProcessingTimeout);
            Console.WriteLine("command recived:" + command);
            eventPublisher.PublishEvent(++counter);
            AcceptedCommands.Add(command);
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
            new InMemoryCqrsEngine(LocalBoundedContext.Named("bc")
                                                     .PublishingEvents(typeof(string)).To("eventExchange").RoutedTo("eventQueue")
                                                     .ListeningCommands(typeof(string)).On("commandQueue").RoutedFrom("commandExchange"));
        }


        [Test]
        public void ListenSameCommandOnDifferentEndpointsTest()
        {
            using (
                var messagingEngine =
                    new MessagingEngine(
                        new TransportResolver(new Dictionary<string, TransportInfo>
                            {
                                {"InMemory", new TransportInfo("none", "none", "none", null, "InMemory")}
                            })))
            {
                using (var engine = new CqrsEngine(Activator.CreateInstance, messagingEngine,
                                                   new InMemoryEndpointResolver(),
                                                   LocalBoundedContext.Named("bc")
                                                                       .PublishingEvents(typeof(int)).To("eventExchange").RoutedTo("eventQueue")
                                                                       .ListeningCommands(typeof(string)).On("exchange1").On("exchange2").RoutedFrom("commandQueue")
                                                                       .WithCommandsHandler<CommandHandler>())
                    )
                {
                    messagingEngine.Send("test1", new Endpoint("InMemory", "exchange1", serializationFormat: "json"));
                    messagingEngine.Send("test2", new Endpoint("InMemory", "exchange2", serializationFormat: "json"));
                    messagingEngine.Send("test3", new Endpoint("InMemory", "exchange3", serializationFormat: "json"));
                    Thread.Sleep(2000);
                    Assert.That(CommandHandler.AcceptedCommands,Is.EquivalentTo(new []{"test1","test2"}));
                }
            }
        }



        [Test]
        public void PrioritizedCommandsProcessingTest()
        {
            using (
                var messagingEngine =
                    new MessagingEngine(
                        new TransportResolver(new Dictionary<string, TransportInfo>
                            {
                                {"InMemory", new TransportInfo("none", "none", "none", null, "InMemory")}
                            })))
            {
                using (var engine = new CqrsEngine(Activator.CreateInstance, messagingEngine,
                                                   new InMemoryEndpointResolver(),
                                                   LocalBoundedContext.Named("bc").ConcurrencyLevel(1)
                                                                       .PublishingEvents(typeof(int)).To("eventExchange").RoutedTo("eventQueue")
                                                                       .ListeningCommands(typeof(string))
                                                                            .On("exchange1", CommandPriority.Low)
                                                                            .On("exchange2", CommandPriority.High)
                                                                            .RoutedFrom("commandQueue")
                                                                  
                                                                       .WithCommandsHandler(new CommandHandler(100)))
                    )
                {
                    messagingEngine.Send("low1", new Endpoint("InMemory", "exchange1", serializationFormat: "json"));
                    messagingEngine.Send("low2", new Endpoint("InMemory", "exchange1", serializationFormat: "json"));
                    messagingEngine.Send("low3", new Endpoint("InMemory", "exchange1", serializationFormat: "json"));
                    messagingEngine.Send("low4", new Endpoint("InMemory", "exchange1", serializationFormat: "json"));
                    messagingEngine.Send("low5", new Endpoint("InMemory", "exchange1", serializationFormat: "json"));
                    messagingEngine.Send("low6", new Endpoint("InMemory", "exchange1", serializationFormat: "json"));
                    messagingEngine.Send("low7", new Endpoint("InMemory", "exchange1", serializationFormat: "json"));
                    messagingEngine.Send("low8", new Endpoint("InMemory", "exchange1", serializationFormat: "json"));
                    messagingEngine.Send("low9", new Endpoint("InMemory", "exchange1", serializationFormat: "json"));
                    messagingEngine.Send("low10", new Endpoint("InMemory", "exchange1", serializationFormat: "json"));
                    messagingEngine.Send("high", new Endpoint("InMemory", "exchange2", serializationFormat: "json"));
                    Thread.Sleep(2000);
                    Assert.That(CommandHandler.AcceptedCommands.Take(2).Any(c=>(string) c=="high"),Is.True);
                }
            }
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
                /* var c=new commandSender(messagingEngine, RemoteBoundedContext.Named("integration")
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
            using (var engine = new InMemoryCqrsEngine(
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
        public void Start(ICommandSender commandSender, IEventPublisher eventPublisher)
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
        private void Handle(int @event , ICommandSender engine, string boundedContext)
        {
            Console.WriteLine("Event cought by saga:"+@event);
        }
    }
}