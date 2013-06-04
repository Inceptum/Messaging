using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Castle.Core;
using Castle.MicroKernel;
using Castle.MicroKernel.Facilities;
using Castle.MicroKernel.Registration;
using Inceptum.Messaging.Contract;


namespace Castle.MicroKernel.Registration
{
    public static class ComponentRegistrationExtensions
    {
        public static ComponentRegistration<T> AsEventListener<T>(this ComponentRegistration<T> registration) where T : class
        {
            return registration.ExtendedProperties(new { IsEventListener=true });
        }
    }
}

namespace Inceptum.Cqrs
{



    public class CqrsFacility:AbstractFacility
    {
        private Endpoint[] m_EventEndpoints=new Endpoint[0];
        private Endpoint[] m_CommandEndpoints = new Endpoint[0];
        private Type[] m_EventTypes;
        private Type[] m_CommandTypes;
        private readonly List<IHandler> m_EventListenerWaitList = new List<IHandler>();
        private ICqrsEngine m_CqrsEngine;


        protected override void Init()
        {
            Action<Configurator> config = c => { };
            Kernel.Register(Component.For<ICqrsEngine>().ImplementedBy<CqrsEngine>().DependsOn(new
                {
                    config
                }));

            Kernel.ComponentRegistered += onComponentRegistered;
            Kernel.HandlersChanged += (ref bool changed) => processWaitList();
            m_CqrsEngine = Kernel.Resolve<ICqrsEngine>();
        }

       

        private void processWaitList()
        {

            foreach (var factoryHandler in m_EventListenerWaitList.ToArray().Where(factoryHandler => factoryHandler.CurrentState == HandlerState.Valid))
            {
                registerEventListener(factoryHandler);
                m_EventListenerWaitList.Remove(factoryHandler);
            }
        }

        private void registerEventListener(IHandler handler)
        {
            m_CqrsEngine.EventDispatcher.Wire(Kernel.Resolve(handler.ComponentModel.Name, handler.ComponentModel.Services.First()));
        }


        [MethodImpl(MethodImplOptions.Synchronized)]
        private void onComponentRegistered(string key, IHandler handler)
        {


            if ((bool)(handler.ComponentModel.ExtendedProperties["IsEventListener"] ?? false))
            {
                if (handler.CurrentState == HandlerState.WaitingDependency)
                {
                    m_EventListenerWaitList.Add(handler);
                }
                else
                {
                    registerEventListener(handler);
                }
            }

            processWaitList();
        }
    }
}