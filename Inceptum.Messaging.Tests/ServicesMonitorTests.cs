/*
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using Inceptum.Core.Messaging;
using Inceptum.Messaging.HeartBeats;
using NUnit.Framework;
using Rhino.Mocks;
using Sonic.Jms;

namespace Inceptum.Messaging.Tests
{
    internal class FakeHbMessageSerializer : IMessageSerializer<HbMessage>
    {
        #region IMessageSerializer<string> Members

        public Message Serialize(HbMessage message, Session sendSession)
        {
            BytesMessage mess = sendSession.createBytesMessage();
            var bf = new BinaryFormatter();
            var ms = new MemoryStream();
            bf.Serialize(ms, message);
            mess.writeBytes(ms.ToArray());
            return mess;
        }

        public HbMessage Deserialize(Message message)
        {
            var bytesMessage = message as BytesMessage;
            if (bytesMessage == null)
                return null;
            var buf = new byte[bytesMessage.getBodyLength()];
            bytesMessage.readBytes(buf);

            var memStream = new MemoryStream();
            var binForm = new BinaryFormatter();
            memStream.Write(buf, 0, buf.Length);
            memStream.Seek(0, SeekOrigin.Begin);
            return (HbMessage)binForm.Deserialize(memStream);
        }

        #endregion
    }
    [TestFixture]
    public class ServicesMonitorTests
    {
        [Test]
        public void InstanceTrackingTest()
        {
            var engine = MockRepository.GenerateMock<IMessagingEngine>();
            Action<HbMessage> callback = null;
            engine.Expect(e => e.Subscribe<HbMessage>("dest", TransportConstants.TRANSPORT_ID, null)).IgnoreArguments()
                .Callback<string, string, Action<HbMessage>>(
                    (s, t, c) =>
                        {
                            callback = c;
                            return true;
                        });

            engine.Expect(e => e.Send(new HbMessage(), "dest", TransportConstants.TRANSPORT_ID)).IgnoreArguments()
                .Callback<HbMessage, string, string>(
                    (m, d, t) =>
                        {
                            Console.WriteLine("- HB from " + m.InstanceName);
                            callback(m);
                            return true;
                        });


            symulateTwoInstancesHb(engine);
        }   
        
        [Test]
        public void RealTransportTest()
        {
            var serializationManager = new SerializationManager();
            serializationManager.RegisterSerializer(typeof(HbMessage), new FakeHbMessageSerializer());

            ITransportResolver resolver = MessagingEngineTests.MockTransportResolver();
            var engine = new MessagingEngine(resolver, serializationManager, JailStrategy.MachineName);
            symulateTwoInstancesHb(engine);
        }

        private static void symulateTwoInstancesHb(IMessagingEngine engine)
        {
            using (var monitor = new ServicesMonitor(engine, "topic://dest", TransportConstants.TRANSPORT_ID))
            {
                using (new HbSender(engine, "topic://dest", TransportConstants.TRANSPORT_ID, 100, "instance1"))
                {
                    using (new HbSender(engine, "topic://dest", TransportConstants.TRANSPORT_ID, 230, "instance2"))
                    {
                        Thread.Sleep(500);
                    }
                }

                Assert.That(monitor.Instances.Keys, Is.EqualTo(new[] {"instance1", "instance2"}), "Instances were recordered incorrectly");
                Assert.That(monitor.Instances.Values, Is.EqualTo(new[] {true, true}), "Some instances are reported as unavailable while there were HB within x2 hb interval");
                Thread.Sleep(250);
                Assert.That(monitor.Instances["instance1"], Is.False, "Instance is reported as available while there were no HB within x2 hb interval");
                Thread.Sleep(250);
                Assert.That(monitor.Instances["instance2"], Is.False, "Instance is reported as available while there were no HB within x2 hb interval");
            }
        }
    }
}
*/
