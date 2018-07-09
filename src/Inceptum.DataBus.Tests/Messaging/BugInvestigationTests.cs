using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Castle.Core.Logging;
using Inceptum.DataBus.Messaging;
using Inceptum.Messaging;
using Inceptum.Messaging.Contract;
using Inceptum.Messaging.InMemory;
using NUnit.Framework;

namespace Inceptum.DataBus.Tests.Messaging
{
    [TestFixture]
    public class BugInvestigationTests
    {
        private class FeedProvider1 : MessagingFeedWithInitializationBase<string, string, string, string>
        {
            public FeedProvider1(IMessagingEngine messagingEngine, long initTimeout = 30000) : base(messagingEngine, initTimeout)
            {
            }

            protected override Endpoint GetEndpoint(string context)
            {
                return new Endpoint("tr1", "queue1", false, "txt");
            }

            protected override IEnumerable<string> ExtractInitialData(string response, string context)
            {
                return new[] {response};
            }

            protected override string GetInitRequest(string context)
            {
                Console.WriteLine("FeedProvider1: GetInitRequest: " + context);
                return context;
            }
        }

        private class FeedProvider2 : MessagingFeedWithInitializationBase<string, string, string, string>
        {
            public FeedProvider2(IMessagingEngine messagingEngine, long initTimeout = 30000)
                : base(messagingEngine, initTimeout)
            {
            }

            protected override Endpoint GetEndpoint(string context)
            {
                return new Endpoint("tr2", "queue2", false, "txt");
            }

            protected override IEnumerable<string> ExtractInitialData(string response, string context)
            {
                return new[] { response };
            }

            protected override string GetInitRequest(string context)
            {
                Console.WriteLine("FeedProvider2: GetInitRequest: " + context);
                return context;
            }
        }

        private class FakeStringSerializer : IMessageSerializer<string>
        {
            #region IMessageSerializer<string> Members

            public byte[] Serialize(string message)
            {
                return Encoding.UTF8.GetBytes(message);
            }

            public string Deserialize(byte[] message)
            {
                return Encoding.UTF8.GetString(message);
            }

            #endregion
        }

        [Test]
        [Ignore("Ignored")]
        public void MultipleInitializationsRaceConditionTest()
        {
            var transportResolver = new TransportResolver(new Dictionary<string, TransportInfo>
                {
                    {"tr1", new TransportInfo("host", "guest", "guest", null, "InMemory")},
                    {"tr2", new TransportInfo("host", "guest", "guest", null, "InMemory")}
                });
            var messagingEngine = new MessagingEngine(transportResolver, new ITransportFactory[] { new InMemoryTransportFactory() });
            messagingEngine.SerializationManager.RegisterSerializer("txt", typeof(string), new FakeStringSerializer());
            var fp1 = new FeedProvider1(messagingEngine);
            var consoleLogger = new ConsoleLogger();
            fp1.Logger = consoleLogger;
            var fp2 = new FeedProvider2(messagingEngine);
            fp2.Logger = consoleLogger;

            var databus = new DataBus();
            databus.RegisterFeedProvider("FP1", fp1);
            databus.RegisterFeedProvider("FP2", fp2);
            var disposable1 = databus.Channel<string>("FP1").Feed("context1").Subscribe(Console.WriteLine);
            var disposable2 = databus.Channel<string>("FP2").Feed("context2").Subscribe(Console.WriteLine);

            Thread.Sleep(10000);

            disposable1.Dispose();
            disposable2.Dispose();
        }
    }
}