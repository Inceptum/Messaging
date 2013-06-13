using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Inceptum.Cqrs.Configuration;
using NUnit.Framework;

namespace Inceptum.Cqrs.Tests
{
    class CommandHandler
    {
        private readonly ICqrsEngine m_Engine;
        private int counter = 0;

        public CommandHandler(ICqrsEngine engine)
        {
            m_Engine = engine;
        }

        public void Handle(string command)
        {
            Console.WriteLine("command recived:" +command);
            m_Engine.PublishEvent(++counter, "local");
        } 
        
        public void Handle(DateTime command)
        {
            Console.WriteLine("command recived:" +command);
            m_Engine.PublishEvent(++counter,"local");
        }
    }

    class EventsListener
    {
        public void Handle(int e,string boundContext)
        {
            Console.WriteLine(boundContext+":"+e);
        }  
    }

    [TestFixture]
    public class CqrsEngineTests
    {
        [Test]
        public void Method_Scenario_Expected()
        {
            var engine = new CqrsEngine(BoundContext.Local("local")
                                                    .PublishingEvents(typeof (int)).To("events").RoutedTo("eventQueue")//RoutedToSameEndpoint()
                                                    .ListeningCommands(typeof(string)).On("commands1").RoutedFromSameEndpoint()
                                                    .ListeningCommands(typeof(DateTime)).On("commands2").RoutedFromSameEndpoint()/*
                                                    .WithEventSource()
                                                    .WithAggregates()
                                                    .WithDocumentStore()*/);
            engine
                .WireCommandsHandler(new CommandHandler(engine),"local")
                .WireEventsListener(new EventsListener());
            engine.Init();
            
            engine.SendCommand("test","local");
            engine.SendCommand(DateTime.Now,"local");

            Thread.Sleep(500);
        }
    }

}