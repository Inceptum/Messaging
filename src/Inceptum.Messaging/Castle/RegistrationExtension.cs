using System.Linq;
using Castle.MicroKernel.Registration;

namespace Inceptum.Messaging.Castle
{
    public static class RegistrationExtensions
    {
        public static ComponentRegistration<T> AsMessageHandler<T>(this ComponentRegistration<T> r, params string[] endpoints)
            where T : class
        {
            return r.ExtendedProperties(new { MessageHandlerFor = endpoints }).WithEndpoints(endpoints.ToDictionary(name => name));
        }
         
    }
}