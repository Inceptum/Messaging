using System;
using System.Collections.Generic;
using System.Threading;
using Castle.Facilities.Logging;
using Castle.Facilities.Startable;
using Castle.MicroKernel.Registration;
using Castle.MicroKernel.Resolvers.SpecializedResolvers;
using Castle.Windsor;
using Inceptum.Messaging.Castle;
using Inceptum.Messaging.Configuration;
using Inceptum.Messaging.Contract;
using Inceptum.Messaging.InMemory;
using NUnit.Framework;
using Rhino.Mocks;

namespace Inceptum.Messaging.Tests.Castle
{
    [TestFixture]
    public class MessagingFacilityTests
    {
        private Endpoint m_Endpoint1;
        private Endpoint m_Endpoint2;
        private IMessagingConfiguration m_MessagingConfiguration;
        private TransportInfo m_Transport1;
        private TransportInfo m_Transport2;

        [SetUp]
        public void SetUp()
        {
            m_Endpoint1 = new Endpoint("transport-id-1", "first-destination",serializationFormat:"json");
            m_Endpoint2 = new Endpoint("transport-id-2", "second-destination", serializationFormat: "json");
            m_Transport1 = new TransportInfo("transport-1", "login1", "pwd1", "None", "InMemory");
            m_Transport2 = new TransportInfo("transport-2", "login2", "pwd1", "None", "InMemory");
            m_MessagingConfiguration = new MockMessagingConfiguration(
                new Dictionary<string, TransportInfo>()
                    {
                        {"transport-id-1", m_Transport1},
                        {"transport-id-2", m_Transport2},
                    },
                new Dictionary<string, Endpoint>
                    {
                        {"endpoint-1", m_Endpoint1},
                        {"endpoint-2", m_Endpoint2},
                    });

        }

        [Test]
        public void ConfigureTransportsViaMessagingConfigurationFacilityTest()
        {
            using (IWindsorContainer container = new WindsorContainer())
            {
                container.Kernel.Resolver.AddSubResolver(new ArrayResolver(container.Kernel));
                container.AddFacility<MessagingFacility>(m => m.WithConfiguration(m_MessagingConfiguration));
                var transportResolver = (container.Resolve<IMessagingEngine>() as MessagingEngine).TransportManager.TransportResolver;
                Assert.That(transportResolver.GetTransport("transport-id-1"), Is.Not.Null.And.EqualTo(m_Transport1));
                Assert.That(transportResolver.GetTransport("transport-id-2"), Is.Not.Null.And.EqualTo(m_Transport2));

                container.Register(Component.For<EndpointDependTestClass1>().WithEndpoints(new {endpoint1 = "endpoint-2"}));
                var test1 = container.Resolve<EndpointDependTestClass1>();
                Assert.AreEqual(m_Endpoint2.TransportId, test1.Endpoint.TransportId);
                Assert.AreEqual(m_Endpoint2.Destination, test1.Endpoint.Destination);
            }
        }

        [Test]
        public void ConfigureTransportsViaPropertiesFacilityTest()
        {
            using (IWindsorContainer container = new WindsorContainer())
            {
                container.Kernel.Resolver.AddSubResolver(new ArrayResolver(container.Kernel));
                container.AddFacility<MessagingFacility>(f => f.WithTransport("transport-id-1", m_Transport1)
                                                               .WithTransport("transport-id-2", m_Transport2));
                var transportResolver = (container.Resolve<IMessagingEngine>() as MessagingEngine).TransportManager.TransportResolver;
                Assert.That(transportResolver.GetTransport("transport-id-1"), Is.Not.Null.And.EqualTo(m_Transport1));
                Assert.That(transportResolver.GetTransport("transport-id-2"), Is.Not.Null.And.EqualTo(m_Transport2));
            }
        }

        [Test]
        public void ConfigureTransportsViaConstructorParametersFacilityTest()
        {
            using (IWindsorContainer container = new WindsorContainer())
            {
                container.Kernel.Resolver.AddSubResolver(new ArrayResolver(container.Kernel));
                container.AddFacility<MessagingFacility>(f => f.WithTransport("transport-id-1", m_Transport1).WithTransport("transport-id-2", m_Transport2));
                var transportResolver = (container.Resolve<IMessagingEngine>() as MessagingEngine).TransportManager.TransportResolver;
                Assert.That(transportResolver.GetTransport("transport-id-1"), Is.Not.Null.And.EqualTo(m_Transport1));
                Assert.That(transportResolver.GetTransport("transport-id-2"), Is.Not.Null.And.EqualTo(m_Transport2));
            }
        }      
        
