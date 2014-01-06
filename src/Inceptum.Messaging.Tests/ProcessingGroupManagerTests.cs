using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using Castle.Core.Internal;
using Inceptum.Messaging.Contract;
using Inceptum.Messaging.InMemory;
using Inceptum.Messaging.Transports;
using NUnit.Framework;
using Rhino.Mocks;

namespace Inceptum.Messaging.Tests
{
    [TestFixture]
    public class ProcessingGroupManagerTests
    {
        [Test]
        [ExpectedException(typeof(ArgumentException), ExpectedMessage = "Priority other then 0 is not applicable for processing group with zero concurrencyLevel (messages are processed on consuming thread)\r\nParameter name: priority")]
        public void ProcessingGroupWithZeroConcurrencyDoesNotAcceptPriority()
        {
            using (var processingGroup = new ProcessingGroup("test", new ProcessingGroupInfo()))
            {
                processingGroup.Subscribe(MockRepository.GenerateMock<IMessagingSession>(), "dest", (message, action) => { }, null, 1);
            }
        }

        [Test]
        public void SameThreadSubscriptionTest()
        {
            var transportManager = new TransportManager(
                new TransportResolver(new Dictionary<string, TransportInfo>
                    {
                        {"transport-1", new TransportInfo("transport-1", "login1", "pwd1", "None", "InMemory")}
                    }));
            var processingGroupManager = new ProcessingGroupManager(transportManager);

            var session = transportManager.GetMessagingSession("transport-1", "pg"); var usedThreads = new List<int>();
            var subscription = processingGroupManager.Subscribe(new Endpoint { Destination = "queue", TransportId = "transport-1" },
                (message, action) =>
                {
                    lock (usedThreads)
                    {
                        usedThreads.Add(Thread.CurrentThread.ManagedThreadId);
                        Console.WriteLine(Thread.CurrentThread.Name + Thread.CurrentThread.ManagedThreadId);
                    }
                    Thread.Sleep(50);
                }, null, "pg", 0);
            using (subscription)
            {
                Enumerable.Range(1, 20).ForEach(i => session.Send("queue", new BinaryMessage(), 0));
                Thread.Sleep(1200);
            }
            Assert.That(usedThreads.Count(), Is.EqualTo(20), "not all messages were processed");
            Assert.That(usedThreads.Distinct().Count(), Is.EqualTo(1), "more then one thread was used for message processing");
        }

        [Test]
        public void MultiThreadThreadSubscriptionTest()
        {
            var transportManager = new TransportManager(
                new TransportResolver(new Dictionary<string, TransportInfo>
                    {
                        {"transport-1", new TransportInfo("transport-1", "login1", "pwd1", "None", "InMemory")}
                    }));
            var processingGroupManager = new ProcessingGroupManager(transportManager, new Dictionary<string, ProcessingGroupInfo>()
            {
                {
                    "pg", new ProcessingGroupInfo() {ConcurrencyLevel = 3}
                }
            });

            var processingGroup = transportManager.GetMessagingSession("transport-1", "pg");
            var usedThreads = new List<int>();
            var subscription = processingGroupManager.Subscribe(new Endpoint { Destination = "queue", TransportId = "transport-1" },
                (message, action) =>
                {
                    lock (usedThreads)
                    {
                        usedThreads.Add(Thread.CurrentThread.ManagedThreadId);
                        Console.WriteLine(Thread.CurrentThread.Name + Thread.CurrentThread.ManagedThreadId + ":" + Encoding.UTF8.GetString(message.Bytes));
                    }
                    Thread.Sleep(50);
                }, null, "pg", 0);

            using (subscription)
            {
                Enumerable.Range(1, 20).ForEach(i => processingGroup.Send("queue", new BinaryMessage { Bytes = Encoding.UTF8.GetBytes((i % 3).ToString()) }, 0));
                Thread.Sleep(1200);
            }
            Assert.That(usedThreads.Count(), Is.EqualTo(20), "not all messages were processed");
            Assert.That(usedThreads.Distinct().Count(), Is.EqualTo(3), "wrong number of threads was used for message processing");
        }

