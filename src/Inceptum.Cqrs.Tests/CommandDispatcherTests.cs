using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Inceptum.Cqrs.Tests
{

    [TestFixture]
    public class CommandDispatcherTests
    {

        [Test]
        public void WireTest()
        {
            var dispatcher = new CommandDispatcher("testBC");
            var handler = new Handler();
            dispatcher.Wire(handler);
            dispatcher.Dispacth("test");
            dispatcher.Dispacth(1);
            Assert.That(handler.HandledEvents, Is.EquivalentTo(new object[] { "test", 1 }), "Some commands were not dispatched");
        }

        [Test]
        [ExpectedException(ExpectedException = typeof(InvalidOperationException), ExpectedMessage = "Only one handler per command is allowed. Command System.String handler is already registered in bound context testBC. Can not register Inceptum.Cqrs.Tests.Handler as handler for it")]
        public void MultipleHandlersAreNotAllowedDispatchTest()
        {
            var dispatcher = new CommandDispatcher("testBC");
            var handler1 = new Handler();
            var handler2 = new Handler();
            dispatcher.Wire(handler1);
            dispatcher.Wire(handler2);
        }


        [Test]
        [ExpectedException(ExpectedException = typeof(InvalidOperationException), ExpectedMessage = "Failed to handle command testCommand in bound context testBC, no handler was registered for it")]
        public void DispatchOfUnknownCommandShouldFailTest()
        {
            var dispatcher = new CommandDispatcher("testBC");
            dispatcher.Dispacth("testCommand");
        }
    }
}
