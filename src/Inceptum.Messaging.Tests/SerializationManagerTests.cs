using System;
using System.Threading;
using System.Threading.Tasks;
using Inceptum.Messaging.Serialization;
using NUnit.Framework;
using Rhino.Mocks;

namespace Inceptum.Messaging.Tests
{
    [TestFixture]
    public class SerializationManagerTests
    {

        [Test]
        public void RegisterSerializersTest()
        {
            var serializationManager = new SerializationManager();
            var serializer = MockRepository.GenerateMock<IMessageSerializer<string>>();
            serializer.Expect(s => s.Serialize("test")).Return(new byte[] { 0x1 });
            serializer.Expect(s => s.Deserialize(new byte[] { 0x1 })).Return("test");
            serializationManager.RegisterSerializer("fake",typeof(string),serializer);

            var stringSerializer = serializationManager.ExtractSerializer<string>("fake");
            
            Assert.That(stringSerializer, Is.Not.Null, "serializer was not cretaed");
            Assert.That(stringSerializer, Is.SameAs(serializer), "Wrong serializer was returned");
            Assert.That(serializationManager.Deserialize<string>("fake",new byte[] { 0x1 }), Is.EqualTo("test"), "Serializer was not used for deserialization");
            Assert.That(serializationManager.Serialize("fake","test"), Is.EqualTo(new byte[] { 0x1 }), "Serializer was not used for deserialization");

        }

            
        [Test]
        public void RegisterSerializerFactoryTest()
        {
            var serializationManager = new SerializationManager();
            var factory = MockRepository.GenerateMock<ISerializerFactory>();
            factory.Expect(f => f.SerializationFormat).Return("fake");
            var serializer = MockRepository.GenerateMock<IMessageSerializer<string>>();
            serializer.Expect(s => s.Serialize("test")).Return(new byte[] {0x1});
            serializer.Expect(s => s.Deserialize(new byte[] { 0x1 })).Return("test");
            factory.Expect(f => f.Create<string>()).Return(serializer);
            serializationManager.RegisterSerializerFactory(factory);

            var stringSerializer = serializationManager.ExtractSerializer<string>("fake");
            
            Assert.That(stringSerializer,Is.Not.Null,"serializer was not cretaed");
            Assert.That(stringSerializer,Is.SameAs(serializer),"Wrong serializer was returned");
            Assert.That(serializationManager.Deserialize<string>("fake",new byte[] { 0x1 }), Is.EqualTo("test"), "Serializer was not used for deserialization");
            Assert.That(serializationManager.Serialize("fake","test"), Is.EqualTo(new byte[] { 0x1 }), "Serializer was not used for deserialization");
        }
             
        [Test]
        [ExpectedException(typeof(ProcessingException))]
        public void SerializerNotRegistedFailureTest()
        {
            var serializationManager = new SerializationManager();
            var factory = MockRepository.GenerateMock<ISerializerFactory>();
            factory.Expect(f => f.SerializationFormat).Return("fake");
            factory.Expect(f => f.Create<int>()).Return(null);
            serializationManager.RegisterSerializerFactory(factory);

            serializationManager.ExtractSerializer<int>("fake");
        }
        
             
        [Test]
        [ExpectedException(typeof(ProcessingException))]
        public void SerializerNotCreatedByFactoryFailureTest()
        {
            var serializationManager = new SerializationManager();
            var factory = MockRepository.GenerateMock<ISerializerFactory>();
            factory.Expect(f => f.SerializationFormat).Return("fake");
            factory.Expect(f => f.Create<string>()).Return(null);
            serializationManager.RegisterSerializerFactory(factory);

            serializationManager.ExtractSerializer<string>("fake");
        }
        
              
        public void SerialiezerShouldBeCreatedOnlyOnceTest()
        {
            var serializationManager = new SerializationManager();
            var factory = MockRepository.GenerateMock<ISerializerFactory>();
            Func<IMessageSerializer<string>> factoryMethod=() => MockRepository.GenerateMock<IMessageSerializer<string>>();
            factory.Expect(f => f.Create<string>()).Do(factoryMethod);
            serializationManager.RegisterSerializerFactory(factory);
            var mre = new ManualResetEvent(false);


            IMessageSerializer<string> serializer1=null;
            IMessageSerializer<string> serializer2=null;
            
            var t1 = Task.Factory.StartNew(() =>
            {
                mre.WaitOne();
                serializer1 = serializationManager.ExtractSerializer<string>("fake");
            });
            var t2 = Task.Factory.StartNew(() =>
            {
                mre.WaitOne();
                serializer2 = serializationManager.ExtractSerializer<string>("fake");
            });
            mre.Set();

            Task.WaitAll(new[] { t1, t2 }, 10000);
            Assert.That(serializer1, Is.SameAs(serializer2), "Previousely created serializer was not reused");
        }

 
    }
}