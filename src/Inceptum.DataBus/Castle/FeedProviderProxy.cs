using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Castle.Core;
using Castle.MicroKernel;
 

namespace Inceptum.DataBus.Castle
{
    internal class FeedProviderProxy<TData, TContext> : IFeedProvider<TData, TContext>, IFeedProviderProxy
    {
        private readonly IKernel _container;
        private readonly string _key;
        private IFeedProvider<TData, TContext> _feedProvider;
        private DataBus _bus;
        //TODO: replace IKernel with factory
        public FeedProviderProxy(IKernel container, string key)
        {
            _container = container;
            _key = key;
            _bus = container.Resolve<DataBus>("DataBus");
            container.ComponentDestroyed += KernelOnComponentDestroyed;
        }

        private void KernelOnComponentDestroyed(ComponentModel model, object instance)
        {
            if(instance==_feedProvider)
            {
                var channelName = model.ExtendedProperties["ChannelName"] as string;
                _bus.RemoveFeedProvider(channelName,this);
            }

        }

        private IFeedProvider<TData, TContext> FeedProvider
        {
            get
            {
                if (_feedProvider == null)
                {
                    try
                    {
                        _feedProvider = _container.Resolve<IFeedProvider<TData, TContext>>(_key);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(string.Format("FeedProvider '{0}' can not be resolved", _key), ex);
                    }
                }
                return _feedProvider;
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Register(DataBus bus, string name)
        {
            bus.RegisterFeedProvider(name, this);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool CanProvideFor(TContext context)
        {
            return FeedProvider.CanProvideFor(context);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IObservable<TData> CreateFeed(TContext context)
        {
            return FeedProvider.CreateFeed(context);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IEnumerable<TData> OnFeedLost(TContext context)
        {
            return FeedProvider.OnFeedLost(context);
        }
 
        [MethodImpl(MethodImplOptions.Synchronized)]
        public IFeedResubscriptionPolicy GetResubscriptionPolicy(TContext context)
        {
            return FeedProvider.GetResubscriptionPolicy(context);
        }
    }
}