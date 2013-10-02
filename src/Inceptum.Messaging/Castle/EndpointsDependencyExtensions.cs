using System.Collections;
using System.Collections.Generic;
using Castle.Core;
using Castle.MicroKernel.ModelBuilder.Descriptors;
using Castle.MicroKernel.Registration;
using Inceptum.Messaging.Contract;

namespace Inceptum.Messaging.Castle
{
    public static class EndpointsDependencyExtensions
    {
        public static ComponentRegistration<T> WithEndpoints<T>(this ComponentRegistration<T> r, object endpoints)
            where T : class
        {
            var dictionary = new ReflectionBasedDictionaryAdapter(endpoints);
            return r.WithEndpoints(dictionary);
        }

        public static ComponentRegistration<T> WithEndpoints<T>(this ComponentRegistration<T> r, IDictionary endpoints)
            where T : class
        {
            var explicitEndpoints = new Dictionary<string, Endpoint>();
            var endpointNames = new Dictionary<string, string>();
            foreach (var key in endpoints.Keys)
            {
                var dependencyName = key.ToString();
                if (endpoints[key] is Endpoint)
                {
                    var endpoint = (Endpoint)endpoints[key];
                    explicitEndpoints[dependencyName] = endpoint;
                }
                if (endpoints[key] is string)
                {
                    var endpointName = (string)endpoints[key];
                    endpointNames[dependencyName] = endpointName;
                }
            }

            return r.AddDescriptor(new CustomDependencyDescriptor(explicitEndpoints)).ExtendedProperties(new { endpointNames });

        }
    }
}