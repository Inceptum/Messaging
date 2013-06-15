using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Castle.Core.Logging;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
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
    internal class CommandsHandler
    {
        public readonly List<string> HandledCommands= new List<string>();

        private void Handle(string m)
        {
            Console.WriteLine("Command received:" + m);
           // m_CqrsEngine.PublishEvent("event fired by command", "integration");
            HandledCommands.Add(m);
        }
    }


    internal class EventListener
    {
        public readonly List<Tuple<string, string>> EventsWithBoundContext= new List<Tuple<string, string>>();
        public readonly List<string> Events= new List<string>();

        void Handle(string m,string boundContext)
        {
            EventsWithBoundContext.Add(Tuple.Create(m,boundContext));
            Console.WriteLine(boundContext+":"+m);
        } 
        void Handle(string m)
        {
            Events.Add(m);
            Console.WriteLine(m);
        } 
    }
    // ReSharper disable InconsistentNaming
    // ReSharper disable PossibleNullReferenceException

    class CqrEngineDependentComponent
    {
        public CqrEngineDependentComponent(ICqrsEngine engine)
        {
        }
    }

    [TestFixture]
    public class CqrsFacilityTests
    {
        [Test]
        public void CqrsEngineIsResolvableASDependencyOnlyAfterInit()
        {
            bool reslovedCqrsDependentComponentBeforeInit = false;
            var container = new WindsorContainer();
            container.Register(Component.For<IMessagingEngine>().Instance(MockRepository.GenerateMock<IMessagingEngine>()));
            container.Register(Component.For<CqrEngineDependentComponent>());
            container.AddFacility<CqrsFacility>();
            try
            {
                container.Resolve<CqrEngineDependentComponent>();
                reslovedCqrsDependentComponentBeforeInit = true;
            }
            catch (Exception)
            {

            }
            var cqrsEngine = container.Resolve<ICqrsEngine>();
            cqrsEngine.Init();

            container.Resolve<CqrEngineDependentComponent>();
            Assert.That(reslovedCqrsDependentComponentBeforeInit,Is.False,"ICqrsEngine was resolved as dependency before it was initialized");
        }


        [Test]
        public void EventsListenerWiringTest()
        {
            var container=new WindsorContainer();
            container.Register(Component.For<IMessagingEngine>().Instance(MockRepository.GenerateMock<IMessagingEngine>()));
            container.AddFacility<CqrsFacility>();
            container.Register(Component.For<EventListener>().AsEventsListener()/*,
                Classes.FromThisAssembly().BasedOn(typeof(object)).WithService.AllInterfaces().*/);
            CqrsEngine cqrsEngine = (CqrsEngine) container.Resolve<ICqrsEngine>();
            var eventListener = container.Resolve<EventListener>();
            cqrsEngine.EventDispatcher.Dispacth("test","bc");
            Assert.That(eventListener.EventsWithBoundContext, Is.EqualTo(new[] { Tuple.Create("test", "bc") }),"Event was not dispatched");
            Assert.That(eventListener.Events, Is.EqualTo(new[] { "test" }), "Event was not dispatched");
        }

        [Test]
        [Ignore("incomplete test")]
        public void CommandsHandlerWiringTest()
        {
            var container=new WindsorContainer();
            container.Register(Component.For<IMessagingEngine>().Instance(MockRepository.GenerateMock<IMessagingEngine>()));
            container.AddFacility<CqrsFacility>();
            container.Register(Component.For<CommandsHandler>().AsCommandsHandler("test"));
            var cqrsEngine = container.Resolve<ICqrsEngine>();
            var commandsHandler = container.Resolve<CommandsHandler>();
            //cqrsEngine.CommandDispatcher.Dispacth("test","bc");
            Assert.That(commandsHandler.HandledCommands, Is.EqualTo(new[] { "test" }), "Event was not dispatched");
        }

        [Test]
        public void CqrsEngineTest()
        {
            var serializationManager= new SerializationManager();
            serializationManager.RegisterSerializerFactory(new JsonSerializerFactory());
            var transportResolver= new TransportResolver(new Dictionary<string, TransportInfo> { { "test", new TransportInfo("localhost", "guest", "guest", null, "RabbitMq") } });
            var messagingEngine= new MessagingEngine(transportResolver,new RabbitMqTransportFactory())
                {
                    Logger= new ConsoleLogger(LoggerLevel.Debug)
                };
         /*   var eventExchange", new Endpoint("test", "unistream.processing.events", true, "json");
            var eventQueue", new Endpoint("test", "unistream.processing.UPlusAdapter.TransferPublisher", true, "json");
            var commandExchange", new Endpoint("test", "unistream.u1.commands", true, "json");
            var commandQueue", new Endpoint("test", "unistream.u1.commands", true, "json");*/
            
            var cqrsEngine= new CqrsEngine(messagingEngine,new FakeEndpointResolver(), BoundContext.Local("integration")
                                                   .PublishingEvents(typeof(string)).To("eventExchange").RoutedTo("eventQueue")
                                                   .ListeningCommands(typeof(string)).On("commandExchange").RoutedFrom("commandQueue")
                                                   //.ListeningCommands(typeof(string)).locally()
                                                   );
           /* var c=new CqrsEngine(messagingEngine, BoundContext.Remote("integration")
                                                   .ListeningCommands(typeof(TestCommand)).On(new Endpoint())
                                                   .PublishingEvents(typeof(TransferCreatedEvent)).To(new Endpoint()),
                                                   BoundContext.Local("testBC")
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
    }

    public class FakeEndpointResolver : IEndpointResolver
    {
        private Dictionary<string, Endpoint> m_Endpoints = new Dictionary<string, Endpoint>
            {
                {"eventExchange", new Endpoint("test", "unistream.processing.events", true, "json")},
                {"eventQueue", new Endpoint("test", "unistream.processing.UPlusAdapter.TransferPublisher", true, "json")},
                {"commandExchange", new Endpoint("test", "unistream.u1.commands", true, "json")},
                {"commandQueue", new Endpoint("test", "unistream.u1.commands", true, "json")}
            };
        public Endpoint Resolve(string endpoint)
        {
            return m_Endpoints[endpoint];
        }
    }

    // ReSharper restore InconsistentNaming
    // ReSharper restore PossibleNullReferenceException


    internal class Transfer
    {
        public Guid Id { get; set; }
        public string Code { get; set; }
        public DateTime CreationDate { get; set; }
    }

    public class TransferCreatedEvent
    {
        public Guid Id { get; set; }
        public string Code { get; set; }
    }

    public class TransferRegisteredInLigacyProcessingEvent
    {
        public Guid Id { get; set; }
        public DateTime CreationDate { get; set; }
    }

    public class TestCommand
    {

    }


    internal class TransferProjection
    {
        private readonly Dictionary<Guid, Transfer> m_Transfers= new Dictionary<Guid, Transfer>();

        public void Handle(TransferCreatedEvent e)
        {
            Transfer transfer;
            if (!m_Transfers.TryGetValue(e.Id, out transfer))
            {
                transfer= new Transfer { Id = e.Id };
                m_Transfers.Add(e.Id, transfer);
            }
            transfer.Code = e.Code;
        }

        public void Handle(TransferRegisteredInLigacyProcessingEvent e)
        {
            Transfer transfer;
            if (!m_Transfers.TryGetValue(e.Id, out transfer))
            {
                transfer= new Transfer { Id = e.Id };
                m_Transfers.Add(e.Id, transfer);
            }
            transfer.CreationDate = e.CreationDate;
        }
    }

}