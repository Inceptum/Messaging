// ReSharper disable CheckNamespace
namespace Castle.MicroKernel.Registration
// ReSharper restore CheckNamespace
{
    public static class ComponentRegistrationExtensions
    {
        public static ComponentRegistration<T> AsEventsListener<T>(this ComponentRegistration<T> registration) where T : class
        {
            return registration.ExtendedProperties(new { IsEventsListener=true });
        }  
        
        public static ComponentRegistration<T> AsCommandsHandler<T>(this ComponentRegistration<T> registration, string localBoundedContext) where T : class
        {
            return registration.ExtendedProperties(new { CommandsHandlerFor = localBoundedContext });
        }
    }
}