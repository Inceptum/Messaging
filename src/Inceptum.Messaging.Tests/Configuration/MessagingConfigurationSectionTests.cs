using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using Inceptum.Messaging.Configuration;
using NUnit.Framework;

namespace Inceptum.Messaging.Tests.Configuration
{
    [TestFixture]
    public class MessagingConfigurationSectionTests
    {
        [Test]
        public void GetDefaultSectionTest()
        {
            var messagingConfiguration = ConfigurationManager.GetSection("default-messaging") as IMessagingConfiguration;
            Assert.IsNotNull(messagingConfiguration);

            var transports = messagingConfiguration.GetTransports();
            Assert.IsNotEmpty(transports);
            var transport = transports["main"];

            Assert.That(transport, Is.Not.Null
                                     .And.Property("Broker").EqualTo("localhost")
                                     .And.Property("Login").EqualTo("guest")
                                     .And.Property("Password").EqualTo("guest")
                                     .And.Property("Messaging").EqualTo("Sonic")
                                     .And.Property("JailStrategyName").EqualTo("None")
                );

            var endpoint1 = messagingConfiguration.GetEndpoints()["endpoint1"];
            Assert.That(endpoint1, Is.Not.Null
                                     .And.Property("TransportId").EqualTo("main")
                                     .And.Property("Destination").EqualTo("queue1")
                                     .And.Property("SharedDestination").EqualTo(false)
                );
        }

        [Test]
        public void GetEmptySectionTest()
        {
            var messagingConfiguration = ConfigurationManager.GetSection("empty-messaging") as IMessagingConfiguration;
            Assert.IsNotNull(messagingConfiguration);

            var transports = messagingConfiguration.GetTransports();
            Assert.IsEmpty(transports);
            CollectionAssert.IsEmpty(messagingConfiguration.GetEndpoints());
        }

        [Test]
        public void GetNotEmptySectionTest()
        {
            var messagingConfiguration = ConfigurationManager.GetSection("one-transport-messaging") as IMessagingConfiguration;
            Assert.IsNotNull(messagingConfiguration);
            var transports = messagingConfiguration.GetTransports();
            Assert.IsNotEmpty(transports);
            var transport = transports["main"];

            Assert.That(transport, Is.Not.Null
                                     .And.Property("Broker").EqualTo("localhost")
                                     .And.Property("Login").EqualTo("guest")
                                     .And.Property("Password").EqualTo("guest")
                                     .And.Property("Messaging").EqualTo("RabbitMq")
                                     .And.Property("JailStrategyName").EqualTo("None")
                );

            var endpoint1 = messagingConfiguration.GetEndpoints()["endpoint1"];
            Assert.That(endpoint1, Is.Not.Null
                                     .And.Property("TransportId").EqualTo("main")
                                     .And.Property("Destination").EqualTo("queue1")
                                     .And.Property("SharedDestination").EqualTo(true)
                );

            var endpoint2 = messagingConfiguration.GetEndpoints()["endpoint2"];
            Assert.That(endpoint2, Is.Not.Null
                                     .And.Property("TransportId").EqualTo("main")
                                     .And.Property("Destination").EqualTo("queue2")
                                     .And.Property("SharedDestination").EqualTo(false)
                );

            var endpoint3 = messagingConfiguration.GetEndpoints()["endpoint3"];
            Assert.That(endpoint3, Is.Not.Null
                                     .And.Property("TransportId").EqualTo("main")
                                     .And.Property("Destination").EqualTo("queue3")
                                     .And.Property("SharedDestination").EqualTo(false)
                );
        }
    }
}
