using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Inceptum.Messaging.Contract;
using NUnit.Framework;
using RabbitMQ.Client;

namespace Inceptum.Messaging.RabbitMq.Tests
{
    [TestFixture]
    public class MessagingEngineTests
    {
        private const string TEST_QUEUE = "_TEST_QUEUE";
        private const string TEST_EXCHANGE = "_TEST_EXCHANGE";
        private const string HOST = "sr-tls01-s01.test-s02.uniservers.ru";

        [TestFixtureSetUp]
        public void FixtureSetUp()
        {
            var m_Factory = new ConnectionFactory { HostName = HOST, UserName = "guest", Password = "guest" };
            using (IConnection connection = m_Factory.CreateConnection())
            using (IModel channel = connection.CreateModel())
            {
                try
                {
                    channel.QueueDelete(TEST_QUEUE);
                }
                catch
                {
                }
            }
            using (IConnection connection = m_Factory.CreateConnection())
            using (IModel channel = connection.CreateModel())
            {
                try
                {
                    channel.ExchangeDelete(TEST_EXCHANGE);
                }
                catch
                {
                }
            }
            using (IConnection connection = m_Factory.CreateConnection())
            using (IModel channel = connection.CreateModel())
            {
                channel.ExchangeDeclare(TEST_EXCHANGE, "direct", false);
                channel.QueueDeclare(TEST_QUEUE, false, false, false, null);
                channel.QueueBind(TEST_QUEUE, TEST_EXCHANGE, "");
            }
        }


        [Test]
#if !DEBUG
        [NUnit.Framework.Timeout(5000)]
#endif
        public void UnknownMessageTest()
        {
            ITransportResolver transportResolver = new TransportResolver(new Dictionary<string, TransportInfo>()
                {
                    {"main",new TransportInfo(HOST, "guest", "guest", "None", "RabbitMq")}
                });
            var eq = new Endpoint("main", TEST_QUEUE, true, "json");
            var ee = new Endpoint("main", TEST_EXCHANGE, true, "json");

            using (var me = new MessagingEngine(transportResolver, new RabbitMqTransportFactory()))
            {
                me.Send("string value", ee);
            }

            using (var me = new MessagingEngine(transportResolver, new RabbitMqTransportFactory()))
            {
                me.Subscribe<int>(eq, Console.WriteLine);
                me.Subscribe<double>(eq, Console.WriteLine);
                me.Subscribe<string>(eq, Console.WriteLine);
            }
        }
    }
}
