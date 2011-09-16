using System;
using System.IO;
using System.Linq;
using Inceptum.AppHosting.AppDiscovery;
using NUnit.Framework;

namespace Inceptum.AppHosting.Tests
{
    [TestFixture]
    public class FolderApplicationBrowserTests
    {

        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void CtorFolderDoesNotExistFailureTest()
        {
            new FolderApplicationBrowser("aaa");
        }

        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void CtorFolderIsNullFailureTest()
        {
            new FolderApplicationBrowser(null);
        }

        [Test]
        public void LoadAppsTest()
        {
            var folderApplicationBrowser = new FolderApplicationBrowser(Path.GetDirectoryName(typeof(HostTests).Assembly.Location));
            var discoveredApps = folderApplicationBrowser.GetAvailabelApps();
            Assert.That(discoveredApps, Is.Not.Null, "Apps list is null");
            Assert.That(discoveredApps.Count(), Is.GreaterThanOrEqualTo(1), "Wrong count of apps were returned");
            var hostedAppInfo = discoveredApps.Where(a => a.Name == "Test").FirstOrDefault();
            Assert.That(hostedAppInfo, Is.Not.Null, "App was not discovered or wrong name was returned");
            Assert.That(hostedAppInfo.AppType, Is.EqualTo(typeof(ApplicationHost<TestHostedApp>).FullName), "Wrong type was returned");
        }
    }
}