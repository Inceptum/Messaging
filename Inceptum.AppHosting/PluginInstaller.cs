using System;
using System.Collections.Generic;
using System.IO;
using Castle.Facilities.Logging;
using Castle.Facilities.Startable;
using Castle.Facilities.TypedFactory;
using Castle.MicroKernel.Registration;
using Castle.MicroKernel.SubSystems.Configuration;
using Castle.Windsor;
using Castle.Windsor.Installer;
using Finam.Internet.Bank.Configuration.Client.Castle;

namespace Inceptum.AppHosting
{
    public class PluginInstaller:IWindsorInstaller
    {
        private readonly IDictionary<string, string> m_Context;

        public PluginInstaller(IDictionary<string, string> context)
        {
            m_Context = context;
        }

        public void Install(IWindsorContainer container, IConfigurationStore store)
        {
            //Basic Facilities
/*            container.AddFacility<LoggingFacility>(f => f.LogUsing(LoggerImplementation.NLog).WithConfig("nlog.config")).AddFacility<TypedFactoryFacility>();*/
            container.AddFacility<ConfigurationFacility>(f => f.Configuration("ibank")
                .ServiceUrl(m_Context["confSvcUrl"]).Params(new { environment = m_Context["environment"], appName = AppDomain.CurrentDomain.FriendlyName }));
/*            container.AddFacility<StartableFacility>();*/



            //castle config
            /*var configurationFile = string.Format("castle.{0}.config", AppDomain.CurrentDomain.FriendlyName);
            if (File.Exists(configurationFile))
            {
                container.Install(Configuration.FromXmlFile(configurationFile));
            }
*/
            //container.Register(Component.For<SonicConfiguration>().FromConfiguration("ibank.{appName}", "Sonic", "{environment}").Named("ConfigurationProvider"));
        }
    }
}