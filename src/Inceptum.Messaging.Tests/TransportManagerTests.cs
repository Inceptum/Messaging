using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Castle.Core.Internal;
using Inceptum.Messaging.Contract;
using Inceptum.Messaging.InMemory;
using Inceptum.Messaging.Transports;
using NUnit.Framework;
using Rhino.Mocks;

namespace Inceptum.Messaging.Tests
{
    // ReSharper disable InconsistentNaming
    // ReSharper disable PossibleNullReferenceException

    [TestFixture]
    public class TransportManagerTests
    {
        private class TransportConstants
        {
            public const string TRANSPORT_ID1 = "tr1";
            public const string TRANSPORT_ID2 = "tr2";
            public const string USERNAME = "test";
            public const string PASSWORD = "test";
            public const string BROKER = "test";
        }

        public static ITransportResolver MockTransportResolver()
        {
            var resolver = MockRepository.GenerateMock<ITransportResolver>();
            resolver.Expect(r => r.GetTransport(TransportConstants.TRANSPORT_ID1)).Return(new TransportInfo(TransportConstants.BROKER, TransportConstants.USERNAME, TransportConstants.PASSWORD, "MachineName", "InMemory") );
            resolver.Expect(r => r.GetTransport(TransportConstants.TRANSPORT_ID2)).Return(new TransportInfo(TransportConstants.BROKER, TransportConstants.USERNAME, TransportConstants.PASSWORD, "MachineName", "InMemory") );
            return resolver;
        }
        [Test]
        public void TransportFailureHandlingTest()
        {
            var resolver = MockTransportResolver();
            var transportManager = new TransportManager(resolver, new InMemoryTransportFactory());
            using (var engine = new MessagingEngine(transportManager))
            {
                engine.SerializationManager.RegisterSerializer("fake", typeof(string), new FakeStringSerializer());
                int failureWasReportedCount = 0;
                engine.SubscribeOnTransportEvents((transportId, @event) => failureWasReportedCount++);

                //need for transportManager to start tracking transport failures for these ids
                transportManager.GetMessagingSession(TransportConstants.TRANSPORT_ID1, "test");
                transportManager.GetMessagingSession(TransportConstants.TRANSPORT_ID2, "test");

                transportManager.ProcessTransportFailure(
                    new TransportInfo(TransportConstants.BROKER,
                        TransportConstants.USERNAME,
                        TransportConstants.PASSWORD, "MachineName", "InMemory"));
                Assert.That(failureWasReportedCount, Is.GreaterThan(0), "Failure was not reported");
                Assert.That(failureWasReportedCount, Is.EqualTo(2), "Failure was not reported for all ids");
            }
        }
        [Test]
        public void ConcurrentTransportResolutionTest()
        {
            var resolver = MockTransportResolver();
            var transportManager = new TransportManager(resolver, new InMemoryTransportFactory());
            var start = new ManualResetEvent(false);
            int errorCount = 0;
            int attemptCount = 0;

            foreach (var i in Enumerable.Range(1, 10))
            {
                int threadNumber = i;
                var thread = new Thread(() =>
                {
                    start.WaitOne();
                    try
                    {
                        var transport = transportManager.GetMessagingSession(TransportConstants.TRANSPORT_ID1, "test");
                        Console.WriteLine(threadNumber + ". " + transport);
                        Interlocked.Increment(ref attemptCount);
                    }
                    catch (Exception)
                    {
                        Interlocked.Increment(ref errorCount);
                    }
                });
                thread.Start();
            }


            start.Set();
            while (attemptCount < 10)
            {
                Thread.Sleep(50);
            }

            Assert.That(errorCount, Is.EqualTo(0));


        }

    }

    // ReSharper restore InconsistentNaming
    // ReSharper restore PossibleNullReferenceException
}