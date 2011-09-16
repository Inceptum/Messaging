using Castle.Core;
using Castle.MicroKernel;
using Castle.MicroKernel.Context;


namespace Inceptum.DataBus.Castle
{
    public class ChannelResolver : ISubDependencyResolver
    {
        private readonly DataBus _bus;

        public ChannelResolver(DataBus bus)
        {
            _bus = bus;
        }

        public bool CanResolve(CreationContext context, ISubDependencyResolver parentResolver,
                               ComponentModel model, DependencyModel dependency)
        {
            if (!Helper.IsImplementationOf(dependency.TargetType, typeof (IChannel<>)))
                return false;
            return true;
        }

        public object Resolve(CreationContext context, ISubDependencyResolver parentResolver,
                              ComponentModel model, DependencyModel dependency)
        {
            var dataType = dependency.TargetType.GetGenericArguments()[0];
            return typeof (DataBus).GetMethod("Channel").MakeGenericMethod(new[] {dataType}).Invoke(_bus, new object[] {dependency.DependencyKey});
        }
    }
}