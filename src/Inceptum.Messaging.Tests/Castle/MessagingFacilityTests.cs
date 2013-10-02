using System;
using System.Collections.Generic;
using System.Threading;
using Castle.Facilities.Logging;
using Castle.MicroKernel.Registration;
using Castle.MicroKernel.Resolvers.SpecializedResolvers;
using Castle.Windsor;
using Inceptum.Messaging.Castle;
using Inceptum.Messaging.Configuration;
using Inceptum.Messaging.Contract;
using NUnit.Framework;

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
            m_Endpoint1 = new Endpoint("transport-id-1", "destination-1",serializationFormat:"json");
            m_Endpoint2 = new Endpoint("transport-id-2", "destination-2", serializationFormat: "json");
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
                container.AddFacility<MessagingFacility>(m => m.MessagingConfiguration = m_MessagingConfiguration);
                var transportResolver = container.Resolve<ITransportResolver>();
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
                container.AddFacility<MessagingFacility>(
                    m => m.Transports = new Dictionary<string, TransportInfo>()
                        {
                            {"transport-id-1", m_Transport1},
                            {"transport-id-2", m_Transport2},
                        });
                var transportResolver = container.Resolve<ITransportResolver>();
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
                container.AddFacility(new MessagingFacility(new Dictionary<string, TransportInfo>()
                    {
                        {"transport-id-1", m_Transport1},
                        {"transport-id-2", m_Transport2},
                    }));
                var transportResolver = container.Resolve<ITransportResolver>();
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
                    .AddFacility<MessagingFacility>(f => f.MessagingConfiguration = m_MessagingConfiguration)
                    .Register(Component.For<Handler>().AsMessageHandler("endpoint-1", "endpoint-2"));
                engine = container.Resolve<IMessagingEngine>();

                engine.Send("test", m_Endpoint1);
                Thread.Sleep(30); 
                engine.Send(1, m_Endpoint1);
                Thread.Sleep(30); 
                engine.Send(DateTime.MinValue, m_Endpoint2);
                Thread.Sleep(100);
                
                Assert.That(Handler.Handled, Is.EquivalentTo(new object[] { "test", 1, DateTime.MinValue }), "message was not handled");
            }
        }
    }

    public class Handler
    {
        readonly static List<object> m_Handled = new List<object>();
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
