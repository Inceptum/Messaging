using System;
using System.Collections.Generic;
using Castle.Core;
using Castle.MicroKernel;
using Castle.MicroKernel.Context;
using Inceptum.Messaging.Configuration;
using Inceptum.Messaging.Contract;

namespace Inceptum.Messaging.Castle
{
    internal class MessagingConfigurationEndpointResolver : ISubDependencyResolver
    {
        private readonly IMessagingConfiguration m_MessagingConfiguration;
        
        public MessagingConfigurationEndpointResolver(IMessagingConfiguration messagingConfiguration)
        {
            if (messagingConfiguration == null) throw new ArgumentNullException("messagingConfiguration");
            m_MessagingConfiguration = messagingConfiguration;
        }

        public bool CanResolve(CreationContext context, ISubDependencyResolver parentResolver,
                               ComponentModel model, DependencyModel dependency)
        {
            if (dependency.TargetItemType != typeof(Endpoint)) return false;

            var endpointName = getEndpointName(model, dependency);
            return m_MessagingConfiguration.HasEndpoint(endpointName);
        }

        public object Resolve(CreationContext context, ISubDependencyResolver parentResolver,
                              ComponentModel model, DependencyModel dependency)
        {
            var endpointName = getEndpointName(model, dependency);
            return m_MessagingConfiguration.GetEndpoint(endpointName);
        }

        private static string getEndpointName(ComponentModel model, DependencyModel dependency)
        {
            var endpointName = dependency.DependencyKey;
            if (model.ExtendedProperties.Contains("endpointNames"))
            {
                var endpointNames = (IDictionary<string, string>)model.ExtendedProperties["endpointNames"];
                if (endpointNames.ContainsKey(dependency.DependencyKey))
                {
                    endpointName = endpointNames[dependency.DependencyKey];
                }
            }
            return endpointName;
        }
    }
}
