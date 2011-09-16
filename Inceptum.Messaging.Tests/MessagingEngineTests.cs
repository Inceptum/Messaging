using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using Inceptum.Core.Messaging;
using Inceptum.Messaging.Castle;
using NUnit.Framework;
using Rhino.Mocks;
using Sonic.Jms;

namespace Inceptum.Messaging.Tests
{
    internal class FakeStringSerializer : IMessageSerializer<string>
    {
        #region IMessageSerializer<string> Members

        public Message Serialize(string message, Session sendSession)
        {
            BytesMessage mess = sendSession.createBytesMessage();
            mess.writeBytes(Encoding.UTF8.GetBytes(message));
            return mess;
        }

        public string Deserialize(Message message)
        {
            var bytesMessage = message as BytesMessage;
            if (bytesMessage == null)
                return null;
            var buf = new byte[bytesMessage.getBodyLength()];
            bytesMessage.readBytes(buf);

            return Encoding.UTF8.GetString(buf);
        }

        #endregion
    }


    [TestFixture]
    public class MessagingEngineTests
    {
        #region Setup/Teardown

        [SetUp]
         public void Setup()
        {
            PurgeQueue("Test setup");
        }
        [TestFixtureTearDown]
        public void TestFixtureTearDown()
        {
            PurgeQueue("Fixture teardown");
        }
        public void PurgeQueue(string context)
        {
            QueueConnectionFactory factory = (new Sonic.Jms.Cf.Impl.QueueConnectionFactory(TransportConstants.BROKER));
            QueueConnection connection = factory.createQueueConnection(TransportConstants.USERNAME, TransportConstants.PASSWORD);
            connection.start();
          
            Session receiveSession = connection.createSession(false, SessionMode.AUTO_ACKNOWLEDGE);
            Queue receiveQueue = receiveSession.createQueue(TransportConstants.QUEUE.Substring(8));
            MessageConsumer receiver = receiveSession.createConsumer(receiveQueue, "JAILED_TAG = \'"+Environment.MachineName+"\'");
            var messageProcessed = new AutoResetEvent(false);
            receiver.setMessageListener(new GenericMessageListener(m => messageProcessed.Set()));
            int i = 0;
            while (messageProcessed.WaitOne(100))
            {
                i++;
            }
            Console.WriteLine(context+": "+i + " messages purged");
            connection.close();
        }

        #endregion

       
        public static ITransportResolver MockTransportResolver()
        {
            var resolver = MockRepository.GenerateMock<ITransportResolver>();
            resolver.Expect(r => r.GetTransport(TransportConstants.TRANSPORT_ID)).Return(
                new TransportInfo(TransportConstants.BROKER,TransportConstants.USERNAME,TransportConstants.PASSWORD)
                );
            return resolver;
        }
         

    

       
        [TestCase(TransportConstants.QUEUE, Description = "SendSubscribe queue")]
        [TestCase(TransportConstants.TOPIC, Description = "SendSubscribe topic")]
        public void RequestReplyTest(string dest)
        {
            var serializationManager = new SerializationManager();
            serializationManager.RegisterSerializer(typeof (string), new FakeStringSerializer());
          
            ITransportResolver resolver = MockTransportResolver( );
            IMessagingEngine engine = new MessagingEngine(resolver, serializationManager, JailStrategy.MachineName);
            string response;
            using (engine.RegisterHandler<string, string>(s => s == "ping" ? "pong" : "error", TransportConstants.QUEUE, TransportConstants.TRANSPORT_ID))
            {
                var stopwatch = Stopwatch.StartNew();
                response = engine.SendRequest<string, string>("ping", TransportConstants.QUEUE, TransportConstants.TRANSPORT_ID);
                stopwatch.Stop();
                Console.WriteLine("Roundtrip: "+stopwatch.ElapsedMilliseconds+"ms");
            }


            var requestFinished = new ManualResetEvent(false);
            Exception exception=null;
            var thread = new Thread(() =>
                                        {
                                            try
                                            {
                                                engine.SendRequest<string, string>("ping", TransportConstants.QUEUE, TransportConstants.TRANSPORT_ID, 500);
                                            }
                                            catch (Exception ex)
                                            {
                                                exception = ex;
                                            }

                                            requestFinished.Set();
                                        });
            thread.Start();
            Assert.That(response, Is.EqualTo("pong"));
            Assert.That(requestFinished.WaitOne(2000), Is.True, "Request has not finished after timeout.");
            Assert.That(exception, Is.Not.Null,"Request was handled after handler registration is disposed.");
            Assert.That(exception, Is.InstanceOf<TimeoutException>(),"Wrong exception was thrown on timeout.");
            if (thread.IsAlive)
                thread.Abort();
        }


