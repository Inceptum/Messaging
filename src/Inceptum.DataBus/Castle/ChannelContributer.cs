using System;
using System.Linq;
using System.Reflection;
using Castle.Core;
using Castle.MicroKernel;
using Castle.MicroKernel.ModelBuilder;


namespace Inceptum.DataBus.Castle
{
    public class ChannelContributer : IContributeComponentModelConstruction
    {
        private readonly DataBus m_Bus;

        public ChannelContributer(DataBus bus)
        {
            m_Bus = bus;
        }

        public void ProcessModel(IKernel kernel, ComponentModel model)
        {
			foreach (var feedProviderType in model.Services.Where(s => s.IsImplementationOf(typeof(IFeedProvider<,>))))
			{
				model.ExtendedProperties["IsChannel"] = true;
				setAllChannelDependencyNotOptional(model);

				var attr = Helper.GetAttribute<ChannelAttribute>(model.Implementation);
				var channelName = attr == null || string.IsNullOrEmpty(attr.Name)
				                     	? feedProviderType.GetGenericArguments()[0].FullName
				                     	: attr.Name;
				model.ExtendedProperties["ChannelName"] = channelName;

				createProxyChannel(feedProviderType, model, kernel, channelName);
			}
        }

        private void createProxyChannel(Type feedProviderType,ComponentModel model, IKernel kernel, string channelName)
        {
            var proxyType = typeof(FeedProviderProxy<,>).MakeGenericType(feedProviderType.GetGenericArguments());
            var proxy = Activator.CreateInstance(proxyType, kernel, model.Name) as IFeedProviderProxy;

            proxy.Register(m_Bus, channelName);
        }

        private static void setAllChannelDependencyNotOptional(ComponentModel model)
        {
            PropertyInfo[] props = model.Implementation.GetProperties(
                BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo prop in props)
            {
                if (prop.PropertyType.IsGenericType && prop.PropertyType.GetGenericTypeDefinition() == typeof (IChannel<>))
                {
                    PropertySet propSet = model.Properties.FindByPropertyInfo(prop);
                    if (propSet == null)
                        continue;
                    propSet.Dependency.IsOptional = false;
                    var attr = propSet.Property.GetCustomAttributes(typeof (ImportChannelAttribute), true).FirstOrDefault() as ImportChannelAttribute;
                    //TODO: not sure its is coorrect to remove this code, but in castle 3.0 DependencyType is not available
                    //propSet.Dependency.DependencyType = DependencyType.ServiceOverride;
                    if (attr == null || string.IsNullOrEmpty(attr.Name))
                        propSet.Dependency.DependencyKey = propSet.Property.Name;
                    else
                        propSet.Dependency.DependencyKey = attr.Name;
                }
            }
        }
    }
}