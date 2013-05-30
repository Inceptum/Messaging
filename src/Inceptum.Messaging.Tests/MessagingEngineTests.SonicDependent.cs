using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Castle.Core.Logging;
using Castle.MicroKernel.Registration;
using Castle.MicroKernel.Resolvers.SpecializedResolvers;
using Castle.Windsor;
using Inceptum.Messaging.Castle;
using Inceptum.Messaging.Contract;
using Inceptum.Messaging.Serialization;
using Inceptum.Messaging.Sonic;
using NUnit.Framework;
using Rhino.Mocks;
using Sonic.Jms;
using Connection = Sonic.Jms.Ext.Connection;
using ConnectionFactory = Sonic.Jms.Ext.ConnectionFactory;
using QueueSession = Sonic.Jms.Ext.QueueSession;
using QueueConnectionFactory = Sonic.Jms.Cf.Impl.QueueConnectionFactory;
namespace Inceptum.Messaging.Tests
{
    internal class FakeStringSerializer : IMessageSerializer<string>
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
    internal class FakeIntSerializer : IMessageSerializer<int>
    {
        #region IMessageSerializer<int> Members

        public byte[] Serialize(int message)
        {
            return Encoding.UTF8.GetBytes(message.ToString());
        }

        public int Deserialize(byte[] message)
        {
            return int.Parse(Encoding.UTF8.GetString(message));
        }

        #endregion
    }


    [TestFixture]
    public class MessagingEngineTests1
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
            var factory = new QueueConnectionFactory();
            factory.setConnectionURLs(TransportConstants.BROKER);
            QueueConnection connection = factory.createQueueConnection(TransportConstants.USERNAME,
                                                                       TransportConstants.PASSWORD);
            connection.start();

