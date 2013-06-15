using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Castle.Core;
using Castle.MicroKernel;
using Castle.MicroKernel.Context;
using Castle.MicroKernel.Facilities;
using Castle.MicroKernel.Registration;
using Inceptum.Cqrs.Configuration;


 

namespace Inceptum.Cqrs
{



    public class CqrsFacility : AbstractFacility, ISubDependencyResolver
    {
        private readonly Dictionary<IHandler, Action<IHandler>> m_WaitList = new Dictionary<IHandler, Action<IHandler>>();
        private ICqrsEngine m_CqrsEngine;
        private BoundedContextRegistration[] m_BoundedContexts = new BoundedContextRegistration[0];


        public CqrsFacility BoundedContexts(params BoundedContextRegistration[] boundedContexts)
        {
            m_BoundedContexts = boundedContexts;
            return this;
        }

        protected override void Init()
        {
            Kernel.Register(Component.For<ICqrsEngine>().ImplementedBy<CqrsEngine>().DependsOn(new
                {
                    registrations = m_BoundedContexts
                }));
            Kernel.Resolver.AddSubResolver(this);
            Kernel.ComponentRegistered += onComponentRegistered;
            Kernel.HandlersChanged += (ref bool changed) => processWaitList();
            m_CqrsEngine = Kernel.Resolve<ICqrsEngine>();
            //Trigger startable facility (it tries to start components on Kernel.ComponentRegistered)
            m_CqrsEngine.Initialized += () => Kernel.Register(Component.For<CqrsEngineBarier>());
        }

        private class CqrsEngineBarier
        {
        }


        private void processWaitList()
        {
            foreach (var pair in m_WaitList.ToArray().Where(pair => pair.Key.CurrentState == HandlerState.Valid && pair.Key.TryResolve(CreationContext.CreateEmpty()) != null))
            {
                pair.Value(pair.Key);
                m_WaitList.Remove(pair.Key);
            }
        }

        private void registerEventsListener(IHandler handler)
        {
            m_CqrsEngine.WireEventsListener(handler.Resolve(CreationContext.CreateEmpty()));
        }

        private void registerIsCommandsHandler(IHandler handler, string localBoundedContext)
        {

            object commandsHandler = handler.Resolve(CreationContext.CreateEmpty());
            m_CqrsEngine.WireCommandsHandler(commandsHandler, localBoundedContext);
        }


        [MethodImpl(MethodImplOptions.Synchronized)]
        private void onComponentRegistered(string key, IHandler handler)
        {
            var isEventsListener = (bool) (handler.ComponentModel.ExtendedProperties["IsEventsListener"] ?? false);
            var commandsHandlerFor = (string) (handler.ComponentModel.ExtendedProperties["CommandsHandlerFor"]);
            var isCommandsHandler = commandsHandlerFor != null;

            if (isCommandsHandler && isEventsListener)
                throw new InvalidOperationException("Component can not be events listener and commands handler simultaneousely");

            if (isEventsListener)
            {
                if (handler.CurrentState == HandlerState.WaitingDependency)
                {
                    m_WaitList.Add(handler, registerEventsListener);
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
                    m_WaitList.Add(handler, h => registerIsCommandsHandler(h, commandsHandlerFor));
                }
                else
                {
                    registerIsCommandsHandler(handler, commandsHandlerFor);
                }
            }

        }

        public bool CanResolve(CreationContext context, ISubDependencyResolver contextHandlerResolver, ComponentModel model, DependencyModel dependency)
        {
            return dependency.TargetItemType == typeof (ICqrsEngine) ; 
        }

        public object Resolve(CreationContext context, ISubDependencyResolver contextHandlerResolver, ComponentModel model, DependencyModel dependency)
        {
            return m_CqrsEngine.IsInitialized?m_CqrsEngine:null;
        }
    }


}