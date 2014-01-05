using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    }

    // ReSharper restore InconsistentNaming
    // ReSharper restore PossibleNullReferenceException
}