        [Test]
        public void MultiThreadPrioritizedThreadSubscriptionTest()
        {
            var transportManager = new TransportManager(
                new TransportResolver(new Dictionary<string, TransportInfo>
                    {
                        {"transport-1", new TransportInfo("transport-1", "login1", "pwd1", "None", "InMemory")}
                    }));
            var processingGroupManager = new ProcessingGroupManager(transportManager, new Dictionary<string, ProcessingGroupInfo>()
            {
                {
                    "pg", new ProcessingGroupInfo {ConcurrencyLevel = 3}
                }
            });

            var usedThreads = new List<int>();
            var processedMessages = new List<int>();
            CallbackDelegate<BinaryMessage> callback = (message, action) =>
            {
                lock (usedThreads)
                {
                    usedThreads.Add(Thread.CurrentThread.ManagedThreadId);
                    processedMessages.Add(int.Parse(Encoding.UTF8.GetString(message.Bytes)));
                    Console.WriteLine(Thread.CurrentThread.ManagedThreadId + ":" + Encoding.UTF8.GetString(message.Bytes));
                }
                Thread.Sleep(50);
            };

            var subscription0 = processingGroupManager.Subscribe(new Endpoint { Destination = "queue0", TransportId = "transport-1" }, callback, null, "pg", 0);
            var subscription1 = processingGroupManager.Subscribe(new Endpoint { Destination = "queue1", TransportId = "transport-1" }, callback, null, "pg", 1);
            var subscription2 = processingGroupManager.Subscribe(new Endpoint { Destination = "queue2", TransportId = "transport-1" }, callback, null, "pg", 2);

            using (subscription0)
            using (subscription1)
            using (subscription2)
            {

                Enumerable.Range(1, 20)
                    .ForEach(i =>processingGroupManager.Send(new Endpoint { Destination = "queue" + i % 3, TransportId = "transport-1" }, new BinaryMessage { Bytes = Encoding.UTF8.GetBytes((i % 3).ToString()) }, 0,"pg"));

                Thread.Sleep(1200);
                ResolvedTransport transport = transportManager.ResolveTransport("transport-1");
                Assert.That(transport.Sessions.Select(s => s.Name).OrderBy(s => s), Is.EqualTo(new[] { "pg priority0", "pg priority1", "pg priority2" }),"Wrong sessions were created. Expectation: one session per [processingGroup,priority] pair ");
            }
            Assert.That(usedThreads.Count(), Is.EqualTo(20), "not all messages were processed");
            Assert.That(usedThreads.Distinct().Count(), Is.EqualTo(3), "wrong number of threads was used for message processing");

            double averageOrder0 = processedMessages.Select((i, index) => new { message = i, index }).Where(arg => arg.message == 0).Average(arg => arg.index);
            double averageOrder1 = processedMessages.Select((i, index) => new { message = i, index }).Where(arg => arg.message == 1).Average(arg => arg.index);
            double averageOrder2 = processedMessages.Select((i, index) => new { message = i, index }).Where(arg => arg.message == 2).Average(arg => arg.index);
            Assert.That(averageOrder0, Is.LessThan(averageOrder1), "priority was not respected");
            Assert.That(averageOrder1, Is.LessThan(averageOrder2), "priority was not respected");


        }

