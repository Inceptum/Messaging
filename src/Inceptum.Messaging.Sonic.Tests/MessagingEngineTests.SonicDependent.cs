﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using Castle.MicroKernel.Registration;
using Castle.MicroKernel.Resolvers.SpecializedResolvers;
using Castle.Windsor;
using Inceptum.Messaging.Castle;
using Inceptum.Messaging.Contract;
using NUnit.Framework;
using Rhino.Mocks;
using Sonic.Jms;
using Connection = Sonic.Jms.Ext.Connection;
using ConnectionFactory = Sonic.Jms.Ext.ConnectionFactory;
using QueueConnectionFactory = Sonic.Jms.Cf.Impl.QueueConnectionFactory;
using QueueSession = Sonic.Jms.Ext.QueueSession;

namespace Inceptum.Messaging.Sonic.Tests
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


    [TestFixture, Ignore("Integration tests - uses real sonic environments")]
    public class MessagingEngineTests1
    {
        #region Setup/Teardown

        [SetUp]
        public void Setup()
        {
            PurgeQueue("Test setup");
        }

     [OneTimeTearDown]
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
            resolver.Expect(r => r.GetTransport(TransportConstants.TRANSPORT_ID1)).Return(new TransportInfo(TransportConstants.BROKER, TransportConstants.USERNAME, TransportConstants.PASSWORD, "MachineName","Sonic") { JailStrategy = JailStrategy.MachineName });
            resolver.Expect(r => r.GetTransport(TransportConstants.TRANSPORT_ID2)).Return(new TransportInfo(TransportConstants.BROKER, TransportConstants.USERNAME, TransportConstants.PASSWORD, "MachineName", "Sonic") { JailStrategy = JailStrategy.MachineName });
            return resolver;
        }

        [Test]
        [TestCase(TransportConstants.QUEUE1)]
        public void SendWithTimeToLive(string dest)
        {
            ITransportResolver resolver = MockTransportResolver();
            using (IMessagingEngine engine = new MessagingEngine(resolver, new SonicTransportFactory()))
            {
                engine.SerializationManager.RegisterSerializer("fake", typeof (string), new FakeStringSerializer());
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
        }

        [TestCase(TransportConstants.QUEUE1, Description = "SendSubscribe queue")]
        [TestCase(TransportConstants.TOPIC, Description = "SendSubscribe topic")]
        public void RequestReplyTest(string dest)
        {
            
            ITransportResolver resolver = MockTransportResolver();
            using (IMessagingEngine engine = new MessagingEngine(resolver, new SonicTransportFactory()))
            {
                engine.SerializationManager.RegisterSerializer("fake", typeof (string), new FakeStringSerializer());
                string response;
                using (
                    engine.RegisterHandler<string, string>(s => s == "ping" ? "pong" : "error",
                        new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.QUEUE1, serializationFormat: "fake")))
                {
                    var stopwatch = Stopwatch.StartNew();
                    response = engine.SendRequest<string, string>("ping",
                        new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.QUEUE1, serializationFormat: "fake"));


                    stopwatch.Stop();
                    Console.WriteLine("Roundtrip: " + stopwatch.ElapsedMilliseconds + "ms");
                }


                var requestFinished = new ManualResetEvent(false);
                Exception exception = null;
                var thread = new Thread(() =>
                {
                    try
                    {
                        engine.SendRequest<string, string>("ping",
                            new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.QUEUE1, serializationFormat: "fake"), 500);
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
        }





        [TestCase(TransportConstants.QUEUE1, Description = "SendSubscribe queue")]
        [TestCase(TransportConstants.TOPIC, Description = "SendSubscribe topic")]
        public void SendSubscribeTest(string dest)
        {
            
            ITransportResolver resolver = MockTransportResolver();
            using (var engine = new MessagingEngine(resolver, new SonicTransportFactory()))
            {
                engine.SerializationManager.RegisterSerializer("fake", typeof (string), new FakeStringSerializer());

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
        }        
        
         


        [Test]
        public void SharedDestinationSendSubscribeTest()
        {


            ITransportResolver resolver = MockTransportResolver();
            using (var engine = new MessagingEngine(resolver, new SonicTransportFactory()))
            {
                engine.SerializationManager.RegisterSerializer("fake", typeof (string), new FakeStringSerializer());
                engine.SerializationManager.RegisterSerializer("fake", typeof (int), new FakeIntSerializer());

                var recievedNonSharedMessages = new List<string>();
                var recievedStrMessages = new List<string>();
                var recievedIntMessages = new List<int>();
                using (
                    engine.Subscribe<string>(new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.TOPIC, false, serializationFormat: "fake"), s =>
                    {
                        recievedNonSharedMessages.Add(s);
                        Console.WriteLine("Non-Shared dest subscription #1:" + s);
                    }))
                using (
                    engine.Subscribe<string>(new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.TOPIC, true, serializationFormat: "fake"), s =>
                    {
                        recievedStrMessages.Add(s);
                        Console.WriteLine("Subscription #1:" + s);
                    }))
                using (engine.Subscribe<int>(new Endpoint(TransportConstants.TRANSPORT_ID1, TransportConstants.TOPIC, true, serializationFormat: "fake"), i =>
                {
                    recievedIntMessages.Add(i);
                    Console.WriteLine("Subscription #2:" + i);
                }))
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
        }


      
        [Test]
        public void SendToOverflownQueueFailureTest()
        {

           
            ITransportResolver resolver = MockTransportResolver();

            //Assumption: queue capacity is 1mb
            var halfMegabyteMessage = new string('a', 1 << 19);
            using (var engine = new MessagingEngine(resolver, new SonicTransportFactory()))
            {
                engine.SerializationManager.RegisterSerializer("fake", typeof(string), new FakeStringSerializer());
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
                Assert.That(t.Join(2000), Is.True, "Exception was not thrown when queue is overflown");
                Assert.That(exception, Is.Not.Null);
                Console.WriteLine(exception);
            }
        }
      
    }

}