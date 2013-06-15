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
        [ExpectedException(ExpectedMessage = "Component can not be projection and commands handler simultaneousely")]
        public void ComponentCanNotBeProjectionAndCommandsHandlerSimultaneousely()
        {
            var container = new WindsorContainer();
            container.AddFacility<CqrsFacility>(f=>f.BoundedContexts(LocalBoundedContext.Named("bc")));
            container.Register(Component.For<CommandsHandler>().AsCommandsHandler("bc").AsProjection("bc","remote"));
        }


        [Test]
        public void CqrsEngineIsResolvableAsDependencyOnlyAfterBootstrapTest()
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
            container.Resolve<ICqrsEngineBootstrapper>().Start();

            container.Resolve<CqrEngineDependentComponent>();
            Assert.That(reslovedCqrsDependentComponentBeforeInit,Is.False,"ICqrsEngine was resolved as dependency before it was initialized");
        }

        [Test]
        public void CqrsEngineIsResolvableAsDependencyOnlyAfterBootstrapStartableFacilityTest()
        {
            var container = new WindsorContainer();
            container.AddFacility<CqrsFacility>()
                     .AddFacility<StartableFacility>();// (f => f.DeferredTryStart());
            container.Register(Component.For<IMessagingEngine>().Instance(MockRepository.GenerateMock<IMessagingEngine>()));
            container.Register(Component.For<CqrEngineDependentComponent>().StartUsingMethod("Start"));
            Assert.That(CqrEngineDependentComponent.Started,Is.False,"Component was started before CqrsEngine initialization");
            container.Resolve<ICqrsEngineBootstrapper>().Start();
            Assert.That(CqrEngineDependentComponent.Started, Is.True, "Component was not started after CqrsEngine initialization");
        }


        [Test]
        public void ProjectionWiringTest()
        {
            var container=new WindsorContainer();
            container.Register(Component.For<IMessagingEngine>().Instance(MockRepository.GenerateMock<IMessagingEngine>()))
                .AddFacility<CqrsFacility>(f => f.BoundedContexts(LocalBoundedContext.Named("local"), RemoteBoundedContext.Named("remote")))
                .Register(Component.For<EventListener>().AsProjection("local", "remote"))
                .Resolve<ICqrsEngineBootstrapper>().Start(); 

            var cqrsEngine = (CqrsEngine) container.Resolve<ICqrsEngine>();
            var eventListener = container.Resolve<EventListener>();
            cqrsEngine.BoundedContexts.First(c => c.Name == "remote").EventDispatcher.Dispacth("test");
            Assert.That(eventListener.EventsWithBoundedContext, Is.EquivalentTo(new[] { Tuple.Create("test", "remote") }),"Event was not dispatched");
            Assert.That(eventListener.Events, Is.EquivalentTo(new[] { "test" }), "Event was not dispatched");
        }

        [Test]
        public void CommandsHandlerWiringTest()
        {
            var container=new WindsorContainer();
            container
                .Register(Component.For<IMessagingEngine>().Instance(MockRepository.GenerateMock<IMessagingEngine>()))
                .AddFacility<CqrsFacility>(f => f.BoundedContexts(LocalBoundedContext.Named("bc")))
                .Register(Component.For<CommandsHandler>().AsCommandsHandler("bc"))
                .Resolve<ICqrsEngineBootstrapper>().Start();
            var cqrsEngine = (CqrsEngine)container.Resolve<ICqrsEngine>();
            var commandsHandler = container.Resolve<CommandsHandler>();
            cqrsEngine.BoundedContexts.First(c=>c.Name=="bc").CommandDispatcher.Dispacth("test");
            Assert.That(commandsHandler.HandledCommands, Is.EqualTo(new[] { "test" }), "Command was not dispatched");
        }


        public void SyntaxTest()
        {
            var container = new WindsorContainer();
            container.Register(Component.For<IMessagingEngine>().Instance(MockRepository.GenerateMock<IMessagingEngine>()));
            container.AddFacility<CqrsFacility>(f => f.BoundedContexts(
                LocalBoundedContext.Named("local")
                                   .PublishingEvents(typeof (int)).To("events").RoutedTo("events")
                                   .ListeningCommands(typeof (string)).On("commands1").RoutedFromSameEndpoint()
                                   .ListeningCommands(typeof (DateTime)).On("commands2").RoutedFromSameEndpoint()
                                   .WithCommandsHandler<CommandHandler>(),
                LocalBoundedContext.Named("projections")));

            container.Register(
                Component.For<TestSaga>().AsSaga("local", "projections"),
                Component.For<CommandHandler>().AsCommandsHandler("local"),
                Component.For<EventsListener>().AsProjection("projections", "local")
                );
        }


    }

    public class FakeEndpointResolver : IEndpointResolver
    {
        private readonly Dictionary<string, Endpoint> m_Endpoints = new Dictionary<string, Endpoint>
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