using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Castle.Core.Logging;
using Castle.Facilities.Logging;
using Castle.Facilities.Startable;
using Castle.Facilities.TypedFactory;
using Castle.MicroKernel.Registration;
using Castle.MicroKernel.Resolvers.SpecializedResolvers;
using Castle.Windsor;
using Castle.Windsor.Installer;
using Finam.Internet.Bank.Configuration.Client.Castle;
using Inceptum.Core.Utils;
using Inceptum.Core;

namespace Inceptum.AppHosting
{
    internal class ApplicationHost
    {
        public static IApplicationHost Create(HostedAppInfo appInfo)
        {
            var domain = AppDomain.CreateDomain(appInfo.Name, null, new AppDomainSetup
                                                                           {
                                                                               ApplicationBase = appInfo.BaseDirectory,
                                                                               //TODO: use plugin app.config
                                                                               ConfigurationFile = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile// appInfo.ConfigFile
                                                                           });
            var instanceAndUnwrap = domain.CreateInstanceAndUnwrap(typeof(ApplicationHost).Assembly.FullName, appInfo.AppType);
            var host = (IApplicationHost)instanceAndUnwrap;
            return host;   
        }
    }

    internal class ApplicationHost<TApp> : MarshalByRefObject, IApplicationHost where TApp : IHostedApplication
    {
        private WindsorContainer m_Container ;

        internal WindsorContainer Container
        {
            get { return m_Container; }
        }


        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Start()
        {
            if (m_Container != null)
                throw new InvalidOperationException("Host is already started");
            try
            {
                Environment.CurrentDirectory = Path.GetDirectoryName(GetType().Assembly.Location);
                AppDomainRenderer.Register();
                m_Container = new WindsorContainer();
                //castle config
                var configurationFile = string.Format("castle.{0}.config", AppDomain.CurrentDomain.FriendlyName);
                if (File.Exists(configurationFile))
                {
                    m_Container.Install(Configuration.FromXmlFile(configurationFile));
                }


                m_Container.Install(FromAssembly.Containing<TApp>(new PluginInstallerFactory()))
                           .Register(Component.For<IHostedApplication>().ImplementedBy<TApp>())
                           .Resolve<IHostedApplication>().Start();
            }
            catch (Exception e)
            {
                try
                {
                    if (m_Container != null)
                    {
                        m_Container.Dispose();
                        m_Container = null;
                    }
                }catch (Exception e1)
                {
                    throw new ApplicationException(string.Format("Failed to start: {0}{1}Exception while disposing container: {2}", e,Environment.NewLine,e1));
                }
                throw new ApplicationException(string.Format("Failed to start: {0}", e.ToString()));
            }
        }

        /// <summary>
        /// Obtains a lifetime service object to control the lifetime policy for this instance.
        /// </summary>
        /// <returns>
        /// An object of type <see cref="T:System.Runtime.Remoting.Lifetime.ILease"/> used to control the lifetime policy for this instance. This is the current lifetime service object for this instance if one exists; otherwise, a new lifetime service object initialized to the value of the <see cref="P:System.Runtime.Remoting.Lifetime.LifetimeServices.LeaseManagerPollTime"/> property.
        /// </returns>
        /// <exception cref="T:System.Security.SecurityException">The immediate caller does not have infrastructure permission. </exception><filterpriority>2</filterpriority><PermissionSet><IPermission class="System.Security.Permissions.SecurityPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Flags="RemotingConfiguration, Infrastructure"/></PermissionSet>
        public override object InitializeLifetimeService()
        {

            // prevents proxy from expiration
            return null;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Stop()
        {
            if (m_Container == null)
                throw new InvalidOperationException("Host is not started");
            m_Container.Dispose();
            m_Container = null;
        }
    }



}
