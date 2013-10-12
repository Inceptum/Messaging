using Castle.MicroKernel.Facilities;
using Castle.MicroKernel.Registration;


namespace Inceptum.DataBus.Castle
{
    public class ChannelRegistrationFacility : AbstractFacility
    {
        private DataBus m_Bus;

        protected override void Init()
        {
            Kernel.Register(Component.For<IDataBus>().ImplementedBy<DataBus>().Named("DataBus"));
            m_Bus = Kernel.Resolve<DataBus>("DataBus");
            Kernel.ComponentModelBuilder.AddContributor(new ChannelContributer(m_Bus));
            Kernel.Resolver.AddSubResolver(new ChannelResolver(m_Bus));
        }
    }
}
