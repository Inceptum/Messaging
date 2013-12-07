using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Castle.Core.Internal;
using Inceptum.Messaging.Transports;
using NUnit.Framework;

namespace Inceptum.Messaging.Tests
{
    // ReSharper disable InconsistentNaming
    // ReSharper disable PossibleNullReferenceException

    [TestFixture]
    public class TransportManagerTests
    {
        [Test]
        public void SameThreadSubscriptionTest()
        {
            var manager=new TransportManager(
                new TransportResolver(new Dictionary<string, TransportInfo>
                    {
                        {"transport-1", new TransportInfo("transport-1", "login1", "pwd1", "None", "InMemory")}
                    }));

            var processingGroup = manager.GetProcessingGroup("transport-1", "pg");
            var usedThreads=new List<int>();
            var subscription = processingGroup.Subscribe("queue", (message, action) =>
                {
                    lock (usedThreads)
                    {
                        usedThreads.Add(Thread.CurrentThread.ManagedThreadId);
                        Console.WriteLine(Thread.CurrentThread.Name + Thread.CurrentThread.ManagedThreadId);
                    }
                    Thread.Sleep(50);
                }, null,0);

            Enumerable.Range(1, 20).ForEach(i => processingGroup.Send("queue", new BinaryMessage(), 0));

            Thread.Sleep(1200);
            Assert.That(usedThreads.Count(), Is.EqualTo(20), "not all messages were processed");
            Assert.That(usedThreads.Distinct().Count(),Is.EqualTo(1), "more then one thread was used for message processing");
        }
  
        [Test]
        public void MultiThreadThreadSubscriptionTest()
        {
            var manager=new TransportManager(
                new TransportResolver(new Dictionary<string, TransportInfo>
                    {
                        {"transport-1", new TransportInfo("transport-1", "login1", "pwd1", "None", "InMemory")
                            {
                                ProcessingGroups=new Dictionary<string, ProcessingGroupInfo>(){
                                    {
                                        "pg",new ProcessingGroupInfo(){ConcurrencyLevel = 3}
                                    }}
                            }}
                    }));

            var processingGroup = manager.GetProcessingGroup("transport-1", "pg");
            var usedThreads=new List<int>();
            var subscription = processingGroup.Subscribe("queue", (message, action) =>
                {
                    lock (usedThreads)
                    {
                        usedThreads.Add(Thread.CurrentThread.ManagedThreadId);
                        Console.WriteLine(Thread.CurrentThread.Name + Thread.CurrentThread.ManagedThreadId + ":" + Encoding.UTF8.GetString(message.Bytes));
                    }
                    Thread.Sleep(50);
                }, null,0);


            Enumerable.Range(1, 20).ForEach(i => processingGroup.Send("queue", new BinaryMessage { Bytes = Encoding.UTF8.GetBytes((i % 3).ToString()) }, 0));

            Thread.Sleep(1200);
            Assert.That(usedThreads.Count(),Is.EqualTo(20), "not all messages were processed");
            Assert.That(usedThreads.Distinct().Count(),Is.EqualTo(3), "wrong number of threads was used for message processing");
        }

        [Test]
        public void MultiThreadPrioritizedThreadSubscriptionTest()
        {
            var manager=new TransportManager(
                new TransportResolver(new Dictionary<string, TransportInfo>
                    {
                        {"transport-1", new TransportInfo("transport-1", "login1", "pwd1", "None", "InMemory")
                            {
                                ProcessingGroups=new Dictionary<string, ProcessingGroupInfo>(){
                                    {
                                        "pg",new ProcessingGroupInfo(){ConcurrencyLevel = 3}
                                    }}
                            }}
                    }));

            var processingGroup = manager.GetProcessingGroup("transport-1", "pg");
            var usedThreads=new List<int>();
            var processedMessages=new List<int>();
            Action<BinaryMessage, Action<bool>> callback = (message, action) =>
                {
                    lock (usedThreads)
                    {
                        usedThreads.Add(Thread.CurrentThread.ManagedThreadId);
                        processedMessages.Add(int.Parse(Encoding.UTF8.GetString(message.Bytes)));
                        Console.WriteLine(Thread.CurrentThread.ManagedThreadId + ":" + Encoding.UTF8.GetString(message.Bytes));
                    }
                    Thread.Sleep(50);
                };
            var subscription0 = processingGroup.Subscribe("queue0", callback, null,0);
            var subscription1 = processingGroup.Subscribe("queue1", callback, null,1);
            var subscription2 = processingGroup.Subscribe("queue2", callback, null,2);


            Enumerable.Range(1, 20).ForEach(i => processingGroup.Send("queue" + i % 3, new BinaryMessage { Bytes = Encoding.UTF8.GetBytes((i % 3).ToString()) }, 0));

            Thread.Sleep(1200);
            Assert.That(usedThreads.Count(),Is.EqualTo(20), "not all messages were processed");
            Assert.That(usedThreads.Distinct().Count(),Is.EqualTo(3), "wrong number of threads was used for message processing");

            double averageOrder0 = processedMessages.Select((i, index) => new { message = i, index }).Where(arg => arg.message == 0).Average(arg => arg.index);
            double averageOrder1 = processedMessages.Select((i, index) => new { message = i, index }).Where(arg => arg.message == 1).Average(arg => arg.index);
            double averageOrder2 = processedMessages.Select((i, index) => new { message = i, index }).Where(arg => arg.message == 2).Average(arg => arg.index);
            Assert.That(averageOrder0,Is.LessThan(averageOrder1), "priority was not respected");
            Assert.That(averageOrder1,Is.LessThan(averageOrder2), "priority was not respected");
 

        }

    }

    // ReSharper restore InconsistentNaming
    // ReSharper restore PossibleNullReferenceException
}