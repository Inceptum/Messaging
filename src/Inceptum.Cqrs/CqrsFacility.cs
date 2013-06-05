using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Castle.MicroKernel;
using Castle.MicroKernel.Facilities;
using Castle.MicroKernel.Registration;


namespace Castle.MicroKernel.Registration
{
    public static class ComponentRegistrationExtensions
    {
        public static ComponentRegistration<T> AsEventsListener<T>(this ComponentRegistration<T> registration) where T : class
        {
            return registration.ExtendedProperties(new { IsEventsListener=true });
        }  
        
        public static ComponentRegistration<T> AsCommandsHandler<T>(this ComponentRegistration<T> registration) where T : class
        {
            return registration.ExtendedProperties(new { IsCommandsHandler = true });
        }
    }
}

namespace Inceptum.Cqrs
{



    public class CqrsFacility:AbstractFacility
    {
        private readonly Dictionary<IHandler, Action<IHandler>> m_WaitList = new Dictionary<IHandler, Action<IHandler>>();
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
            foreach (var pair in m_WaitList.ToArray().Where(pair => pair.Key.CurrentState == HandlerState.Valid))
            {
                pair.Value(pair.Key);
                m_WaitList.Remove(pair.Key);
            }
        }

        private void registerEventsListener(IHandler handler)
        {
            m_CqrsEngine.EventDispatcher.Wire(Kernel.Resolve(handler.ComponentModel.Name, handler.ComponentModel.Services.First()));
        }

        private void registerIsCommandsHandler(IHandler handler)
        {
            m_CqrsEngine.CommandDispatcher.Wire(Kernel.Resolve(handler.ComponentModel.Name, handler.ComponentModel.Services.First()));
        }


        [MethodImpl(MethodImplOptions.Synchronized)]
        private void onComponentRegistered(string key, IHandler handler)
        {
            var isEventsListener = (bool) (handler.ComponentModel.ExtendedProperties["IsEventsListener"] ?? false);
            var isCommandsHandler = (bool)(handler.ComponentModel.ExtendedProperties["IsCommandsHandler"] ?? false);

            if(isCommandsHandler&& isEventsListener)
                throw new InvalidOperationException("Class can not be events listener and commands handler simultaneousely");
            
            if (isEventsListener)
            {
                if (handler.CurrentState == HandlerState.WaitingDependency)
                {
                    m_WaitList.Add(handler,registerEventsListener);
                }
                else
                {
                    registerEventsListener(handler);
                }
            }


            if (isCommandsHandler)
            {
                if (handler.CurrentState == HandlerState.WaitingDependency)
                {
                    m_WaitList.Add(handler, registerIsCommandsHandler);
                }
                else
                {
                    registerIsCommandsHandler(handler);
                }
            }

            processWaitList();
        }

        
    }
}