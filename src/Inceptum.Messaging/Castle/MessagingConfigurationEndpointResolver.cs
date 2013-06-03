using System;
using System.Collections.Generic;
using Castle.Core;
using Castle.MicroKernel;
using Castle.MicroKernel.Context;
using Castle.MicroKernel.ModelBuilder.Descriptors;
using Castle.MicroKernel.Registration;
using Inceptum.Messaging.Configuration;
using Inceptum.Messaging.Contract;

namespace Inceptum.Messaging.Castle
{
    public static class EndpointsDependencyExtensions
    {
        public static ComponentRegistration<T> WithEndpoints<T>(this ComponentRegistration<T> r, object endpoints)
             where T : class
        {
            var dictionary = new ReflectionBasedDictionaryAdapter(endpoints);

            var explicitEndpoints = new Dictionary<string, Endpoint>();
            var endpointNames = new Dictionary<string, string>();
            foreach (var key in dictionary.Keys)
            {
                var dependencyName = key.ToString();
                if (dictionary[key] is Endpoint)
                {
                    var endpoint = (Endpoint)dictionary[key];
                    explicitEndpoints[dependencyName] = endpoint;
                }
                if (dictionary[key] is string)
                {
                    var endpointName = (string)dictionary[key];
                    endpointNames[dependencyName] = endpointName;
                }
            }

            return r.AddDescriptor(new CustomDependencyDescriptor(explicitEndpoints)).ExtendedProperties(new { endpointNames });

        }
    }

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
