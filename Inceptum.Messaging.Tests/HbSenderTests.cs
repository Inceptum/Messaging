/*
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Inceptum.Core.Messaging;
using Inceptum.Messaging.HeartBeats;
using NUnit.Framework;
using Rhino.Mocks;

namespace Inceptum.Messaging.Tests
{
    [TestFixture]
    public class HbSenderTests
    {
        [Test]
        public void SendTest()
        {
            var sentMessageCount = 0;
            var delays=new List<long>();
            var engine = MockRepository.GenerateMock<IMessagingEngine>();
            var sw = new Stopwatch();
            engine.Expect(e => e.Send(new HbMessage(), "dest", TransportConstants.TRANSPORT_ID)).IgnoreArguments()
                .Callback<HbMessage, string, string>(
                    (m, d, t) =>
                        {
                            long delay = sw.ElapsedMilliseconds-delays.Sum();
                            delays.Add(delay);
                            Console.WriteLine(string.Format("{0}. from {1} sent {2:h:mm:ss fff} recieved {3:h:mm:ss fff} delay {4}", ++sentMessageCount, m.InstanceName, m.SendDate, DateTime.Now, delay));
                            return true;
                        });
            sw.Start();
            using (new HbSender(engine, "dest", TransportConstants.TRANSPORT_ID, 100))
            {
                Thread.Sleep(500);
            }

            var afterDispose = sentMessageCount;
            Thread.Sleep(500);


            Assert.That(afterDispose, Is.InRange(3,5), "HB messages were not sent");
            Assert.That(delays.All(d=>100<=d && d<160), Is.True, "Heartbeats were sent with wrong interval");
            Assert.That(sentMessageCount, Is.EqualTo(afterDispose), "HB messages were sent after instance is disposed");
        }
    }
}
*/
