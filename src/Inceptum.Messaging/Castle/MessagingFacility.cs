using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Runtime.CompilerServices;
using Castle.Core;
using Castle.MicroKernel;
using Castle.MicroKernel.Facilities;
using Castle.MicroKernel.Registration;
using Inceptum.Core;
using Inceptum.Messaging.Configuration;
using Inceptum.Messaging.Contract;
using Inceptum.Messaging.Serialization;

namespace Inceptum.Messaging.Castle
{
    public class MessagingFacility : AbstractFacility
    {
        private IDictionary<string, TransportInfo> m_Transports;
        private IDictionary<string, JailStrategy> m_JailStrategies;
        private readonly List<IHandler> m_SerializerWaitList = new List<IHandler>();
        private readonly List<IHandler> m_SerializerFactoryWaitList = new List<IHandler>();
        private ISerializationManager m_SerializationManager;

        public IDictionary<string, TransportInfo> Transports
        {
            get { return m_Transports; }
            set { m_Transports = value; }
        }

        public IDictionary<string, JailStrategy> JailStrategies
        {
            get { return m_JailStrategies; }
            set { m_JailStrategies = value; }
        }

        public IMessagingConfiguration MessagingConfiguration { get; set; }

        public MessagingFacility()
        {
        }

        public MessagingFacility(IDictionary<string, TransportInfo> transports, IDictionary<string, JailStrategy> jailStrategies = null)
        {
            m_Transports = transports;
            m_JailStrategies = jailStrategies;
        }

        protected override void Init()
        {
            var messagingConfiguration = MessagingConfiguration;
            var transports = m_Transports;

            if (messagingConfiguration != null && transports != null)
                throw new Exception("Messaging facility can be configured via transports parameter or via MessagingConfiguration property, not both.");

            Kernel.Register(
                Component.For<IMessagingEngine>().ImplementedBy<MessagingEngine>()
                );
            
            if (messagingConfiguration != null)
            {
                transports = messagingConfiguration.GetTransports();
                Kernel.Resolver.AddSubResolver(new MessagingConfigurationEndpointResolver(messagingConfiguration));
            }

            if (transports != null)
            {
                Kernel.Register(Component.For<ITransportResolver>().ImplementedBy<TransportResolver>().DependsOn(new { transports, jailStrategies = m_JailStrategies }));
            }
            

            m_SerializationManager = Kernel.Resolve<IMessagingEngine>().SerializationManager;
            Kernel.ComponentRegistered += onComponentRegistered;
            Kernel.ComponentModelCreated += ProcessModel;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void onComponentRegistered(string key, IHandler handler)
        {
         

            if ((bool)(handler.ComponentModel.ExtendedProperties["IsSerializerFactory"] ?? false))
            {
                if (handler.CurrentState == HandlerState.WaitingDependency)
                {
                    m_SerializerFactoryWaitList.Add(handler);
                }
                else
                {
                    registerSerializerFactory(handler);
                }
            }

            processWaitList();
        }

        private void registerSerializerFactory(IHandler handler)
        {
            m_SerializationManager.RegisterSerializerFactory(Kernel.Resolve(handler.ComponentModel.Name,typeof(ISerializerFactory)) as ISerializerFactory);
        }
 

        private void onHandlerStateChanged(object source, EventArgs args)
        {
            processWaitList();
        }




        private void processWaitList()
        {
            
            foreach (var factoryHandler in m_SerializerFactoryWaitList.ToArray().Where(factoryHandler => factoryHandler.CurrentState == HandlerState.Valid))
            {
                registerSerializerFactory(factoryHandler);
                m_SerializerWaitList.Remove(factoryHandler);
            }
        }


        public void ProcessModel(ComponentModel model)
        {
          
            
            if (model.Services.Contains(typeof(ISerializerFactory)))
            {
                model.ExtendedProperties["IsSerializerFactory"] = true;
            }
            else
            {
                model.ExtendedProperties["IsSerializerFactory"] = false;
            }
        }
    }
}