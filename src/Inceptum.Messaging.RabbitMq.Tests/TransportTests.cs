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
        [SetUp]
        public void Setup()
        {
             
        }

        [Test]
        public void SendTest()
        {
            var factory = new ConnectionFactory { HostName = "localhost", UserName = "guest", Password = "guest" };
            var connection = factory.CreateConnection();
            var channel = connection.CreateModel();
            channel.QueueDeclare("test", false, false, true, null);
            channel.ExchangeDeclare("test", "direct");
            channel.QueueBind("test", "test","");

            var transport = new Transport("localhost", "guest", "guest");
            transport.Send("test",new BinaryMessage{Bytes =new byte[]{0x0,0x1,0x2},Type = typeof(byte[]).Name},0);
            transport.Subscribe("test", message =>
                {
                    Console.WriteLine("message:"+message.Type);
                },null);
        }


        [Test]
        public void MessageOfUnknownTypeShouldPauseProcessingTillCorrespondingHandlerIsRegisteredTest()
        {
            var factory = new ConnectionFactory { HostName = "localhost", UserName = "guest", Password = "guest" };
            var connection = factory.CreateConnection();
            var channel = connection.CreateModel();
            channel.ExchangeDeclare("test.exchange", "direct");
            channel.QueueDeclare("test.queue", false, false, true, null);
            channel.QueueBind("test.queue", "test.exchange", "");
            using (var transport = new Transport("localhost", "guest", "guest"))
            {
                var type1Received = new AutoResetEvent(false);
                var type2Received = new AutoResetEvent(false);

                transport.Subscribe("test.queue", message => type1Received.Set(), "type1");

                transport.Send("test.exchange", new BinaryMessage { Bytes = new byte[] { 0x0, 0x1, 0x2 }, Type = "type1" }, 0);
                Assert.That(type1Received.WaitOne(2222500), Is.True, "Message of subscribed type was not delivered");
                transport.Send("test.exchange", new BinaryMessage { Bytes = new byte[] { 0x0, 0x1, 0x2 }, Type = "type2" }, 0);
                transport.Send("test.exchange", new BinaryMessage { Bytes = new byte[] { 0x0, 0x1, 0x2 }, Type = "type1" }, 0);
                Assert.That(type1Received.WaitOne(500), Is.False, "Message of not subscribed type has not paused processing");
                transport.Subscribe("test.queue", message => type2Received.Set(), "type2");
                Assert.That(type1Received.WaitOne(500), Is.True, "Processing was not resumed after handler for unknown message type was registered");
                Assert.That(type2Received.WaitOne(500), Is.True, "Processing was not resumed after handler for unknown message type was registered");

            }
        }


        [Test]
        public void UnknownMessageTypHandlereWaitingDoesNotPreventTransportDisposeTest()
        {
            var factory = new ConnectionFactory { HostName = "localhost", UserName = "guest", Password = "guest" };
            using (var connection = factory.CreateConnection())
            {
                var channel = connection.CreateModel();
                channel.ExchangeDeclare("test.exchange", "direct");
                channel.QueueDeclare("test.queue", false, false, true, null);
                channel.QueueBind("test.queue", "test.exchange", "");
                var received=new ManualResetEvent(false);
                Thread connectionThread = null;
                using (var transport = new Transport("localhost", "guest", "guest"))
                {
                    Console.WriteLine(Process.GetCurrentProcess().Threads.Count);
                    transport.Subscribe("test.queue", message =>
                        {
                            connectionThread = Thread.CurrentThread;
                            received.Set();
                        }, "type1");
                    transport.Send("test.exchange", new BinaryMessage { Bytes = new byte[] { 0x0, 0x1, 0x2 }, Type = "type1" }, 0);
                    Assert.That(received.WaitOne(500), Is.True, "Message was not delivered");
                    transport.Send("test.exchange", new BinaryMessage { Bytes = new byte[] { 0x0, 0x1, 0x2 }, Type = "type2" }, 0);

                }
                Assert.That(connectionThread.ThreadState, Is.EqualTo(ThreadState.Stopped),"Processing thread is still active in spite of transport dispose");
            }
        }

        [Test]
        public void HandlerWaitStopsAndMessageOfUnknownTypeReturnsToQueueOnUnsubscribeTest()
        {
            var factory = new ConnectionFactory { HostName = "localhost", UserName = "guest", Password = "guest" };
            var connection = factory.CreateConnection();
            var channel = connection.CreateModel();
            channel.ExchangeDeclare("test.exchange", "direct");
            channel.QueueDeclare("test.queue", false, false, false, null);
            channel.QueueBind("test.queue", "test.exchange", "");
            using (var transport = new Transport("localhost", "guest", "guest"))
            {
                var received = new AutoResetEvent(false);
                var subscription = transport.Subscribe("test.queue", message => received.Set(), "type2");
                transport.Send("test.exchange", new BinaryMessage { Bytes = new byte[] { 0x0, 0x1, 0x2 }, Type = "type1" }, 0);
                Assert.That(received.WaitOne(500), Is.False, "Message of not subscribed type has not paused processing");
                subscription.Dispose();
                transport.Subscribe("test.queue", message => received.Set(), "type1");
                Assert.That(received.WaitOne(500), Is.True, "Message was not returned to queue");
            }
            channel.QueueDelete("test.queue");


        }

    }
}
