using System;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using Inceptum.DataBus;
using Inceptum.DataBus.Castle;
using NUnit.Framework;

namespace Inceptum.DataBus.Tests
{
    [TestFixture]
    public class ChannelRegistrationFacilityTests
    {
        [Test]
        public void ChannelRegistrationTest()
        {
            IWindsorContainer container = new WindsorContainer();
            container.AddFacility<ChannelRegistrationFacility>();
            container.Register(AllTypes.FromAssembly(GetType().Assembly).BasedOn(typeof (IFeedProvider<,>)).WithService.FromInterface());
            var bus = container.Resolve<IDataBus>();
            Assert.IsNotNull(bus, "Facility have not registered DataBus as component");
            IObservable<int> myChannelFeed = bus.Channel<int>("Channel1").Feed(10);
            Assert.IsNotNull(myChannelFeed, "Channel was not registered");
        }


        [Test]
        public void ChannelRegistration_NameFromAttribute_Test()
        {
            IWindsorContainer container = new WindsorContainer();
            container.AddFacility<ChannelRegistrationFacility>();
            container.Register(AllTypes.FromAssembly(GetType().Assembly).BasedOn(typeof (IFeedProvider<,>)).WithService.FromInterface());
            var bus = container.Resolve<IDataBus>();
            Assert.IsNotNull(bus, "Facility have not registered DataBus as component");
            IObservable<int> myChannelFeed = bus.Channel<int>("Channel_With_Name").Feed(10);
            Assert.IsNotNull(myChannelFeed, "Channel was not registered");
        }


        [Test]
        public void ChannelDependencyResolvingTest()
        {
            IWindsorContainer container = new WindsorContainer();
            container.AddFacility<ChannelRegistrationFacility>();
            container.Register(AllTypes.FromAssembly(GetType().Assembly).BasedOn(typeof (IFeedProvider<,>)).WithService.FromInterface());
            var bus = container.Resolve<IDataBus>();
            Assert.IsNotNull(bus, "Facility have not registered DataBus as component");
            IObservable<int> myOtherChannelFeed = bus.Channel<int>("ChannelWithDependency").Feed("10");
            Assert.IsNotNull(myOtherChannelFeed, "Channel was not registered");
        }

        [Test]
        public void ChannelDependencyResolving_NameFromAttribute_Test()
        {
            IWindsorContainer container = new WindsorContainer();
            container.AddFacility<ChannelRegistrationFacility>();
            container.Register(AllTypes.FromAssembly(GetType().Assembly).BasedOn(typeof (IFeedProvider<,>)).WithService.FromInterface());
            var bus = container.Resolve<IDataBus>();
            Assert.IsNotNull(bus, "Facility have not registered DataBus as component");
            IObservable<int> myOtherChannelFeed = bus.Channel<int>("FeedWithExplicitlyNamedDependencyChannel").Feed("10");
            Assert.IsNotNull(myOtherChannelFeed, "Channel was not registered");
        }

		[Test]
		public void ChannelDependencyResolving_WithoutName_Test()
		{
			IWindsorContainer container = new WindsorContainer();
			container.AddFacility<ChannelRegistrationFacility>();
			container.Register(AllTypes.FromAssembly(GetType().Assembly).BasedOn(typeof(IFeedProvider<,>)).WithService.FromInterface());
			var bus = container.Resolve<IDataBus>();
			Assert.IsNotNull(bus, "Facility have not registered DataBus as component");
			IObservable<int> myOtherChannelFeed = bus.Channel<int>().Feed(5);
			Assert.IsNotNull(myOtherChannelFeed, "Channel was not registered");
		}

		[Test]
		public void ChannelDependencyResolving_WithoutAttr_Test()
		{
			IWindsorContainer container = new WindsorContainer();
			container.AddFacility<ChannelRegistrationFacility>();
			container.Register(AllTypes.FromAssembly(GetType().Assembly).BasedOn(typeof(IFeedProvider<,>)).WithService.FromInterface());
			var bus = container.Resolve<IDataBus>();
			Assert.IsNotNull(bus, "Facility have not registered DataBus as component");
			IObservable<long> myOtherChannelFeed = bus.Channel<long>().Feed(5L);
			Assert.IsNotNull(myOtherChannelFeed, "Channel was not registered");
		}

		[Test]
		public void ChannelDependencyResolving_CustomFeedProviderWithoutAttr_Test()
		{
			IWindsorContainer container = new WindsorContainer();
			container.AddFacility<ChannelRegistrationFacility>();
			container.Register(AllTypes.FromAssembly(GetType().Assembly).BasedOn(typeof(IFeedProvider<,>)).WithService.FromInterface());
			var bus = container.Resolve<IDataBus>();
			Assert.IsNotNull(bus, "Facility have not registered DataBus as component");
			IObservable<DateTime> myOtherChannelFeed = bus.Channel<DateTime>().Feed(5);
			Assert.IsNotNull(myOtherChannelFeed, "Channel was not registered");
		}


        [Test]
        [ExpectedException(typeof (InvalidOperationException)/*, ExpectedMessage = "FeedProvider 'Inceptum.DataBus.Tests.FeedProviderWithNotResolvableDependency' can not be resolved"*/)]
        public void ChannelDependencyResolvingFailureTest()
        {
            IWindsorContainer container = new WindsorContainer();
            container.AddFacility<ChannelRegistrationFacility>();
            container.Register(Component.For<IFeedProvider<int, string>>().ImplementedBy<FeedProviderWithNotResolvableDependency>());
            var bus = container.Resolve<IDataBus>();
            Assert.IsNotNull(bus, "Facility have not registered DataBus as component");
            bus.Channel<int>("ChannelHavingFeedWithNotResolvableDependency").Feed("10");
        }
    }
}