            Session receiveSession = connection.createSession(false, SessionMode.AUTO_ACKNOWLEDGE);
            PurgeQueue(context, receiveSession, TransportConstants.QUEUE1);
            PurgeQueue(context, receiveSession, TransportConstants.QUEUE2);
            connection.close();
        }

        private static void PurgeQueue(string context, Session receiveSession, string name)
        {
            Queue receiveQueue = receiveSession.createQueue(name.Substring(8));
            MessageConsumer receiver = receiveSession.createConsumer(receiveQueue,
                                                                     "JAILED_TAG = \'" + Environment.MachineName + "\'");
            var messageProcessed = new AutoResetEvent(false);
            receiver.setMessageListener(new GenericMessageListener(m => messageProcessed.Set()));
            int i = 0;
            while (messageProcessed.WaitOne(100))
            {
                i++;
            }
            Console.WriteLine(context + ": " + i + " messages purged");
        }

        #endregion


        public static ITransportResolver MockTransportResolver()
        {
            var resolver = MockRepository.GenerateMock<ITransportResolver>();
            resolver.Expect(r => r.GetTransport(TransportConstants.TRANSPORT_ID1)).Return(new TransportInfo(TransportConstants.BROKER, TransportConstants.USERNAME, TransportConstants.PASSWORD, "MachineName") { JailStrategy = JailStrategy.MachineName });
            resolver.Expect(r => r.GetTransport(TransportConstants.TRANSPORT_ID2)).Return(new TransportInfo(TransportConstants.BROKER, TransportConstants.USERNAME, TransportConstants.PASSWORD, "MachineName") { JailStrategy = JailStrategy.MachineName });
            return resolver;
        }

        [Test]
        [TestCase(TransportConstants.QUEUE1)]
        public void SendWithTimeToLive(string dest)
        {
            var serializationManager = new SerializationManager();
            serializationManager.RegisterSerializer("fake",typeof(string), new FakeStringSerializer());

            ITransportResolver resolver = MockTransportResolver();
            IMessagingEngine engine = new MessagingEngine(resolver, serializationManager, new SonicTransportFactory());
            engine.Send("ping1", new Endpoint(TransportConstants.TRANSPORT_ID1, dest, serializationFormat: "fake"), 200);
            engine.Send("ping2", new Endpoint(TransportConstants.TRANSPORT_ID1, dest, serializationFormat: "fake"), 1000);

            Thread.Sleep(300);

            var receivedMessages = new List<object>();
            using (engine.Subscribe<string>(new Endpoint(TransportConstants.TRANSPORT_ID1, dest, serializationFormat: "fake"), receivedMessages.Add))
            {
                Thread.Sleep(1000);
            }

            Assert.AreEqual(1, receivedMessages.Count);
            Assert.AreEqual("ping2", receivedMessages[0]);
        }

        [TestCase(TransportConstants.QUEUE1, Description = "SendSubscribe queue")]
        [TestCase(TransportConstants.TOPIC, Description = "SendSubscribe topic")]
        public void RequestReplyTest(string dest)
        {
            var serializationManager = new SerializationManager();
            serializationManager.RegisterSerializer("fake",typeof (string), new FakeStringSerializer());

            ITransportResolver resolver = MockTransportResolver();
            IMessagingEngine engine = new MessagingEngine(resolver, serializationManager, new SonicTransportFactory());
            string response;
			using (engine.RegisterHandler<string, string>(s => s == "ping" ? "pong" : "error", new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.QUEUE1,serializationFormat:"fake")))
            {
                var stopwatch = Stopwatch.StartNew();
                response = engine.SendRequest<string, string>("ping", new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.QUEUE1,serializationFormat:"fake"));


                stopwatch.Stop();
                Console.WriteLine("Roundtrip: " + stopwatch.ElapsedMilliseconds + "ms");
            }


            var requestFinished = new ManualResetEvent(false);
            Exception exception = null;
            var thread = new Thread(() =>
                                        {
                                            try
                                            {
                                                engine.SendRequest<string, string>("ping", new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.QUEUE1, serializationFormat: "fake"), 500);
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
            Assert.That(exception, Is.Not.Null, "Request was handled after handler registration is disposed.");
            Assert.That(exception, Is.InstanceOf<TimeoutException>(), "Wrong exception was thrown on timeout.");
            if (thread.IsAlive)
                thread.Abort();
        }





        [TestCase(TransportConstants.QUEUE1, Description = "SendSubscribe queue")]
        [TestCase(TransportConstants.TOPIC, Description = "SendSubscribe topic")]
        public void SendSubscribeTest(string dest)
        {
            var serializationManager = new SerializationManager();
            serializationManager.RegisterSerializer("fake",typeof (string), new FakeStringSerializer());

            ITransportResolver resolver = MockTransportResolver();
            var engine = new MessagingEngine(resolver, serializationManager, new SonicTransportFactory());

            var recievedMessages = new List<object>();
            using (engine.Subscribe<string>(new Endpoint(TransportConstants.TRANSPORT_ID1, dest, serializationFormat: "fake"), recievedMessages.Add))
            {
                engine.Send("test", new Endpoint(TransportConstants.TRANSPORT_ID1, dest, serializationFormat: "fake"));
                engine.Send("test", new Endpoint(TransportConstants.TRANSPORT_ID1, dest, serializationFormat: "fake"));
                engine.Send("test", new Endpoint(TransportConstants.TRANSPORT_ID1, dest, serializationFormat: "fake"));
                Thread.Sleep(1000);
            }
            Assert.That(recievedMessages.Count, Is.EqualTo(3), "Some messages were not received");
            Assert.That(recievedMessages, Is.EqualTo(new[] {"test", "test", "test"}), "Some messages were corrupted");
            engine.Send("test", new Endpoint(TransportConstants.TRANSPORT_ID1, dest, serializationFormat: "fake"));
            Thread.Sleep(100);
            Assert.That(recievedMessages.Count, Is.EqualTo(3),
                        "Subscription callback was called after subscription is disposed");
        }        
        
        
        [TestCase(TransportConstants.QUEUE1, Description = "Send queue")]
        [Ignore("Investigation test")]
        public void SendTest(string dest)
        {
            var serializationManager = new SerializationManager();
            serializationManager.RegisterSerializer("fake",typeof (string), new FakeStringSerializer());

            ITransportResolver resolver = MockTransportResolver();
            var engine = new MessagingEngine(resolver, serializationManager, new SonicTransportFactory());

            using (engine.Subscribe<string>(new Endpoint(TransportConstants.TRANSPORT_ID1, dest), s => { }))
            {
                engine.Send(Guid.NewGuid().ToString(), new Endpoint(TransportConstants.TRANSPORT_ID1, dest));
                Thread.Sleep(1000);
            }

            int i = 0;
            Stopwatch sw=Stopwatch.StartNew();
            var done=new ManualResetEvent(false);
            using (engine.Subscribe<string>(new Endpoint(TransportConstants.TRANSPORT_ID1, dest), s =>
                                                                                        {
                                                                                            if (Interlocked.Increment(ref i) == 2961)
                                                                                                done.Set();
                                                                                        }))
            {
                var message = string.Join(",",Enumerable.Range(0, 100).Select(x => Guid.NewGuid().ToString()));
                while(!done.WaitOne(0))
                    engine.Send(message, new Endpoint(TransportConstants.TRANSPORT_ID1, dest));
            }

            Console.WriteLine(sw.ElapsedMilliseconds);
        }


        [Test]
        public void SharedDestinationSendSubscribeTest()
        {
            var serializationManager = new SerializationManager();
            serializationManager.RegisterSerializer("fake",typeof (string), new FakeStringSerializer());
            serializationManager.RegisterSerializer("fake",typeof (int), new FakeIntSerializer());

            ITransportResolver resolver = MockTransportResolver();
            var engine = new MessagingEngine(resolver, serializationManager, new SonicTransportFactory());

            var recievedNonSharedMessages = new List<string>();
            var recievedStrMessages = new List<string>();
            var recievedIntMessages = new List<int>();
            using (engine.Subscribe<string>(new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.TOPIC, false, serializationFormat: "fake"), s => { recievedNonSharedMessages.Add(s); Console.WriteLine("Non-Shared dest subscription #1:" + s); }))
            using (engine.Subscribe<string>(new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.TOPIC, true, serializationFormat: "fake"), s => { recievedStrMessages.Add(s); Console.WriteLine("Subscription #1:" + s); }))
            using (engine.Subscribe<int>(new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.TOPIC, true, serializationFormat: "fake"), i => { recievedIntMessages.Add(i); Console.WriteLine("Subscription #2:" + i); }))
            {
                engine.Send("11", new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.TOPIC, serializationFormat: "fake"));
                engine.Send("12", new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.TOPIC, serializationFormat: "fake"));
                engine.Send("13", new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.TOPIC, serializationFormat: "fake"));
                engine.Send("14", new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.TOPIC, serializationFormat: "fake"));
                engine.Send("15", new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.TOPIC, serializationFormat: "fake"));
                engine.Send(21, new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.TOPIC, serializationFormat: "fake"));
                engine.Send("16", new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.TOPIC, serializationFormat: "fake"));
                engine.Send(22, new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.TOPIC, serializationFormat: "fake"));
                engine.Send("23", new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.TOPIC, serializationFormat: "fake"));
                engine.Send(24, new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.TOPIC, serializationFormat: "fake"));
                engine.Send(25, new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.TOPIC, serializationFormat: "fake"));
                engine.Send(26, new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.TOPIC, serializationFormat: "fake"));
                engine.Send(27, new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.TOPIC, serializationFormat: "fake"));
                engine.Send(28, new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.TOPIC, serializationFormat: "fake"));
                Thread.Sleep(2000);
            }
            Assert.That(recievedNonSharedMessages.Count, Is.EqualTo(14), "Not all messages from both sequences were received by non-shared subscription ");
            Assert.That(recievedStrMessages.Count, Is.EqualTo(7), "Not all messages from first sequence were received by corresponding subscription ");
            Assert.That(recievedIntMessages.Count, Is.EqualTo(7), "Not all messages from second sequence were received by corresponding subscription");
        }


        [Test]
        public void EachDestinationIsSubscribedOnDedicatedThreadTest()
        {
            var serializationManager = new SerializationManager();
            serializationManager.RegisterSerializer("fake",typeof(string), new FakeStringSerializer());

            ITransportResolver resolver = MockTransportResolver();
            var engine = new MessagingEngine(resolver, serializationManager, new SonicTransportFactory());

            var queue1MessagesThreadIds = new List<int>();
            var queue2MessagesThreadIds = new List<int>();
            using (engine.Subscribe<string>(new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.QUEUE1, serializationFormat: "fake"), s => queue1MessagesThreadIds.Add(Thread.CurrentThread.ManagedThreadId)))
            using (engine.Subscribe<string>(new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.QUEUE2, serializationFormat: "fake"), s => queue2MessagesThreadIds.Add(Thread.CurrentThread.ManagedThreadId)))
            {
                engine.Send("test", new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.QUEUE1, serializationFormat: "fake"));
                engine.Send("test", new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.QUEUE2, serializationFormat: "fake"));
                engine.Send("test", new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.QUEUE1, serializationFormat: "fake"));
                engine.Send("test", new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.QUEUE2, serializationFormat: "fake"));
                engine.Send("test", new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.QUEUE1, serializationFormat: "fake"));
                engine.Send("test", new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.QUEUE2, serializationFormat: "fake"));
                Thread.Sleep(1000);
            }
            Assert.That(queue1MessagesThreadIds.Distinct().Any(), Is.True, "Messages were not processed");
            Assert.That(queue2MessagesThreadIds.Distinct().Any(), Is.True, "Messages were not processed");
            Assert.That(queue1MessagesThreadIds.Distinct().Count(), Is.EqualTo(1), "Messages from one subscription were processed in more then 1 thread");
            Assert.That(queue2MessagesThreadIds.Distinct().Count(), Is.EqualTo(1), "Messages from one subscription were processed in more then 1 thread");
            Assert.That(queue1MessagesThreadIds.First() != queue2MessagesThreadIds.First(), Is.True, "Messages from different subscriptions were processed one thread");
        }

        [Test]
        public void FacilityTest()
        {
            using (IWindsorContainer container = new WindsorContainer())
            {
                container.Kernel.Resolver.AddSubResolver(new ArrayResolver(container.Kernel));
                container.AddFacility(new MessagingFacility(
                                          new Dictionary<string, TransportInfo>
                                              {
                                                  {
                                                      TransportConstants.TRANSPORT_ID1,
                                                      new TransportInfo(TransportConstants.BROKER,
                                                                        TransportConstants.USERNAME,
                                                                        TransportConstants.PASSWORD, "MachineName")
                                                      }
                                              }));
                var factory = MockRepository.GenerateMock<ISerializerFactory>();
                factory.Expect(f => f.SerializationFormat).Return("fake");
                factory.Expect(f => f.Create<string>()).Return(new FakeStringSerializer());
                container.Register(Component.For<ISerializerFactory>().Instance(factory));
                container.Register(Component.For<ITransportFactory>().ImplementedBy<SonicTransportFactory>());
                var engine = container.Resolve<IMessagingEngine>();
                var ev = new ManualResetEvent(false);
                using (engine.Subscribe<string>(new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.TOPIC, serializationFormat: "fake"), s =>
                            {
                                Console.WriteLine(s);
                                ev.Set();
                            }))
                {
                    engine.Send("test", new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.TOPIC,serializationFormat:"fake"));
                    Assert.That(ev.WaitOne(500), Is.True, "message was not received");
                }
            }

        }




        [Test]
        public void SendToOverflowenQueueFailureTest()
        {

            var serializationManager = new SerializationManager();
            serializationManager.RegisterSerializer("fake",typeof(string), new FakeStringSerializer());

            ITransportResolver resolver = MockTransportResolver();

            //Assumption: queue capacity is 1mb
            var halfMegabyteMessage = new string('a', 1 << 19);
            using (var engine = new MessagingEngine(resolver, serializationManager, new SonicTransportFactory()))
            {
                engine.Send(halfMegabyteMessage, new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.QUEUE1, serializationFormat: "fake"));
                Exception exception = null;

                var t = new Thread(() =>
                {
                    try
                    {
                        engine.Send(halfMegabyteMessage, new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.QUEUE1, serializationFormat: "fake"));
                    }
                    catch (Exception ex)
                    {
                        exception = ex;
                    }
                });
                t.Start();
                Assert.That(t.Join(2000), Is.True, "Exception was not thrown when queue is overflowed");
                Assert.That(exception, Is.Not.Null);
                Console.WriteLine(exception);
            }
        }



        [Test]
        public void TransportFailuteHandlingTest()
        {
            var serializationManager = new SerializationManager();
            serializationManager.RegisterSerializer("fake",typeof (string), new FakeStringSerializer());
            var resolver = MockTransportResolver();
            var transportManager = new TransportManager(resolver, new SonicTransportFactory());
            var engine = new MessagingEngine(transportManager, serializationManager);
            int failureWasReportedCount = 0;
            engine.SubscribeOnTransportEvents((transportid, @event) => failureWasReportedCount++);

            //need for transportManager to start tracking transport failures for these ids
            transportManager.GetProcessingGroup(TransportConstants.TRANSPORT_ID1,"test");
            transportManager.GetProcessingGroup(TransportConstants.TRANSPORT_ID2, "test");

            transportManager.ProcessTransportFailure( 
                                                     new TransportInfo(TransportConstants.BROKER,
                                                                       TransportConstants.USERNAME,
                                                                       TransportConstants.PASSWORD, "MachineName"));
            Assert.That(failureWasReportedCount, Is.GreaterThan(0), "Failure was not reported");
            Assert.That(failureWasReportedCount, Is.EqualTo(2), "Failure was not reported for all ids");
        }



        [Test]
        public void ConcurrentTransportResolutionTest()
        {
            var resolver = MockTransportResolver();
            var transportManager = new TransportManager(resolver,  new SonicTransportFactory());
            var start =new ManualResetEvent(false);
            int errorCount=0;
            int attemptCount=0;

            foreach (var i in Enumerable.Range(1, 10))
            {
                int treadNum = i;
                var thread = new Thread(() =>
                {
                    start.WaitOne();
                    try
                    {
                        var transport = transportManager.GetProcessingGroup(TransportConstants.TRANSPORT_ID1,"test");
                        Console.WriteLine(treadNum+". "+transport);
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
            while (attemptCount<10)
            {
                Thread.Sleep(50);
            }

            Assert.That(errorCount, Is.EqualTo(0));            
            

        }

        [Test]
        [Ignore]
        public void InvestigationTest()
        {

            var factory = new QueueConnectionFactory();
            (factory as ConnectionFactory).setConnectionURLs(TransportConstants.BROKER);
            var connection = factory.createQueueConnection(TransportConstants.USERNAME, TransportConstants.PASSWORD);
            ((Connection)connection).setPingInterval(30);
            connection.start();


            var session = (QueueSession)connection.createQueueSession(false, 1004);

          

            var queue = session.createQueue("finam.ibank.dev.tests");


            var consumer = session.createConsumer(queue);
            int i = 0;

            consumer.setMessageListener(new GenericMessageListener(message => { Console.WriteLine(DateTime.Now);message.acknowledge(); }));
            Thread.Sleep(2000);
            consumer.close();




            
            var producer = session.createProducer(queue);

            BytesMessage bytesMessage;
            for (int j = 0; j < 5; j++)
            {
                bytesMessage = session.createBytesMessage();
                bytesMessage.writeBytes(new byte[] { 100 });
                producer.send(bytesMessage);
            }
            

            consumer = session.createConsumer(queue);


            var p = session.createProducer(session.createTemporaryQueue());
            bytesMessage = session.createBytesMessage();
            bytesMessage.writeBytes(new byte[] { 100 });
            p.send(bytesMessage);

            consumer.setMessageListener(new GenericMessageListener(message =>
                                                                                {
                                                                                    Console.WriteLine(""+DateTime.Now+(++i)+" "+(i % 2 == 0));
                                                                                    //message.acknowledge();return;                    
                                                                                    if (i % 2 == 0) message.acknowledge();
                                                                                    else throw new Exception();
                                                                                    //session.recover();
                                                                                }));
            Thread.Sleep(1000);
            Console.WriteLine("waiting");
            Thread.Sleep(60*60 * 1000);
            consumer.close();
            session.close();
            connection.close();
        }
    }

}