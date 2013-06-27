using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Inceptum.Messaging.Contract;
using NUnit.Framework;

namespace Inceptum.Messaging.RabbitMq.Tests
{
    [TestFixture]
    public class MessagingEngineTests
    {
        [Test]
        [Timeout(5000)]
        public void UnknownMessageTest()
        {
            ITransportResolver transportResolver = new TransportResolver(new Dictionary<string, TransportInfo>()
                {
                    {"main",new TransportInfo("localhost", "guest", "guest", "None", "RabbitMq")}
                });
            var eq = new Endpoint("main", "_TEST_QUEUE", true, "json");
            var ee = new Endpoint("main", "_TEST_EXCHANGE", true, "json");
           
            using (var me = new MessagingEngine(transportResolver, new RabbitMqTransportFactory()))
            {
                me.Send("string value", ee);
            }

            using (var me = new MessagingEngine(transportResolver, new RabbitMqTransportFactory()))
            {
                me.Subscribe<int>(eq, Console.WriteLine);
                me.Subscribe<double>(eq, Console.WriteLine);
                me.Subscribe<string>(eq, Console.WriteLine);
            }
            Thread.Sleep(200);
        } 
        
        
    }
}
