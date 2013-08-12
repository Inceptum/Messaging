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
using IRegistration = Inceptum.Cqrs.Configuration.IRegistration;


namespace Inceptum.Cqrs
{
    public interface ICqrsEngineBootstrapper
    {
        void Start();
    }

    public class CqrsFacility : AbstractFacility,  ICqrsEngineBootstrapper
    {
        private readonly string m_EngineComponetName = Guid.NewGuid().ToString();
        private readonly Dictionary<IHandler, Action<IHandler>> m_WaitList = new Dictionary<IHandler, Action<IHandler>>();
        private BoundedContextRegistration[] m_BoundedContexts = new BoundedContextRegistration[0];
        private List<SagaRegistration> m_Sagas = new List<SagaRegistration>();


        public CqrsFacility BoundedContexts(params BoundedContextRegistration[] boundedContexts)
        {
            m_BoundedContexts = boundedContexts;
            return this;
        }

        protected override void Init()
        {
            Func<Type, object> dependencyResolver = Kernel.Resolve;
            Kernel.Register(Component.For<ICqrsEngineBootstrapper>().Instance(this));
            Kernel.ComponentRegistered += onComponentRegistered;
            Kernel.HandlersChanged += (ref bool changed) => processWaitList();
        }
 


        private void processWaitList()
        {
            foreach (var pair in m_WaitList.ToArray().Where(pair => pair.Key.CurrentState == HandlerState.Valid && pair.Key.TryResolve(CreationContext.CreateEmpty()) != null))
            {
                pair.Value(pair.Key);
                m_WaitList.Remove(pair.Key);
            }
        }
      


        [MethodImpl(MethodImplOptions.Synchronized)]
        private void onComponentRegistered(string key, IHandler handler)
        {
            if (handler.ComponentModel.Name == m_EngineComponetName)
            {
                var dependencyModels = m_BoundedContexts.Cast<IRegistration>().Concat(m_Sagas).SelectMany(bc => bc.Dependencies).Select(d => new DependencyModel(d.Name, d, false));
                foreach (var dependencyModel in dependencyModels)
                {
                    handler.ComponentModel.Dependencies.Add(dependencyModel);
                }
                return;
            }

            var isCommandsHandler = (bool)(handler.ComponentModel.ExtendedProperties["IsCommandsHandler"] ?? false);
            var isProjection = (bool)(handler.ComponentModel.ExtendedProperties["IsProjection"] ?? false);
            var isSaga = (bool)(handler.ComponentModel.ExtendedProperties["isSaga"] ?? false);
            var isProcess = (bool)(handler.ComponentModel.ExtendedProperties["isProcess"] ?? false);

            if (isCommandsHandler && isProjection)
                throw new InvalidOperationException("Component can not be projection and commands handler simultaneousely");

            if (isProjection)
            {
                var projectedBoundContext = (string)(handler.ComponentModel.ExtendedProperties["ProjectedBoundContext"]);
                var boundContext = (string)(handler.ComponentModel.ExtendedProperties["BoundContext"]);
                var registration = m_BoundedContexts.FirstOrDefault(bc => bc.Name == boundContext);
                if (registration == null)
                    throw new ComponentRegistrationException(string.Format("Bounded context {0} was not registered", projectedBoundContext));
                if (registration is RemoteBoundedContextRegistration)
                    throw new ComponentRegistrationException(string.Format("Projection can be registered only for local bounded contexts. Bounded context {0} is remote", registration.Name));
          
                //TODO: decide which service to use
                (registration as LocalBoundedContextRegistration).WithProjection(handler.ComponentModel.Services.First(), projectedBoundContext);
 
            }


           if (isCommandsHandler)
           {
               var commandsHandlerFor = (string)(handler.ComponentModel.ExtendedProperties["CommandsHandlerFor"]);

               var registration = m_BoundedContexts.FirstOrDefault(bc => bc.Name == commandsHandlerFor);
               if(registration==null)
                   throw new ComponentRegistrationException(string.Format("Bounded context {0} was not registered",commandsHandlerFor)); 
               if(registration is RemoteBoundedContextRegistration)
                   throw new ComponentRegistrationException(string.Format("Commands handler can be registered only for local bounded contexts. Bounded context {0} is remote",commandsHandlerFor));
          
               //TODO: decide which service to use
               (registration as LocalBoundedContextRegistration).WithCommandsHandler(handler.ComponentModel.Services.First());
 
           } 

            if (isSaga)
            {
                var listenedBoundContexts = (string[])(handler.ComponentModel.ExtendedProperties["ListenedBoundContexts"]);
                m_Sagas.Add(Saga.OfType(handler.ComponentModel.Services.First()).Listening(listenedBoundContexts));
            }

            if (isProcess)
            {
                var processFor = (string)(handler.ComponentModel.ExtendedProperties["ProcessFor"]);
                var registration = m_BoundedContexts.FirstOrDefault(bc => bc.Name == processFor);
                if (registration == null)
                    throw new ComponentRegistrationException(string.Format("Bounded context {0} was not registered", processFor));
                if (registration is RemoteBoundedContextRegistration)
                    throw new ComponentRegistrationException(string.Format("Process can be registered only for local bounded contexts. Bounded context {0} is remote", processFor));

                (registration as LocalBoundedContextRegistration).WithProcess(handler.ComponentModel.Services.First());
            }

        }


        public void Start()
        {
            Func<Type, object> dependencyResolver = Kernel.Resolve;
            Kernel.Register(Component.For<ICommandSender>().ImplementedBy<CommandSender>().Named(m_EngineComponetName).DependsOn(new
                {
                    registrations = m_BoundedContexts.Cast<IRegistration>().Concat(m_Sagas).ToArray(),
                    dependencyResolver
                }));
            Kernel.Resolve<ICommandSender>();
        }
    }


}