        [Test]
        public void AsHandlerTest()
        {
            IMessagingEngine engine;
            using (IWindsorContainer container = new WindsorContainer())
            { 
                container.Kernel.Resolver.AddSubResolver(new ArrayResolver(container.Kernel));
                container.AddFacility<LoggingFacility>(f => f.LogUsing(LoggerImplementation.Console))
                    .AddFacility<MessagingFacility>(f => f.WithConfiguration(m_MessagingConfiguration))
                    .Register(Component.For<HandlerWithDependency>().AsMessageHandler("endpoint-1", "endpoint-2"));
                container.Register(Component.For<HandlerDependency>());
                engine = container.Resolve<IMessagingEngine>();
                engine.Send("test", m_Endpoint1);
                Thread.Sleep(30); 
                engine.Send(1, m_Endpoint1);
                Thread.Sleep(30); 
                engine.Send(DateTime.MinValue, m_Endpoint2);
                Thread.Sleep(100);
                
                Assert.That(Handler.Handled, Is.EquivalentTo(new object[] { "test", 1, DateTime.MinValue }), "message was not handled");
                Console.WriteLine(engine.GetStatistics());
            }
        }
  

        [Test]
        public void AsHandlerAndWithEndpointTest()
        {
            using (IWindsorContainer container = new WindsorContainer())
            {
                container.Kernel.Resolver.AddSubResolver(new ArrayResolver(container.Kernel));
                container.AddFacility<LoggingFacility>(f => f.LogUsing(LoggerImplementation.Console))
                    .AddFacility<MessagingFacility>(f => f.WithConfiguration(m_MessagingConfiguration))
                    .Register(Component.For<Handler>().WithEndpoints(new { someEndpoint = "endpoint-2" }).AsMessageHandler("endpoint-1"));
                var handler= container.Resolve<Handler>();
                Assert.That(handler.SomeEndpoint,Is.Not.Null);
                Assert.That(handler.SomeEndpoint.Destination.Subscribe, Is.EqualTo("second-destination"));
            }
        }

        [Test]
        public void EndToEndTest()
        {
            using (IWindsorContainer container = new WindsorContainer())
            {
                container.Kernel.Resolver.AddSubResolver(new ArrayResolver(container.Kernel));
                container.AddFacility<MessagingFacility>(f => f
                    .WithTransport("TRANSPORT_ID1", new TransportInfo("BROKER", "USERNAME", "PASSWORD", "MachineName", "InMemory"))
                    .WithTransportFactory(new InMemoryTransportFactory()));
                var factory = MockRepository.GenerateMock<ISerializerFactory>();
                factory.Expect(f => f.SerializationFormat).Return("fake");
                factory.Expect(f => f.Create<string>()).Return(new FakeStringSerializer());
                container.Register(Component.For<ISerializerFactory>().Instance(factory));
                var engine = container.Resolve<IMessagingEngine>();
                var ev = new ManualResetEvent(false);
                var endpoint = new Endpoint("TRANSPORT_ID1", "destination", serializationFormat: "fake");
                using (engine.Subscribe<string>(endpoint, s =>
                {
                    Console.WriteLine(s);
                    ev.Set();
                }))
                {
                    engine.Send("test", endpoint);
                    Assert.That(ev.WaitOne(500), Is.True, "message was not received");
                }

                Console.WriteLine(engine.GetStatistics());
            }

        }
    }

        public class HandlerDependency
        {
        }

    public class HandlerWithDependency : Handler
    {

        public HandlerWithDependency(HandlerDependency dependency)
        {
        }
    }

    public class Handler{
    
        public Endpoint SomeEndpoint { get; set; }
        readonly static List<object> m_Handled = new List<object>();

        public Handler()
        {
        }


        public Handler(Endpoint someEndpoint)
        {
            SomeEndpoint = someEndpoint;
        }

        public void Handle(string message)
        {
            m_Handled.Add(message);
        }

        public void Handle(int message)
        {
            m_Handled.Add(message);
        }

        public void Handle(DateTime message)
        {
            m_Handled.Add(message);
        }

        public static List<object> Handled
        {
            get { return m_Handled; }
        }
    }
}
