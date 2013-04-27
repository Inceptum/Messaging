using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Inceptum.Messaging.Transports;
using NUnit.Framework;
using RabbitMQ.Client;
using ThreadState = System.Threading.ThreadState;

namespace Inceptum.Messaging.RabbitMq.Tests
{
    [TestFixture]
    public class TransportTests
    {
        private const string TEST_QUEUE = "test.queue";
        private const string TEST_EXCHANGE = "test.exchange";
        private IConnection m_Connection;
        private IModel m_Channel;
        private ConnectionFactory m_Factory;


        [TestFixtureSetUp]
        public void FixtureSetUp()
        {
            m_Factory = new ConnectionFactory { HostName = "localhost", UserName = "guest", Password = "guest" };
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


        [SetUp]
        public void Setup()
        {
            m_Connection = m_Factory.CreateConnection();
            m_Channel = m_Connection.CreateModel();
            m_Channel.QueuePurge(TEST_QUEUE);
        }        
        
        [TearDown]
        public void TearDown()
        {
            m_Channel.Dispose();
            m_Connection.Dispose();
             
        }

        [Test]
        public void SendTest()
        {
            using (var transport = new Transport("localhost", "guest", "guest"))
            {
                transport.Send(TEST_EXCHANGE, new BinaryMessage {Bytes = new byte[] {0x0, 0x1, 0x2}, Type = typeof (byte[]).Name}, 0);
                transport.Subscribe(TEST_QUEUE, message => Console.WriteLine("message:" + message.Type), typeof (byte[]).Name);
            }
        }


        [Test]
        public void MessageOfUnknownTypeShouldPauseProcessingTillCorrespondingHandlerIsRegisteredTest()
        {
            using (var transport = new Transport("localhost", "guest", "guest"))
            {
                var type1Received = new AutoResetEvent(false);
                var type2Received = new AutoResetEvent(false);

                transport.Subscribe(TEST_QUEUE, message => type1Received.Set(), "type1");

                transport.Send(TEST_EXCHANGE, new BinaryMessage { Bytes = new byte[] { 0x0, 0x1, 0x2 }, Type = "type1" }, 0);
                Assert.That(type1Received.WaitOne(2222500), Is.True, "Message of subscribed type was not delivered");
                transport.Send(TEST_EXCHANGE, new BinaryMessage { Bytes = new byte[] { 0x0, 0x1, 0x2 }, Type = "type2" }, 0);
                transport.Send(TEST_EXCHANGE, new BinaryMessage { Bytes = new byte[] { 0x0, 0x1, 0x2 }, Type = "type1" }, 0);
                Assert.That(type1Received.WaitOne(500), Is.False, "Message of not subscribed type has not paused processing");
                transport.Subscribe(TEST_QUEUE, message => type2Received.Set(), "type2");
                Assert.That(type1Received.WaitOne(500), Is.True, "Processing was not resumed after handler for unknown message type was registered");
                Assert.That(type2Received.WaitOne(500), Is.True, "Processing was not resumed after handler for unknown message type was registered");

            }
        }


        [Test]
        public void UnknownMessageTypHandlereWaitingDoesNotPreventTransportDisposeTest()
        {
 
                var received=new ManualResetEvent(false);
                Thread connectionThread = null;
                using (var transport = new Transport("localhost", "guest", "guest"))
                {
                    transport.Subscribe(TEST_QUEUE, message =>
                        {
                            connectionThread = Thread.CurrentThread;
                            received.Set();
                        }, "type1");
                    transport.Send(TEST_EXCHANGE, new BinaryMessage { Bytes = new byte[] { 0x0, 0x1, 0x2 }, Type = "type1" }, 0);
                    Assert.That(received.WaitOne(100), Is.True, "Message was not delivered");
                    transport.Send(TEST_EXCHANGE, new BinaryMessage { Bytes = new byte[] { 0x0, 0x1, 0x2 }, Type = "type2" }, 0);

                }
                Assert.That(connectionThread.ThreadState, Is.EqualTo(ThreadState.Stopped),"Processing thread is still active in spite of transport dispose");
        }

        [Test]
        public void HandlerWaitStopsAndMessageOfUnknownTypeReturnsToQueueOnUnsubscribeTest()
        {
            using (var transport = new Transport("localhost", "guest", "guest"))
            {
                var received = new AutoResetEvent(false);
                var subscription = transport.Subscribe(TEST_QUEUE, message => received.Set(), "type2");
                transport.Send(TEST_EXCHANGE, new BinaryMessage { Bytes = new byte[] { 0x0, 0x1, 0x2 }, Type = "type1" }, 0);
                Assert.That(received.WaitOne(500), Is.False, "Message of not subscribed type has not paused processing");
                subscription.Dispose();
                transport.Subscribe(TEST_QUEUE, message => received.Set(), "type1");
                Assert.That(received.WaitOne(500), Is.True, "Message was not returned to queue");
            }
        }

    }
}
