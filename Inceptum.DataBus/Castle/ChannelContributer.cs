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
            bool isFeedProvider = Helper.IsImplementationOf(model.Service, typeof (IFeedProvider<,>));
            ChannelAttribute attr = Helper.GetAttribute<ChannelAttribute>(model.Implementation);
            var isDataBusPart = isFeedProvider && attr != null && !string.IsNullOrEmpty(attr.Name);
            model.ExtendedProperties["IsChannel"] = isDataBusPart;

            setAllChannelDependencyNotOptional(model);
            if (!isDataBusPart)
                return;
            model.ExtendedProperties["ChannelName"] = attr.Name;

            createProxyChannel(model, kernel, attr.Name);
        }

        private void createProxyChannel(ComponentModel model, IKernel kernel, string channelName)
        {
            var proxyType = typeof (FeedProviderProxy<,>).MakeGenericType(model.Service.GetGenericArguments());
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
                    propSet.Dependency.DependencyType = DependencyType.ServiceOverride;
                    if (attr == null || string.IsNullOrEmpty(attr.Name))
                        propSet.Dependency.DependencyKey = propSet.Property.Name;
                    else
                        propSet.Dependency.DependencyKey = attr.Name;
                }
            }
        }
    }
}