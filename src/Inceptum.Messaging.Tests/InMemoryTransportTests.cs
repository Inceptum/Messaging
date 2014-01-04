using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Inceptum.Messaging.InMemory;
using Inceptum.Messaging.Transports;
using NUnit.Framework;
using ThreadState = System.Threading.ThreadState;

namespace Inceptum.Messaging.Tests
{
    // ReSharper disable InconsistentNaming
    // ReSharper disable PossibleNullReferenceException

    [TestFixture]
    public class InMemoryTransportTests
    {
        private const string TEST_TOPIC = "test.queue";
        
        [Test]
        public void SendTest()
        {
            using (var transport = new InMemoryTransport())
            {
                var delivered1=new ManualResetEvent(false);
                var delivered2=new ManualResetEvent(false);
                IMessagingSession messagingSession = transport.CreateSession(null);
                messagingSession.Subscribe(TEST_TOPIC, (message,ack) =>
                    {
                        delivered1.Set();
                        Console.WriteLine("subscription1: message:" + message.Type);
                    }, typeof(byte[]).Name);

                messagingSession.Subscribe(TEST_TOPIC, (message, ack) =>
                    {
                        delivered2.Set();
                        Console.WriteLine("subscription2: message:" + message.Type);
                    }, typeof(byte[]).Name);
                 

                messagingSession.Send(TEST_TOPIC, new BinaryMessage { Bytes = new byte[] { 0x0, 0x1, 0x2 }, Type = typeof(byte[]).Name }, 0);

                
                Assert.That(delivered1.WaitOne(1000), Is.True, "message was not delivered to all subscribers");
                Assert.That(delivered2.WaitOne(1000), Is.True, "message was not delivered to all subscribers");
                Thread.Sleep(1000);
            }


        }


        [Test]
        public void RpcTest()
        {
            using (var transport = new InMemoryTransport())
            {
                var request = new byte[] { 0x0, 0x1, 0x2 };
                var response = new byte[] { 0x2, 0x1, 0x0 };
                byte[] actualResponse = null;
                var received = new ManualResetEvent(false);

                var session = transport.CreateSession(null);
                session.RegisterHandler(TEST_TOPIC, message => new BinaryMessage { Bytes = response, Type = typeof(byte[]).Name }, null);
                session.SendRequest(TEST_TOPIC, new BinaryMessage { Bytes = request, Type = typeof(byte[]).Name }, message =>
                {
                    actualResponse = message.Bytes;
                    received.Set();
                });
                Assert.That(received.WaitOne(500), Is.True, "Response was not received");
                Assert.That(actualResponse, Is.EqualTo(response), "Received response does not match sent one");
            }
        }

        [Test]
        public void UnsubscribeTest()
        {
            using (var transport = new InMemoryTransport())
            {
                var ev = new AutoResetEvent(false);
                IMessagingSession messagingSession = transport.CreateSession(null);
                IDisposable subscription = messagingSession.Subscribe(TEST_TOPIC, (message, ack) => ev.Set(), null);
                messagingSession.Send(TEST_TOPIC, new BinaryMessage { Bytes = new byte[] { 0x0, 0x1, 0x2 }, Type = null }, 0);
                Assert.That(ev.WaitOne(500), Is.True, "Message was not delivered");
                subscription.Dispose();
                Assert.That(ev.WaitOne(500), Is.False, "Message was delivered for canceled subscription");
            }
        }

        [Test]
        public void DisposeTest()
        {
            ManualResetEvent delivered = new ManualResetEvent(false);
            int deliveredMessagesCount = 0;
            var transport = new InMemoryTransport();
                IMessagingSession messagingSession = transport.CreateSession(null);
                messagingSession.Subscribe(TEST_TOPIC, (message, ack) =>
                    {
                        delivered.WaitOne();
                        Interlocked.Increment(ref deliveredMessagesCount);
                    }, null);
                messagingSession.Send(TEST_TOPIC,new BinaryMessage(), 0);
            Thread.Sleep(200);
            var task = Task.Factory.StartNew(transport.Dispose);
            Assert.That(task.Wait(200), Is.False,"transport was disposd before all message processing finished");
            delivered.Set();
            Assert.That(task.Wait(1000), Is.True, "transport was not disposd after all message processing finished");
        }


    }

    // ReSharper restore InconsistentNaming
    // ReSharper restore PossibleNullReferenceException
}