          [Test]
          public void DeferredAcknowledgementTest()
          {
              Action<BinaryMessage, Action<bool>> callback=null;
              using (var processingGroupManager = createProcessingGroupManagerWithMockedDependencies(action => callback = action))
              {

                  DateTime processed = default(DateTime);
                  DateTime acked = default(DateTime);
                  processingGroupManager.Subscribe(new Endpoint("test", "test", false, "fake"), (message, acknowledge) =>
                      {
                          processed = DateTime.Now;
                          acknowledge(1000, true);
                          Console.WriteLine(processed.ToString("HH:mm:ss.ffff") + " recieved");
                      },null,"ProcessingGroup", 0);
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
        public void MessagesStuckedInProcessingGroupAfterUnsubscriptionShouldBeUnacked()
        {
            bool? normallyProcessedMessageAck = null;
            bool? stuckedInQueueMessageAck = null;
            var finishProcessing=new ManualResetEvent(false);
            Action<BinaryMessage, Action<bool>> callback = null;
            using (var processingGroupManager = createProcessingGroupManagerWithMockedDependencies(action => callback = action))
            {
                IDisposable subscription=null ;
                subscription = processingGroupManager.Subscribe(new Endpoint("test", "test", false, "fake"), (message, acknowledge) =>
                {
                    acknowledge(0,true);
                    finishProcessing.WaitOne();

                    //dispose subscription in first message processing to ensure first message processing starts before unsubscription 
                    subscription.Dispose();
                }, null, "SingleThread", 0);
                  

                callback(new BinaryMessage { Bytes = new byte[]{1}, Type = typeof(string).Name }, b => normallyProcessedMessageAck=b);
               
                finishProcessing.Set();
                callback(new BinaryMessage { Bytes = new byte[]{2}, Type = typeof(string).Name }, b => stuckedInQueueMessageAck=b);
            }
            Assert.That(normallyProcessedMessageAck,Is.True,"Normally processed message was not acked");
            Assert.That(stuckedInQueueMessageAck, Is.False, "Stucked message was not unacked");
        }

          [Test]
          public void DeferredAcknowledgementShouldBePerformedOnDisposeTest()
          {
              Action<BinaryMessage, Action<bool>> callback=null;
              bool acknowledged = false;

              using (var processingGroupManager = createProcessingGroupManagerWithMockedDependencies(action => callback = action))
              {
                 var subscription= processingGroupManager.Subscribe(new Endpoint("test", "test", false, "fake"), (message, acknowledge) =>
                      {
                          acknowledge(60000, true);
                          Console.WriteLine(DateTime.Now.ToString("HH:mm:ss.ffff") + " received");
                      }, null, "ProcessingGroup", 0);
                  
                  callback(new BinaryMessage { Bytes = new byte[0], Type = typeof(string).Name }, b => {
                      acknowledged=true; 
                      Console.WriteLine(DateTime.Now.ToString("HH:mm:ss.ffff") + " acknowledged"); 
                  });
              }

              Assert.That(acknowledged,Is.True,"Message was not acknowledged on engine dispose");
              Console.WriteLine(acknowledged+" "+Thread.CurrentThread.ManagedThreadId);
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

            using (var processingGroupManager = createProcessingGroupManagerWithMockedDependencies(action => callback = action, action => emulateFail = action, onSubscribe ))
            {
                var subscription =processingGroupManager.Subscribe(new Endpoint("test", "test", false, "fake"), (message, acknowledge) =>
                    {
                        acknowledge(0, true);
                        Console.WriteLine(DateTime.Now.ToString("HH:mm:ss.ffff") + " recieved");
                    }, null, "ProcessingGroup", 0);

                //First attempt fails next one happends in 1000ms and should be successfull
                Assert.That(subscribed.WaitOne(1200), Is.True, "Has not resubscribed after first subscription fail");
                callback(new BinaryMessage {Bytes = new byte[0], Type = typeof (string).Name}, b => Console.WriteLine(DateTime.Now.ToString("HH:mm:ss.ffff") + " acknowledged"));
                Thread.Sleep(300);

                Console.WriteLine("{0:H:mm:ss.fff} Emulating fail", DateTime.Now);
                emulateFail();
                //First attempt is taken right after failure, but it fails next one happends in 1000ms and should be successfull
                Assert.That(subscribed.WaitOne(1500), Is.True, "Resubscription has not happened within resubscription timeout");
                Assert.That(subscriptionsCounter, Is.EqualTo(4));
                
                
                Thread.Sleep(300);
                Console.WriteLine("{0:H:mm:ss.fff} Emulating fail", DateTime.Now);

                emulateFail();
                subscription.Dispose();
                //First attempt is taken right after failure, but it fails next one should not be taken since subscription is disposed
                Assert.That(subscribed.WaitOne(3000), Is.False, "Resubscription happened after subscription was disposed");

            }
        }

        [Test]
        [Ignore("PerformanceTest")]
        public void DeferredAcknowledgementPerformanceTest()
        {
            var rnd = new Random();
            const int messageCount = 100;
            Console.WriteLine(messageCount+" messages");
            var delays = new Dictionary<long, string> { { 0, "immediate acknowledge" }, { 1, "deferred acknowledge" }, { 100, "deferred acknowledge" }, { 1000, "deferred acknowledge" } };
            foreach (var delay in delays)
            {


                Action<BinaryMessage, Action<bool>> callback = null;
                long counter = messageCount;
                var complete = new ManualResetEvent(false);
                var processingGroupManager = createProcessingGroupManagerWithMockedDependencies(action => callback = action);
                Stopwatch sw = Stopwatch.StartNew();
                processingGroupManager.Subscribe(new Endpoint("test", "test", false, "fake"), (message, acknowledge) =>
                    {
                        Thread.Sleep(rnd.Next(1,10));
                        acknowledge(delay.Key, true);
                    }, null, "ProcessingGroup", 0);
                for (int i = 0; i < messageCount; i++)
                    callback(new BinaryMessage {Bytes = new byte[0], Type = typeof (string).Name}, b =>
                        {
                            if (Interlocked.Decrement(ref counter) == 0)
                                complete.Set();
                        });
                complete.WaitOne();
                Console.WriteLine("{0} ({1}ms delay extracted to narrow results): {2}ms ",delay.Value,delay.Key, sw.ElapsedMilliseconds-delay.Key);
                Console.WriteLine(processingGroupManager.GetStatistics());
            }

            
        }


        private static ProcessingGroupManager createProcessingGroupManagerWithMockedDependencies(Action<Action<BinaryMessage, Action<bool>>> setCallback, Action<Action> setOnFail = null, Action onSubscribe = null)
        {

            if (setOnFail == null)
                setOnFail = action => { };
            if (onSubscribe == null)
                onSubscribe = () => { };
            var transportManager = MockRepository.GenerateMock<ITransportManager>();
            var session = MockRepository.GenerateMock<IMessagingSession>();
            session.Expect(p => p.Subscribe("test", null, null))
                           .IgnoreArguments()
                           .WhenCalled(invocation =>
                           {
                               setCallback((Action<BinaryMessage, Action<bool>>) invocation.Arguments[1]);
                               onSubscribe();
                           })
                           .Return(MockRepository.GenerateMock<IDisposable>());
            transportManager.Expect(t => t.GetMessagingSession(null,null, null))
                .IgnoreArguments()
                .WhenCalled(invocation => setOnFail((Action)invocation.Arguments[2]))
                .Return(session);
            return new ProcessingGroupManager(transportManager,new Dictionary<string, ProcessingGroupInfo>
            {
                {"SingleThread",new ProcessingGroupInfo(){ConcurrencyLevel = 1}},
                {"MultiThread",new ProcessingGroupInfo(){ConcurrencyLevel = 3}}
            },1000)
            {
                //Logger = new ConsoleLoggerWithTime()
            };
        }
    }
}
