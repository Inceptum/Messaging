using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

namespace Inceptum.AppHosting.Tests
{
    internal class TestHostedApp : IHostedApplication, IDisposable
    {
        public bool IsStarted { get; set; }
        public bool IsDisposed { get; set; }

        public void Start()
        {
            IsStarted = true;
        }

        public string Name
        {
            get { return "Test App"; }
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    internal class TestExceptionOnStartHostedApp : IHostedApplication, IDisposable
    {
        /// <summary>
        /// Starts the application.
        /// </summary>
        public void Start()
        {
            throw new Exception();
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {

        }
    }

    internal class TestExceptionOnDisposeHostedApp : IHostedApplication, IDisposable
    {
        /// <summary>
        /// Starts the application.
        /// </summary>
        public void Start()
        {
            throw new Exception();
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            throw new Exception();
        }
    }

    [TestFixture]
    public class ApplicationHostTests
    {
        private readonly IDictionary<string, string> m_Context = new Dictionary<string, string> { { "environment", "TEST" }, { "confSvcUrl", "http://localhost:8080" } };
        
        [Test]
        public void CreationInAppDomainSmokeTest()
        {
            var applicationHost = ApplicationHost.Create(new HostedAppInfo("TEST", typeof (ApplicationHost<TestHostedApp>).FullName));
            applicationHost.Start( );
        }


        [Test]
        public void StartTest()
        {
            var applicationHost = new ApplicationHost<TestHostedApp>();
            applicationHost.Start( );
            //TODO[KN]: more asserts
            Assert.That(applicationHost.Container, Is.Not.Null, "Container was not created");
            Assert.That(applicationHost.Container.Kernel.GetAssignableHandlers(typeof (IHostedApplication)).Length,
                        Is.GreaterThan(0), "Application was not registered as component");
            Assert.That(applicationHost.Container.Kernel.GetAssignableHandlers(typeof (IHostedApplication)).Length,
                        Is.EqualTo(1), "More trhen 1 application component was registered");
            Assert.That(applicationHost.Container.Resolve<IHostedApplication>(), Is.InstanceOf(typeof (TestHostedApp)),
                        "Application was registered with wrong implementation");
            Assert.That((applicationHost.Container.Resolve<IHostedApplication>() as TestHostedApp).IsStarted, Is.True,
                        "Application was not started");
        }

        [Test]
        public void StopTest()
        {
            var applicationHost = new ApplicationHost<TestHostedApp>();
            applicationHost.Start( );
            var testHostedApp = (applicationHost.Container.Resolve<IHostedApplication>() as TestHostedApp);
            applicationHost.Stop();
            Assert.That(testHostedApp.IsDisposed, Is.True, "Application was not started");
        }

        [Test]
        public void StartWithExceptionOnStartThrowsApplicationException()
        {
            var applicationHost = new ApplicationHost<TestExceptionOnStartHostedApp>();

            Assert.Throws<ApplicationException>(applicationHost.Start);
        }

        [Test]
        public void StartWithExceptionOnStartAndOnDisposeThrowsApplicationException()
        {
            var applicationHost = new ApplicationHost<TestExceptionOnDisposeHostedApp>();

            Assert.Throws<ApplicationException>(applicationHost.Start);
        }

        [Test]
        public void StartTwiceFailureTest()
        {
            Exception exception = null;
            try
            {
                var applicationHost = new ApplicationHost<TestHostedApp>();
                applicationHost.Start( );
                applicationHost.Start( );
            }
            catch (TargetInvocationException ex)
            {
                exception = ex.InnerException;
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            Assert.That(exception, Is.Not.Null, "Exception was not thrown");
            Assert.That(exception, Is.InstanceOf(typeof (InvalidOperationException)));
        }

        [Test]
        public void StopWithoutStartFailureTest()
        {
            Exception exception = null;
            try
            {
                var applicationHost = new ApplicationHost<TestHostedApp>();
                applicationHost.Stop();
            }
            catch (TargetInvocationException ex)
            {
                exception = ex.InnerException;
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            Assert.That(exception, Is.Not.Null, "Exception was not thrown");
            Assert.That(exception, Is.InstanceOf(typeof (InvalidOperationException)));
        }

        [Test]
        public void StopTwiceFailureTest()
        {
            Exception exception = null;
            try
            {
                var applicationHost = new ApplicationHost<TestHostedApp>();
                applicationHost.Stop();
                applicationHost.Stop();
            }
            catch (TargetInvocationException ex)
            {
                exception = ex.InnerException;
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            Assert.That(exception, Is.Not.Null, "Exception was not thrown");
            Assert.That(exception, Is.InstanceOf(typeof (InvalidOperationException)));
        }
    }
}