using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using Castle.Core.Logging;
using Inceptum.Messaging.Contract;
using Inceptum.Messaging.Transports;
using NUnit.Framework;
using Rhino.Mocks;

namespace Inceptum.Messaging.RabbitMq.Tests
{
    [TestFixture]
    public class SubscriptionManagerTests
    {
        [Test]
        /*[Timeout(5000)]*/
        public void UnknownMessageTest()
        {
            const string TEST_QUEUE = "test.queue";
            const string TEST_EXCHANGE = "test.exchange";
            ITransportResolver transportResolver = new TransportResolver(new Dictionary<string, TransportInfo>()
            {
                {"main", new TransportInfo("sr-tls01-s01.test-s02.uniservers.ru", "guest", "guest", "None", "RabbitMq")}
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
              using (var subscriptionManager = createSubscriptionManagerWithMockedDependencies(action => callback = action))
              {

                  DateTime processed = default(DateTime);
                  DateTime acked = default(DateTime);
                  subscriptionManager.Subscribe(new Endpoint("test", "test", false, "fake"), (message, acknowledge) =>
                  {
                      processed = DateTime.Now;
                      acknowledge(1000, true);
                      Console.WriteLine(processed.ToString("HH:mm:ss.ffff") + " recieved");
                  },null);
                  var acknowledged = new ManualResetEvent(false);
                  callback(new BinaryMessage {Bytes = new byte[0], Type = typeof (string).Name}, b =>
                  {
                      acked = DateTime.Now;
                      acknowledged.Set();
                      Console.WriteLine(acked.ToString("HH:mm:ss.ffff") + " acknowledged");
                  });
                  Assert.That(acknowledged.WaitOne(1300), Is.True, "Message was not acknowledged");
                  Assert.That((acked - processed).TotalMilliseconds, Is.GreaterThan(1000), "Message was acknowledged earlier than scheduled time ");
              }
          }
          [Test]
          public void DeferredAcknowledgementShouldBePerfomedOnDisposeTest()
          {
              Action<BinaryMessage, Action<bool>> callback=null;
              bool acknowledged = false;
              using (var subscriptionManager = createSubscriptionManagerWithMockedDependencies(action => callback = action))
              {
                  subscriptionManager.Subscribe(new Endpoint("test", "test", false, "fake"), (message, acknowledge) =>
                      {
                          acknowledge(60000, true);
                          Console.WriteLine(DateTime.Now.ToString("HH:mm:ss.ffff") + " received");
                      },null);
                  
                  callback(new BinaryMessage { Bytes = new byte[0], Type = typeof(string).Name }, b => { acknowledged=true; Console.WriteLine(DateTime.Now.ToString("HH:mm:ss.ffff") + " acknowledged"); });
              }
              Assert.That(acknowledged,Is.True,"Message was not acknowledged on engine dispose");

          }



 

       
        [Test]
        public void ResubscriptionTest()
        {
            Action<BinaryMessage, Action<bool>> callback = null;
            Action emulateFail= () => { };
            int subscriptionsCounter = 0;
            var subscribed = new AutoResetEvent(false);
            Action onSubscribe = () =>
            {
                subscriptionsCounter++;
                if (subscriptionsCounter == 1 || subscriptionsCounter == 3 || subscriptionsCounter == 5)
                    throw new Exception("Fail #" + subscriptionsCounter);
                subscribed.Set();
            };

            using (var subscriptionManager = createSubscriptionManagerWithMockedDependencies(action => callback = action, action => emulateFail = action, onSubscribe ))
            {
                var subscription =subscriptionManager.Subscribe(new Endpoint("test", "test", false, "fake"), (message, acknowledge) =>
                {
                    acknowledge(0, true);
                    Console.WriteLine(DateTime.Now.ToString("HH:mm:ss.ffff") + " recieved");
                },null);

                //First attempt fails next one happends in 1000ms and should be successfull
                Assert.That(subscribed.WaitOne(1200), Is.True, "Has not resubscribed after first subscription fail");
                callback(new BinaryMessage {Bytes = new byte[0], Type = typeof (string).Name}, b => Console.WriteLine(DateTime.Now.ToString("HH:mm:ss.ffff") + " acknowledged"));
                Thread.Sleep(200);

                Console.WriteLine("{0:H:mm:ss.fff} Emulating fail", DateTime.Now);
                emulateFail();
                //First attempt is taken right after failure, but it fails next one happends in 1000ms and should be successfull
                Assert.That(subscribed.WaitOne(1500), Is.True, "Resubscription has not happened within resubscription timeout");
                Assert.That(subscriptionsCounter, Is.EqualTo(4));
                
                
                Thread.Sleep(200);
                Console.WriteLine("{0:H:mm:ss.fff} Emulating fail", DateTime.Now);

                emulateFail();
                subscription.Dispose();
                //First attempt is taken right after failure, but it fails next one should not be taken since subscription is disposed
                Assert.That(subscribed.WaitOne(3000), Is.False, "Resubscription happened after subscription was disposed");

            }
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
                var subscriptionManager = createSubscriptionManagerWithMockedDependencies(action => callback = action);
                Stopwatch sw = Stopwatch.StartNew();
                subscriptionManager.Subscribe(new Endpoint("test", "test", false, "fake"), (message, acknowledge) =>
                    {
                        Thread.Sleep(rnd.Next(1,10));
                        acknowledge(delay.Key, true);
                    },null);
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


        private static SubscriptionManager createSubscriptionManagerWithMockedDependencies(Action<Action<BinaryMessage, Action<bool>>> setCallback, Action<Action> setOnFail = null, Action onSubscribe = null)
        {

            if (setOnFail == null)
                setOnFail = action => { };
            if (onSubscribe == null)
                onSubscribe = () => { };
            var transportManager = MockRepository.GenerateMock<ITransportManager>();
            var processingGroup = MockRepository.GenerateMock<IProcessingGroup>();
            processingGroup.Expect(p => p.Subscribe("test", null, null))
                           .IgnoreArguments()
                           .WhenCalled(invocation =>
                           {
                               setCallback((Action<BinaryMessage, Action<bool>>) invocation.Arguments[1]);
                               onSubscribe();
                           })
                           .Return(MockRepository.GenerateMock<IDisposable>());
            transportManager.Expect(t => t.GetProcessingGroup(null,(Destination) null, null))
                .IgnoreArguments()
                .WhenCalled(invocation => setOnFail((Action)invocation.Arguments[2]))
                .Return(processingGroup);
            return new SubscriptionManager(transportManager,1000)
            {
                Logger = new ConsoleLoggerWithTime()
            };
        }
    }
}
