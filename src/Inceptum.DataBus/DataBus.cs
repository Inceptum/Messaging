using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using Castle.Core.Logging;
using Inceptum.DataBus.Castle;

namespace Inceptum.DataBus
{
    public class DataBus : IDataBus, IDisposable
    {
        private readonly CompositeDisposable m_ChannelErrorsSubscribtion = new CompositeDisposable();
        private readonly Dictionary<string, IDisposable> m_Channels = new Dictionary<string, IDisposable>();
        private readonly long m_DefaultRetryPeriod;
        private ILogger m_Logger = NullLogger.Instance;

        public DataBus(long defaultRetryPeriod)
        {
            m_DefaultRetryPeriod = defaultRetryPeriod;
        }

        public DataBus() : this(120000)
        {
        }

        public ILogger Logger
        {
            get { return m_Logger; }
            set { m_Logger = value; }
        }

        #region IDataBus Members

        public IChannel<TData> Channel<TData>(string channelName = null)
        {
            return new ChannelDescriptor<TData>(this, channelName);
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            m_ChannelErrorsSubscribtion.Dispose();
            foreach (var channel in m_Channels)
            {
                channel.Value.Dispose();
            }
        }

        #endregion

        public event EventHandler<DataBusUnhandledExceptionEventArgs> UnhandledException;

        public void RemoveFeedProvider<TData, TContext>(string channelName, IFeedProvider<TData, TContext> feedProvider)
        {
            Channel<TData, TContext> channel;
            IDisposable obj;
            if (m_Channels.TryGetValue(channelName, out obj))
            {
                channel = obj as Channel<TData, TContext>;
                if (channel != null)
                {
                    channel.RemoveFeedProvider(feedProvider);
                }
            }
        }

        public void RegisterFeedProvider<TData, TContext>(string channelName,
                                                          IFeedProvider<TData, TContext> feedProvider)
        {
            if (string.IsNullOrEmpty(channelName))
                throw new ArgumentException("'channelName' should be not empty string", "channelName");
			if (feedProvider == null)
                throw new ArgumentNullException("feedProvider");
            IDisposable obj;
            Channel<TData, TContext> channel;
            if (!m_Channels.TryGetValue(channelName, out obj))
            {
                channel = new Channel<TData, TContext>(channelName, m_DefaultRetryPeriod, Logger);
                m_ChannelErrorsSubscribtion.Add(channel.Errors.Subscribe(ex => onUnhandledException(channelName, ex)));
                m_Channels[channelName] = channel;
            }
            else
            {
                channel = obj as Channel<TData, TContext>;
            }

            if (channel == null)
            {
                throw new DataBusException(
                    string.Format(
                        "Can not register feed provider resolving feeds of '{0}' within context of '{1}' for channel '{2}'",
                        typeof (TData).Name,
                        typeof (TContext).Name, obj));
            }
            channel.AddFeedProvider(feedProvider);
        }


        public void RegisterFeedProvider<TData, TContext>(string channelName,
                                                          Func<TContext, IObservable<TData>> feedResolver,
                                                          Func<TContext, bool> canProvideFor)
        {
            if (feedResolver == null)
                throw new ArgumentNullException("feedResolver");
            RegisterFeedProvider(channelName, new AnonymousFeedProvider<TData, TContext>(feedResolver, canProvideFor));
        }


        public void RegisterFeedProvider<TData, TContext>(string channelName,
                                                          Func<TContext, IObservable<TData>> feedResolver)
        {
            if (feedResolver == null)
                throw new ArgumentNullException("feedResolver");
            RegisterFeedProvider(channelName, new AnonymousFeedProvider<TData, TContext>(feedResolver, null));
        }


        internal IFeed<TData> Feed<TData, TContext>(string channelName, TContext c)
        {
            Channel<TData, TContext> channel = GetChannel<TData, TContext>(channelName);
            IFeed<TData> feed = channel.Feed(c);
            if (feed == null)
                throw new InvalidOperationException(
                    string.Format("The channel of {0} named '{1}' has no feed within context of '{2}'", typeof (TData),
                                  channelName, c));

            return feed;
        }

        internal IFeed<TData> Feed<TData>(string channelName)
        {
            return Feed<TData, EmptyContext>(channelName, EmptyContext.Value);
        }


        private void onUnhandledException(string channelName, Exception ex)
        {
            EventHandler<DataBusUnhandledExceptionEventArgs> handler = UnhandledException;
            if (handler != null)
            {
                var args = new DataBusUnhandledExceptionEventArgs(channelName, ex);
                handler(this, args);
            }
            else
            {
                throw new DataBusException("Unhandled exception in feed provider", ex);
            }
        }


        internal Channel<TData, TContext> GetChannel<TData, TContext>(string channelName)
        {
            IDisposable channel;
            if (!m_Channels.TryGetValue(channelName, out channel) || !(channel is Channel<TData, TContext>))
                throw new InvalidOperationException(
                    string.Format("There is no channel of {0} named '{1}' with context type '{2}'", typeof (TData),
                                  channelName, typeof (TContext)));
            return channel as Channel<TData, TContext>;
        }
    }
}