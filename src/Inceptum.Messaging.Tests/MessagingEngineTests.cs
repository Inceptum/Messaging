using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Inceptum.Messaging.Contract;
using Inceptum.Messaging.InMemory;
using NUnit.Framework;
using Rhino.Mocks;

namespace Inceptum.Messaging.Tests
{
    // ReSharper disable InconsistentNaming
    // ReSharper disable PossibleNullReferenceException

    [TestFixture]
    public class MessagingEngineTests
    {
        private abstract class TransportConstants
        {
            public const string QUEUE1="queue1";
            public const string QUEUE2="queue2";
            public const string TRANSPORT_ID1 = "tr1";
            public const string TRANSPORT_ID2 = "tr2";
            public const string USERNAME = "test";
            public const string PASSWORD = "test";
            public const string BROKER = "test";
        }

        private static ITransportResolver MockTransportResolver()
        {
            var resolver = MockRepository.GenerateMock<ITransportResolver>();
            resolver.Expect(r => r.GetTransport(TransportConstants.TRANSPORT_ID1)).Return(new TransportInfo(TransportConstants.BROKER, TransportConstants.USERNAME, TransportConstants.PASSWORD, "MachineName", "InMemory"));
            resolver.Expect(r => r.GetTransport(TransportConstants.TRANSPORT_ID2)).Return(new TransportInfo(TransportConstants.BROKER, TransportConstants.USERNAME, TransportConstants.PASSWORD, "MachineName", "InMemory"));
            return resolver;
        }
       
        [Test]
        public void TransportFailureHandlingTest()
        {
            var resolver = MockTransportResolver();
            using (var engine = new MessagingEngine(resolver, new InMemoryTransportFactory()))
            {
                engine.SerializationManager.RegisterSerializer("fake", typeof(string), new FakeStringSerializer());
                int failureWasReportedCount = 0;
                engine.SubscribeOnTransportEvents((transportId, @event) => failureWasReportedCount++);

                //need for transportManager to start tracking transport failures for these ids
                engine.TransportManager.GetMessagingSession(TransportConstants.TRANSPORT_ID1, "test");
                engine.TransportManager.GetMessagingSession(TransportConstants.TRANSPORT_ID2, "test");

                engine.TransportManager.ProcessTransportFailure(
                    new TransportInfo(TransportConstants.BROKER,
                        TransportConstants.USERNAME,
                        TransportConstants.PASSWORD, "MachineName", "InMemory"));
                Assert.That(failureWasReportedCount, Is.GreaterThan(0), "Failure was not reported");
                Assert.That(failureWasReportedCount, Is.EqualTo(2), "Failure was not reported for all ids");
            }
        }

        [Test]
        public void ByDefaultEachDestinationIsSubscribedOnDedicatedThreadTest()
        {
            ITransportResolver resolver = MockTransportResolver();
            using (var engine = new MessagingEngine(resolver, new InMemoryTransportFactory()))
            {
                engine.SerializationManager.RegisterSerializer("fake", typeof(string), new FakeStringSerializer());

                var queue1MessagesThreadIds = new List<int>();
                var queue2MessagesThreadIds = new List<int>();
                var messagesCounter = 0;
                var allMessagesAreRecieved=new ManualResetEvent(false);
                using (engine.Subscribe<string>(new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.QUEUE1, serializationFormat: "fake"), s =>
                {
                    queue1MessagesThreadIds.Add(Thread.CurrentThread.ManagedThreadId);
                    Console.WriteLine(Thread.CurrentThread.ManagedThreadId);
                    if (Interlocked.Increment(ref messagesCounter) == 6) allMessagesAreRecieved.Set();
                }))
                using (engine.Subscribe<string>(new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.QUEUE2, serializationFormat: "fake"), s =>
                {
                    queue2MessagesThreadIds.Add(Thread.CurrentThread.ManagedThreadId);
                    Console.WriteLine(Thread.CurrentThread.ManagedThreadId);
                    if (Interlocked.Increment(ref messagesCounter) == 6) allMessagesAreRecieved.Set();
                }))
                {
                    engine.Send("test", new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.QUEUE1, serializationFormat: "fake"));
                    engine.Send("test", new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.QUEUE2, serializationFormat: "fake"));
                    engine.Send("test", new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.QUEUE1, serializationFormat: "fake"));
                    engine.Send("test", new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.QUEUE2, serializationFormat: "fake"));
                    engine.Send("test", new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.QUEUE1, serializationFormat: "fake"));
                    engine.Send("test", new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.QUEUE2, serializationFormat: "fake"));
                    allMessagesAreRecieved.WaitOne(1000);
                }
                Assert.That(queue1MessagesThreadIds.Distinct().Any(), Is.True, "Messages were not processed");
                Assert.That(queue2MessagesThreadIds.Distinct().Any(), Is.True, "Messages were not processed");
                Assert.That(queue1MessagesThreadIds.Distinct().Count(), Is.EqualTo(1), "Messages from one subscription were processed in more then 1 thread");
                Assert.That(queue2MessagesThreadIds.Distinct().Count(), Is.EqualTo(1), "Messages from one subscription were processed in more then 1 thread");
                Assert.That(queue1MessagesThreadIds.First() != queue2MessagesThreadIds.First(), Is.True, "Messages from different subscriptions were processed one thread");
            }
        }

    }

    // ReSharper restore InconsistentNaming
    // ReSharper restore PossibleNullReferenceException
}