        [TestCase(TransportConstants.QUEUE, Description = "SendSubscribe queue")]
        [TestCase(TransportConstants.TOPIC, Description = "SendSubscribe topic")]
        public void SendSubscribeTest(string dest)
        {
            var serializationManager = new SerializationManager();
            serializationManager.RegisterSerializer(typeof (string), new FakeStringSerializer());
           
            ITransportResolver resolver = MockTransportResolver( );
            var engine = new MessagingEngine(resolver, serializationManager, JailStrategy.MachineName);

            var recievedMessages = new List<object>();
            using (engine.Subscribe<string>(dest, TransportConstants.TRANSPORT_ID, recievedMessages.Add))
            {
                engine.Send("test", dest, TransportConstants.TRANSPORT_ID);
                engine.Send("test", dest, TransportConstants.TRANSPORT_ID);
                engine.Send("test", dest, TransportConstants.TRANSPORT_ID);
                Thread.Sleep(100);
            }
            Assert.That(recievedMessages.Count, Is.EqualTo(3), "Some messages were not received");
            Assert.That(recievedMessages, Is.EqualTo(new[] {"test", "test", "test"}), "Some messages were corrupted");
            engine.Send("test", dest, TransportConstants.TRANSPORT_ID);
            Thread.Sleep(100);
            Assert.That(recievedMessages.Count, Is.EqualTo(3),
                        "Subscription callback was called after subscription is disposed");
        }

        [Test]
        public void FacilityTest()
        {
            using (IWindsorContainer container = new WindsorContainer())
            {
                container.AddFacility("messagingFacility", new MessagingFacility(
                    new Dictionary<string, TransportInfo> { { TransportConstants.TRANSPORT_ID, new TransportInfo(TransportConstants.BROKER, TransportConstants.USERNAME,TransportConstants.PASSWORD) } }, JailStrategy.MachineName));
                container.Register(Component.For<IMessageSerializer<string>>().ImplementedBy<FakeStringSerializer>());
                var engine = container.Resolve<IMessagingEngine>();
                var ev = new ManualResetEvent(false);
                using (engine.Subscribe<string>(TransportConstants.TOPIC, TransportConstants.TRANSPORT_ID, s =>
                {
                    Console.WriteLine(s);
                    ev.Set();
                }))
                {
                    engine.Send("test",  TransportConstants.TOPIC, TransportConstants.TRANSPORT_ID);
                    Assert.That(ev.WaitOne(500), Is.True, "message was not recieved");
                }
            }

        }

      
        [Test, Ignore]
        public void FXListener()
        {

            using (IWindsorContainer container = new WindsorContainer())
            {
               
                container.AddFacility("messagingFacility", new MessagingFacility(
                       new Dictionary<string, TransportInfo>
                           {
                               { "tr", new TransportInfo("tcp://MSK-SONIC1.office.finam.ru:3506", "dev", "dev") },
                               { "tr1", new TransportInfo("tcp://msk-mqesb1.office.finam.ru:2510", "dev", "dev") }
                           })
                       );
                var engine = container.Resolve<IMessagingEngine>();
                engine.SubscribeOnTransportEvents((id, @event) => Console.WriteLine("transport " + id + " failed"));

                var ev = new ManualResetEvent(false);
                using (engine.Subscribe<Quote>("topic://finam.reuters.fx.usd.rub", "tr1", s =>
                {
                    Console.WriteLine(string.Format("USD: {0}: last:{1} bid:{2} ask:{3} close:{4}  buy:{5} sell:{6}", DateTime.Now, s.last, s.bid, s.ask, s.close, s.last * .992, s.last * 1.008));
                    ev.Set();
                }))

                using (engine.Subscribe<Quote>("topic://finam.reuters.fx.eur.rub", "tr1", s =>
                {
                    Console.WriteLine(string.Format("EUR: {0}: last:{1} bid:{2} ask:{3} close:{4}  buy:{5} sell:{6}",DateTime.Now,s.last,s.bid,s.ask,s.close, s.last*.992, s.last*1.008));
                    ev.Set();
                }))
                {
                    Thread.Sleep(3*24*60*60*1000);
                }
            }

        } 
        


        [Test]
        public void SendToOverflowenQueueFailureTest()
        {

            var serializationManager = new SerializationManager();
            serializationManager.RegisterSerializer(typeof (string), new FakeStringSerializer());
           
            ITransportResolver resolver = MockTransportResolver( );

            //Assumption: queue capacity is 1mb
            var halfMegabyteMessage = new string('a', 1 << 19);
            using (var engine = new MessagingEngine(resolver, serializationManager, JailStrategy.MachineName))
            {
                engine.Send(halfMegabyteMessage, TransportConstants.QUEUE, TransportConstants.TRANSPORT_ID);
                Exception exception=null;

                var t = new Thread(() =>
                                       {
                                           try
                                           {
                                               engine.Send(halfMegabyteMessage, TransportConstants.QUEUE, TransportConstants.TRANSPORT_ID);
                                           }
                                           catch (Exception ex)
                                           {
                                               exception = ex;
                                           }
                                       });
                t.Start();
                Assert.That(t.Join(2000), Is.True, "Exception was not thrown when queue is overflowen");
                Assert.That(exception, Is.Not.Null);
            }
        }


        [Test]
        public void TransportFailuteHandlingTest()
        {
            var serializationManager = new SerializationManager();
            serializationManager.RegisterSerializer(typeof(string), new FakeStringSerializer());
            var resolver = MockTransportResolver();
            var transportManager =new TransportManager(resolver);
            var engine = new MessagingEngine(transportManager, serializationManager, JailStrategy.MachineName);
            bool failureWasReported = false;
            engine.SubscribeOnTransportEvents((transportid, @event) => failureWasReported=true);
            transportManager.ProceesTarnsportFailure(TransportConstants.TRANSPORT_ID, new TransportInfo(TransportConstants.BROKER, TransportConstants.USERNAME, TransportConstants.PASSWORD));
            Assert.That(failureWasReported, Is.True, "Failure was not reported");
        }
    }

}