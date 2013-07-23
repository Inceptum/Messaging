using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using Inceptum.Messaging.Contract;
using Inceptum.Messaging.Transports;
using NUnit.Framework;
using Rhino.Mocks;

namespace Inceptum.Messaging.RabbitMq.Tests
{
    [TestFixture]
    public class MessagingEngineTests
    {
        [Test]
        /*[Timeout(5000)]*/
        public void UnknownMessageTest()
        {
               const string TEST_QUEUE = "test.queue";
          const string TEST_EXCHANGE = "test.exchange";
            ITransportResolver transportResolver = new TransportResolver(new Dictionary<string, TransportInfo>()
                {
                    {"main",new TransportInfo("sr-tls01-s01.test-s02.uniservers.ru", "guest", "guest", "None", "RabbitMq")}
                });
            var eq = new Endpoint("main", TEST_QUEUE, true, "json");
            var ee = new Endpoint("main", TEST_EXCHANGE, true, "json");
           
            using (var me = new MessagingEngine(transportResolver, new RabbitMqTransportFactory()))
            {
                me.Send("string value", ee);
            }

            using (var me = new MessagingEngine(transportResolver, new RabbitMqTransportFactory()))
            {
                me.Subscribe<int>(eq, Console.WriteLine);
                me.Subscribe<double>(eq, Console.WriteLine);
                me.Subscribe<string>(eq, s => Console.WriteLine(s));
            }
            Thread.Sleep(200);
        } 
        

          [Test]
          public void DeferredAcknowledgementTest()
          {
              Action<BinaryMessage, Action<bool>> callback=null;
              var messagingEngine = createMessagingEngineWithMockedDependencies(action => callback = action);
              
              DateTime processed=default(DateTime);
              DateTime acked = default(DateTime);
              messagingEngine.Subscribe<string>(new Endpoint("test", "test",false,"fake"), (message, acknowledge) =>
                  {
                      processed = DateTime.Now;
                      acknowledge(1000, true);
                      Console.WriteLine(processed.ToString("HH:mm:ss.ffff") + " recieved");
                  });
              var acknowledged=new ManualResetEvent(false);
              callback(new BinaryMessage { Bytes = new byte[0], Type = typeof(string).Name }, b => {  acked = DateTime.Now; acknowledged.Set();Console.WriteLine(acked.ToString("HH:mm:ss.ffff") + " acknowledged"); });
              Assert.That(acknowledged.WaitOne(1300),Is.True,"Message was not acknowledged");
              Assert.That((acked-processed).TotalMilliseconds,Is.GreaterThan(1000),"Message was acknowledged earlier than scheduled time ");

          }
          [Test]
          public void DeferredAcknowledgementShouldBePerfomedOnDisposeTest()
          {
              Action<BinaryMessage, Action<bool>> callback=null;
              bool acknowledged = false;
              using (var messagingEngine = createMessagingEngineWithMockedDependencies(action => callback=action))
              {
                  messagingEngine.Subscribe<string>(new Endpoint("test", "test", false, "fake"), (message, acknowledge) =>
                      {
                          acknowledge(60000, true);
                          Console.WriteLine(DateTime.Now.ToString("HH:mm:ss.ffff") + " recieved");
                      });
                  
                  callback(new BinaryMessage { Bytes = new byte[0], Type = typeof(string).Name }, b => { acknowledged=true; Console.WriteLine(DateTime.Now.ToString("HH:mm:ss.ffff") + " acknowledged"); });
              }
              Assert.That(acknowledged,Is.True,"Message was not acknowledged on engine dispose");

          }

        [Test]
       [Ignore("PerfofmanceTest")]
        public void DeferredAcknowledgementPerformanceTest()
        {
            Random rnd = new Random();
            const int messageCount = 100;
            Console.WriteLine(messageCount+" messages");
            var delays = new Dictionary<long, string> { { 0, "immediate acknowledge" }, { 1, "deferred acknowledge" }, { 100, "deferred acknowledge" }, { 1000, "deferred acknowledge" } };
            foreach (var delay in delays)
            {


                Action<BinaryMessage, Action<bool>> callback = null;
                long counter = messageCount;
                var complete = new ManualResetEvent(false);
                var messagingEngine = createMessagingEngineWithMockedDependencies(action => callback = action);
                Stopwatch sw = Stopwatch.StartNew();
                messagingEngine.Subscribe<string>(new Endpoint("test", "test", false, "fake"), (message, acknowledge) =>
                    {
                        Thread.Sleep(rnd.Next(1,10));
                        acknowledge(delay.Key, true);
                    });
                for (int i = 0; i < messageCount; i++)
                    callback(new BinaryMessage {Bytes = new byte[0], Type = typeof (string).Name}, b =>
                        {
                            if (Interlocked.Decrement(ref counter) == 0)
                                complete.Set();
                        });
                complete.WaitOne();
                Console.WriteLine("{0} ({1}ms delay extracted to narrow results): {2}ms ",delay.Value,delay.Key, sw.ElapsedMilliseconds-delay.Key);
            }
        }

        private static MessagingEngine createMessagingEngineWithMockedDependencies(Action<Action<BinaryMessage, Action<bool>>> setCallback)
        {
            var transportResolver = MockRepository.GenerateMock<ITransportResolver>();
            transportResolver.Expect(r => r.GetTransport(null)).IgnoreArguments().Return(new TransportInfo("broker", "l", "p", null, "test"));
            var transportFactory = MockRepository.GenerateMock<ITransportFactory>();
            transportFactory.Expect(t => t.Name).Return("test");
            var transport = MockRepository.GenerateMock<ITransport>();
            transportFactory.Expect(t => t.Create(null, null)).IgnoreArguments().Return(transport);
            var processingGroup = MockRepository.GenerateMock<IProcessingGroup>();
            transport.Expect(t => t.CreateProcessingGroup(null)).IgnoreArguments().Return(processingGroup);
            processingGroup.Expect(p => p.Subscribe("test", null, null))
                           .IgnoreArguments()
                           .WhenCalled(invocation => setCallback((Action<BinaryMessage, Action<bool>>) invocation.Arguments[1]))
                           .Return(MockRepository.GenerateMock<IDisposable>());
            var serializer = MockRepository.GenerateMock<IMessageSerializer<string>>();
            serializer.Expect(s => s.Deserialize(null)).IgnoreArguments().Return("test message");
            var messagingEngine = new MessagingEngine(transportResolver, transportFactory);
            messagingEngine.SerializationManager.RegisterSerializer("test", typeof (string), serializer);
            return messagingEngine;
        }
    }
}
