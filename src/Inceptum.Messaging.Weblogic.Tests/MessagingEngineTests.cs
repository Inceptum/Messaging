using System;
using System.Collections.Generic;
using System.Threading;
using Inceptum.Messaging.Contract;
using Inceptum.Messaging.Weblogic.Tests.Fakes;
using NUnit.Framework;

namespace Inceptum.Messaging.Weblogic.Tests
{
    [TestFixture, Ignore("Uses real weblogic test environment")]
    public class MessagingEngineTests
    {
        [Test]
        public void Send()
        {
            ITransportResolver resolver = ObjectMother.MockTransportResolver("Weblogic");
            using (IMessagingEngine engine = new MessagingEngine(resolver, new WeblogicTransportFactory()))
            {
                engine.SerializationManager.RegisterSerializer("fake", typeof (string), new FakeStringSerializer());
                engine.Send("hello world", new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.QUEUE1, serializationFormat: "fake"));
            }
        }

        [Test]
        public void Subscribe()
        {
            ITransportResolver resolver = ObjectMother.MockTransportResolver("Weblogic");
            using (IMessagingEngine engine = new MessagingEngine(resolver, new WeblogicTransportFactory()))
            {
                engine.SerializationManager.RegisterSerializer("fake", typeof(string), new FakeStringSerializer());
                var receivedMessages = new List<object>();
                using (engine.Subscribe<string>(new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.QUEUE1, serializationFormat: "fake"), receivedMessages.Add))
                {
                    Thread.Sleep(1000);
                }

                foreach (var receivedMessage in receivedMessages)
                {
                    Console.WriteLine(receivedMessage);
                }
            }
        }

        private class WeblogicCustomTransportFactory : WeblogicTransportFactoryAbstract
        {
            public WeblogicCustomTransportFactory()
                : base("WeblogicMoneymail",
                new Dictionary<string, string> { { "operType", "clientAndAgreementHBF" }, {"operSubtype","FinamMsgPaymentDogJurRq" }},
                new Dictionary<string, string> { { "operType", "clientAndAgreementHBF" }})
            {
            }
        }

        [Test]
        public void SendWithCustomProperties()
        {
            ITransportResolver resolver = ObjectMother.MockTransportResolver("WeblogicMoneymail");
            using (IMessagingEngine engine = new MessagingEngine(resolver, new WeblogicCustomTransportFactory()))
            {
                engine.SerializationManager.RegisterSerializer("fake", typeof(string), new FakeStringSerializer());
                engine.Send("hello world", new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.QUEUE1, serializationFormat: "fake"));
            }            
        }

        [Test]
        public void SendSubscribeWithTimeToLive()
        {
            ITransportResolver resolver = ObjectMother.MockTransportResolver("Weblogic");
            using (IMessagingEngine engine = new MessagingEngine(resolver, new WeblogicTransportFactory()))
            {
                engine.SerializationManager.RegisterSerializer("fake", typeof(string), new FakeStringSerializer());
                engine.Send("ping1", new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.QUEUE1, serializationFormat: "fake"), 200);
                engine.Send("ping2", new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.QUEUE1, serializationFormat: "fake"), 1000);

                Thread.Sleep(300);

                var receivedMessages = new List<object>();
                using (engine.Subscribe<string>(new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.QUEUE1, serializationFormat: "fake"), receivedMessages.Add))
                {
                    Thread.Sleep(1000);
                }

                Assert.AreEqual(1, receivedMessages.Count);
                Assert.AreEqual("ping2", receivedMessages[0]);
            }
        }
    }
}