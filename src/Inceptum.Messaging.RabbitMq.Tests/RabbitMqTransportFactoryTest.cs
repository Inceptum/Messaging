using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inceptum.Messaging.Contract;
using Inceptum.Messaging.Transports;
using NUnit.Framework;
using RabbitMQ.Client;

namespace Inceptum.Messaging.RabbitMq.Tests
{

    class RabbitMqTransportFactoryWrapper : ITransportFactory
    {

        private RabbitMqTransportFactory m_RabbitMqTransportFactory = new RabbitMqTransportFactory();
        private Action m_OnFailure;

        public string Name
        {
            get { return "RabbitMq"; }
        }
        public ITransport Create(TransportInfo transportInfo, Action onFailure)
        {
            m_OnFailure = onFailure;
            return m_RabbitMqTransportFactory.Create(transportInfo, m_OnFailure);
        }

        public void EmulateFail()
        {
            m_OnFailure();
        }
    }
    [TestFixture]
    public class RabbitMqTransportFactoryTest
    {
        private const string TEST_QUEUE = "test.queue";
        private const string TEST_EXCHANGE = "test.exchange";
        private const string HOST = "localhost";
        private IConnection m_Connection;
        private IModel m_Channel;
        private ConnectionFactory m_Factory;


        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            m_Factory = new ConnectionFactory { HostName = HOST, UserName = "guest", Password = "guest" };
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
        public void UnknownMessageTest()
        {

            ITransportResolver transportResolver = new TransportResolver(new Dictionary<string, TransportInfo>()
            {
                {"main", new TransportInfo("localhost1,localhost", "guest", "guest", "None", "RabbitMq")},
                {"sendTransport", new TransportInfo("localhost", "guest", "guest", "None", "RabbitMq")}
            });
            var endpoint = new Endpoint("main", TEST_EXCHANGE,TEST_QUEUE, true, "json");
            var sendEndpoint = new Endpoint("sendTransport", TEST_EXCHANGE, TEST_QUEUE, true, "json");


            var factory = new RabbitMqTransportFactoryWrapper();
            using (var me = new MessagingEngine(transportResolver, factory))
            {
                me.Send(1, sendEndpoint);
                me.ResubscriptionTimeout = 100;
                var received=new ManualResetEvent(false);
                me.Subscribe<int>(endpoint, i => received.Set());
                Assert.That(received.WaitOne(1000),Is.True,"Subscription when first broker in list is not resolvable while next one is ok");
            }
        }
    }
}