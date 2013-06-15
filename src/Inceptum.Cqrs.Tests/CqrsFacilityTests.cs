using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Castle.Core.Logging;
using Castle.Facilities.Startable;
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
            HandledCommands.Add(m);
        }
    }


    internal class EventListener
    {
        public readonly List<Tuple<string, string>> EventsWithBoundedContext= new List<Tuple<string, string>>();
        public readonly List<string> Events= new List<string>();

        void Handle(string m,string boundedContext)
        {
            EventsWithBoundedContext.Add(Tuple.Create(m,boundedContext));
            Console.WriteLine(boundedContext+":"+m);
        } 
        void Handle(string m)
        {
            Events.Add(m);
            Console.WriteLine(m);
        } 
    }
    

    class CqrEngineDependentComponent
    {
        public  static bool Started { get; set; }
        public CqrEngineDependentComponent(ICqrsEngine engine)
        {
        }
        public void Start()
        {
            Started = true;
        }
    }

    [TestFixture]
    public class CqrsFacilityTests
    {
        [Test]
        public void CqrsEngineIsResolvableAsDependencyOnlyAfterInit()
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
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            var cqrsEngine = container.Resolve<ICqrsEngine>();
            cqrsEngine.Init();

            container.Resolve<CqrEngineDependentComponent>();
            Assert.That(reslovedCqrsDependentComponentBeforeInit,Is.False,"ICqrsEngine was resolved as dependency before it was initialized");
        }

        [Test]
        public void CqrsEngineIsResolvableAsDependencyOnlyAfterInitStartableFacility()
        {
            var container = new WindsorContainer();
            container.AddFacility<CqrsFacility>()
                     .AddFacility<StartableFacility>();// (f => f.DeferredTryStart());
            container.Register(Component.For<IMessagingEngine>().Instance(MockRepository.GenerateMock<IMessagingEngine>()));
            container.Register(Component.For<CqrEngineDependentComponent>().StartUsingMethod("Start"));
            Assert.That(CqrEngineDependentComponent.Started,Is.False,"Component was started before CqrsEngine initialization");
            var cqrsEngine = container.Resolve<ICqrsEngine>();
            cqrsEngine.Init();
            Assert.That(CqrEngineDependentComponent.Started, Is.True, "Component was not started after CqrsEngine initialization");
        }


        [Test]
        public void EventsListenerWiringTest()
        {
            var container=new WindsorContainer();
            container.Register(Component.For<IMessagingEngine>().Instance(MockRepository.GenerateMock<IMessagingEngine>()));
            container.AddFacility<CqrsFacility>();
            container.Register(Component.For<EventListener>().AsEventsListener());
            var cqrsEngine = (CqrsEngine) container.Resolve<ICqrsEngine>();
            var eventListener = container.Resolve<EventListener>();
            cqrsEngine.EventDispatcher.Dispacth("test","bc");
            Assert.That(eventListener.EventsWithBoundedContext, Is.EqualTo(new[] { Tuple.Create("test", "bc") }),"Event was not dispatched");
            Assert.That(eventListener.Events, Is.EqualTo(new[] { "test" }), "Event was not dispatched");
        }

        [Test]
        public void CommandsHandlerWiringTest()
        {
            var container=new WindsorContainer();
            container.Register(Component.For<IMessagingEngine>().Instance(MockRepository.GenerateMock<IMessagingEngine>()));
            container.AddFacility<CqrsFacility>(f=>f.BoundedContexts(BoundedContext.Local("bc")));
            container.Register(Component.For<CommandsHandler>().AsCommandsHandler("bc"));
            var cqrsEngine = (CqrsEngine)container.Resolve<ICqrsEngine>();
            cqrsEngine.Init();

            var commandsHandler = container.Resolve<CommandsHandler>();
            cqrsEngine.CommandDispatcher.Dispacth("test","bc");
            Assert.That(commandsHandler.HandledCommands, Is.EqualTo(new[] { "test" }), "Event was not dispatched");
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

 /*

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
*/
}