using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Inceptum.Cqrs.Tests
{
    class Handler 
    {
        public Handler(bool fail=false)
        {
            m_Fail = fail;
        }

        public readonly List<object> HandledEvents=new List<object>();
        private readonly bool m_Fail;

        public void Handle(string e)
        {
            HandledEvents.Add(e);
            if (m_Fail)
                throw new Exception();
        }        
        
        
        public void Handle(int e)
        {
            HandledEvents.Add(e);
            if (m_Fail)
                throw new Exception();
        }
    }   
    

    [TestFixture]
    public class EventDispatcherTests
    {
       
        [Test]
        public void WireTest()
        {
            var dispatcher=new EventDispatcher();
            var handler = new Handler();
            dispatcher.Wire(handler);
            dispatcher.Dispacth("test","testBC");
            dispatcher.Dispacth(1, "testBC");
            Assert.That(handler.HandledEvents, Is.EquivalentTo(new object[] { "test" ,1}), "Some events were not dispatched");
        }

        [Test]
        public void MultipleHandlersDispatchTest()
        {
            var dispatcher=new EventDispatcher();
            var handler1 = new Handler();
            var handler2 = new Handler();
            dispatcher.Wire(handler1);
            dispatcher.Wire(handler2);
            dispatcher.Dispacth("test", "testBC");
            Assert.That(handler1.HandledEvents, Is.EquivalentTo(new[] { "test" }), "Event was not dispatched");
            Assert.That(handler2.HandledEvents, Is.EquivalentTo(new[] { "test" }), "Event was not dispatched");
        }
    }
}
