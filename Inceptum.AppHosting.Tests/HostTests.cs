using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Castle.Core.Logging;
using NUnit.Framework;
using Rhino.Mocks;
using mscoree;

namespace Inceptum.AppHosting.Tests
{
    [TestFixture]
    public class HostTests
    {
            



        [Test]
        public void StartStopTest()
        {
            var tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var directory = Directory.CreateDirectory(tempPath);
            try
            {
                var count = GetAppDomains().Count;

                var host = new Host(directory.FullName);
                var source = typeof(HostTests).Assembly.Location;
                var dest = Path.Combine(directory.FullName, Path.GetFileName(source));
                Console.WriteLine(string.Format("Copy from {0} to {1}", source, dest));
                
                File.Copy(source, dest);

                //host.LoadApps(Path.GetDirectoryName(typeof(HostTests).Assembly.Location));
                host.LoadApps();
                host.StartApps();
                Assert.That(GetAppDomains().Count, Is.GreaterThanOrEqualTo(count + 1), "AppDomain was not created");
                host.Dispose();
            }
            finally
            {
                if (directory.Exists)
                    directory.Delete(true);
            }

        }

        public static IList<AppDomain> GetAppDomains()
        {
            IList<AppDomain> list = new List<AppDomain>();
            IntPtr enumHandle = IntPtr.Zero;
            var host = new CorRuntimeHost();
            try
            {
                host.EnumDomains(out enumHandle);
                while (true)
                {
                    object domain;
                    host.NextDomain(enumHandle, out domain);
                    if (domain == null) break;
                    var appDomain = (AppDomain) domain;
                    list.Add(appDomain);
                }
                return list;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return null;
            }
            finally
            {
                host.CloseEnum(enumHandle);
                Marshal.ReleaseComObject(host);
            }
        }

        [Test]
        public void StartStopOrderTest()
        {
            var host = MockRepository.GeneratePartialMock<Host>(".",NullLogger.Instance);
            IList<int> startOrder = new List<int>();
            IList<int> stopOrder = new List<int>();
            var applicationHost1 = MockRepository.GenerateMock<AppHosting.IApplicationHost>();
            var applicationHost2 = MockRepository.GenerateMock<AppHosting.IApplicationHost>();
            var appInfo1 = new HostedAppInfo("Test1", typeof (TestHostedApp).AssemblyQualifiedName);
            var appInfo2 = new HostedAppInfo("Test2", typeof (TestHostedApp).AssemblyQualifiedName);
            host.Expect(h => h.CreateApplicationHost(appInfo1)).Return(applicationHost1);
            host.Expect(h => h.CreateApplicationHost(appInfo2)).Return(applicationHost2);
            host.Expect(h => h.DiscoveredApps).Return(new[] {appInfo1, appInfo2});
            applicationHost1.Expect(a => a.Start( )).Do(new Action(()=> startOrder.Add(1)));
            applicationHost2.Expect(a => a.Start( )).Do(new Action(()=> startOrder.Add(2)));
            applicationHost1.Expect(a => a.Stop()).Do(new Action(() => stopOrder.Add(1)));
            applicationHost2.Expect(a => a.Stop()).Do(new Action(() => stopOrder.Add(2)));

            host.StartApps();
            host.Dispose();
            applicationHost1.AssertWasCalled(a => a.Start( ));
            applicationHost2.AssertWasCalled(a => a.Start( ));
            applicationHost1.AssertWasCalled(a => a.Stop());
            applicationHost2.AssertWasCalled(a => a.Stop());

            Assert.That(stopOrder, Is.EquivalentTo(stopOrder.Reverse()),
                        "Applications were not stopped in reverse order.");
            host.VerifyAllExpectations();
        }


        [Test]
        public void ExceptionHandlingTest()
        {
            var loggedErrors = new List<Tuple<string, Exception>>();
            var logger = MockRepository.GenerateMock<ILogger>();
            ManualResetEvent exceptionWaiter=new ManualResetEvent(false);
            logger.Expect(l => l.Error("", new Exception())).IgnoreArguments()
                .Callback<string, Exception>((s, e) =>
                                                 {
                                                     
                                                     loggedErrors.Add(Tuple.Create(s, e));
                                                     exceptionWaiter.Set();
                                                     return true;
                                                 });
            var host = new Host(AppDomain.CurrentDomain.BaseDirectory, logger);
            host.StartApps();
            ThreadPool.QueueUserWorkItem(state => { throw new Exception("test"); });
            Assert.That(exceptionWaiter.WaitOne(1000),Is.True,"Exception was not fired");
            Assert.That(loggedErrors.Any(x => x.Item2.Message == "test"), "Exception was not logged");
        }

 
    }
}
