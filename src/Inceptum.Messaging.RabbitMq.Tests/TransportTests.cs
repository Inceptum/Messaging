using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Inceptum.Messaging.Transports;
using NUnit.Framework;

namespace Inceptum.Messaging.RabbitMq.Tests
{
    [TestFixture]
    public class TransportTests
    {
        [SetUp]
        public void Setup()
        {
             
        }

        [Test]
        public void SendTest()
        {
            var transport = new Transport("localhost", "guest", "guest");
            transport.Send("test",new BinaryMessage(){Bytes =new byte[]{0x0,0x1,0x2},Type = typeof(byte[]).AssemblyQualifiedName},0);
        }
    }
}
