using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
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
        private const string TEST_QUEUE = "test.queue";
        
        [Test]
        public void SendTest()
        {
            using (var transport = new InMemoryTransport())
            {
                IProcessingGroup processingGroup = transport.CreateProcessingGroup(null);
                processingGroup.Send(TEST_QUEUE, new BinaryMessage { Bytes = new byte[] { 0x0, 0x1, 0x2 }, Type = typeof(byte[]).Name }, 0);
                processingGroup.Subscribe(TEST_QUEUE, message => Console.WriteLine("message:" + message.Type), typeof(byte[]).Name);
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

                var processingGroup = transport.CreateProcessingGroup(null);
                processingGroup.RegisterHandler(TEST_QUEUE, message => new BinaryMessage { Bytes = response, Type = typeof(byte[]).Name }, null);
                processingGroup.SendRequest(TEST_QUEUE, new BinaryMessage { Bytes = request, Type = typeof(byte[]).Name }, message =>
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
                IProcessingGroup processingGroup = transport.CreateProcessingGroup(null);
                IDisposable subscription = processingGroup.Subscribe(TEST_QUEUE, message => ev.Set(), null);
                processingGroup.Send(TEST_QUEUE, new BinaryMessage { Bytes = new byte[] { 0x0, 0x1, 0x2 }, Type = null }, 0);
                Assert.That(ev.WaitOne(500), Is.True, "Message was not delivered");
                subscription.Dispose();
                Assert.That(ev.WaitOne(500), Is.False, "Message was delivered for canceled subscription");
            }
        }

        

    }

    // ReSharper restore InconsistentNaming
    // ReSharper restore PossibleNullReferenceException
}