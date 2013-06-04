using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Castle.Core.Logging;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using EventStore;
using Inceptum.Messaging;
using Inceptum.Messaging.Contract;
using Inceptum.Messaging.RabbitMq;
using Inceptum.Messaging.Serialization;
using NUnit.Framework;
using Rhino.Mocks;

namespace Inceptum.Cqrs.Tests
{
    internal class CommandHandler
    {
        private CqrsEngine m_CqrsEngine;
        private MessagingEngine m_MessagingEngine;

        public CommandHandler(CqrsEngine cqrsEngine, MessagingEngine messagingEngine)
        {
            m_MessagingEngine = messagingEngine;
            m_CqrsEngine = cqrsEngine;
        }

        private void Handle(string m)
        {
            Console.WriteLine("Command received:" + m);
            //m_MessagingEngine.Send("event fired by command", new Endpoint("test", "unistream.processing.events", true, "json"));
            m_CqrsEngine.PublishEvent("event fired by command", "integration");
        }
    }
    internal class EventListener
    {
        void Handle(string m,string boundContext)
        {
            Console.WriteLine(boundContext+":"+m);
        } 
        void Handle(string m)
        {
            Console.WriteLine(m);
        } 
    }
    // ReSharper disable InconsistentNaming
    // ReSharper disable PossibleNullReferenceException

    [TestFixture]
    public class CqrsFacilityTests
    {
        [Test]
        public void EventHandlerWiringTest()
        {
            var container=new WindsorContainer();
            container.Register(
                Component.For<IMessagingEngine>().Instance(MockRepository.GenerateMock<IMessagingEngine>()));
            container.AddFacility<CqrsFacility>();
            container.Register(Component.For<EventListener>().AsEventListener());
            var cqrsEngine = container.Resolve<ICqrsEngine>();
            cqrsEngine.EventDispatcher.Dispacth("test","bc");


        }

        [Test]
        public void Method_Scenario_Expected()
        {
            var serializationManager = new SerializationManager();
            serializationManager.RegisterSerializerFactory(new JsonSerializerFactory());
            var transportResolver = new TransportResolver(new Dictionary<string, TransportInfo> { { "test", new TransportInfo("localhost", "guest", "guest", null, "RabbitMq") } });
            var messagingEngine = new MessagingEngine(transportResolver,serializationManager, new RabbitMqTransportFactory())
                {
                    Logger = new ConsoleLogger(LoggerLevel.Debug)
                };
            var eventExchange = new Endpoint("test", "unistream.processing.events", true, "json");
            var eventQueue = new Endpoint("test", "unistream.processing.UPlusAdapter.TransferPublisher", true, "json");
            var commandExchange = new Endpoint("test", "unistream.u1.commands", true, "json");
            var commandQueue = new Endpoint("test", "unistream.u1.commands", true, "json");
            
            var cqrsEngine = new CqrsEngine(messagingEngine, config => config
                                               .WithLocalBoundContext("integration")
                                                   .PublishingEvents(typeof(string)).To(eventExchange).RoutedTo(eventQueue)
                                                   .ListeningCommands(typeof(string)).On(commandExchange).RoutedFrom(commandQueue)
                                                   );
/*            var c=new CqrsEngine(messagingEngine, config => config
                                               .WithRemoteBoundContext("integration")
                                                   .ListeningCommands(typeof(TestCommand)).On(new Endpoint())
                                                   .PublishingEvents(typeof(TransferCreatedEvent)).To(new Endpoint())
                                               .WithLocalBoundContext("someBC")
                                                   .ListeningCommands(typeof(TestCommand)).On(new Endpoint())
                                                   .PublishingEvents().To(new Endpoint())
                                               .WithLocalBoundContext("testBC")
                                                   .ListeningCommands(typeof(TestCommand)).On(new Endpoint("test", "unistream.u1.commands", true))
                                                   .PublishingEvents(typeof (int)).To(new Endpoint()).RoutedTo(new Endpoint())
                                                   .PublishingEvents(typeof (string)).To(new Endpoint())
                                                   .WithEventStore(dispatchCommits => Wireup.Init()
                                                                                .LogToOutputWindow()
                                                                                .UsingInMemoryPersistence()
                                                                                .InitializeStorageEngine()
                                                                                .UsingJsonSerialization()
                                                                                .UsingSynchronousDispatchScheduler()
                                                                                    .DispatchTo(dispatchCommits))
                                               );*/
            cqrsEngine.EventDispatcher.Wire(new EventListener());
            cqrsEngine.CommandDispatcher.Wire(new CommandHandler(cqrsEngine,messagingEngine));
            cqrsEngine.Init();
          //  messagingEngine.Send("test", new Endpoint("test", "unistream.u1.commands", true,"json"));
              cqrsEngine.SendCommand("test", "integration");
            Thread.Sleep(3000);
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
        private readonly Dictionary<Guid, Transfer> m_Transfers = new Dictionary<Guid, Transfer>();

        public void Handle(TransferCreatedEvent e)
        {
            Transfer transfer;
            if (!m_Transfers.TryGetValue(e.Id, out transfer))
            {
                transfer = new Transfer { Id = e.Id };
                m_Transfers.Add(e.Id, transfer);
            }
            transfer.Code = e.Code;
        }

        public void Handle(TransferRegisteredInLigacyProcessingEvent e)
        {
            Transfer transfer;
            if (!m_Transfers.TryGetValue(e.Id, out transfer))
            {
                transfer = new Transfer { Id = e.Id };
                m_Transfers.Add(e.Id, transfer);
            }
            transfer.CreationDate = e.CreationDate;
        }
    }

}