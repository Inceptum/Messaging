using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Rhino.Mocks;

namespace Inceptum.Messaging.Tests
{
    [TestFixture]
    [Ignore]
    public class InvestigationTests
    {
        [Test]
        public void DeserializeTest()
        {
            var bytes = new byte[] {0x1};
            var manager = MockRepository.GenerateMock<ISerializationManager>();
            manager.Expect(m => m.Deserialize<string>(bytes)).Return("test");
            var deserialized = manager.Deserialize(bytes, typeof (string));
            Assert.That(deserialized,Is.EqualTo("test"));
        }

        [Test]
        public void SerializeTest()
        {
            var bytes = new byte[] {0x1};
            var manager = MockRepository.GenerateMock<ISerializationManager>();
            manager.Expect(m => m.Serialize("test")).Return(bytes);
            var serialized = manager.SerializeObject("test");
            Assert.That(serialized, Is.EqualTo(bytes));
        }
    }
}