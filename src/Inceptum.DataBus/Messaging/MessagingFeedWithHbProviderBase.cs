using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Inceptum.Messaging.Contract;

namespace Inceptum.DataBus.Messaging
{

    /// <summary>
    /// Base class for messaging feed providers providing feeds that require reinitialization on hearbeat loss.
    /// </summary>
    /// <typeparam name="TData">The type of the data.</typeparam>
    /// <typeparam name="TMessage">The type of the message.</typeparam>
    /// <typeparam name="TContext">The type of the context.</typeparam>
    /// <typeparam name="TInitRequest">The type of the init request.</typeparam>
    /// <typeparam name="TInitResponse">The type of the init response.</typeparam>
    /// <typeparam name="THeartbeatMessage">The type of heartbeat message</typeparam>
    public abstract class MessagingFeedWithHbProviderBase<TData, TMessage, TContext, TInitRequest, TInitResponse, THeartbeatMessage>
        : MessagingFeedWithInitializationBase<TData, TMessage, TContext, TInitRequest, TInitResponse>
    {

        protected MessagingFeedWithHbProviderBase(IMessagingEngine messagingEngine, long initTimeout = 30000)
            : base(messagingEngine, initTimeout)
        {
        }

        /// <summary>
        /// Last known uptime date
        /// </summary>
        protected virtual DateTime? LastKnownUptimeDate { get; set; }

        /// <summary>
        /// Gets the tolerance, number of intervals without HB before feed is lost.
        /// </summary>
        protected virtual uint Tolerance
        {
            get { return 2; }
        }

        protected override IDisposable InitializeFeed(Subject<TData> dataFeed, TInitResponse response, TContext context)
        {

            var hbEndpoint = GetHbEndpoint(context);
            var heartBeatInterval = GetHeartbeatIntervalFromResponse(response, context);
            Logger.DebugFormat("Subscribing for heartbeats. Context: {0}; Endpoint: {1}; Hb interval {2}ms", GetContextLogRepresentationString(context), hbEndpoint, heartBeatInterval);
            var scheduledHbLoss = new SerialDisposable();

            var hbSubscription = Disposable.Empty;



            var tmp = MessagingEngine.Subscribe<THeartbeatMessage>(hbEndpoint, message => processHeartBeat(context, message, dataFeed, heartBeatInterval, scheduledHbLoss));
            hbSubscription = Disposable.Create(() =>
            {
                tmp.Dispose();
                Logger.DebugFormat("Unsubscribed from heartbeats: {0};  Endpoint: {1}", GetContextLogRepresentationString(context), hbEndpoint);
            });

            scheduledHbLoss.Disposable = scheduleHbLoss(heartBeatInterval, dataFeed, context);
            return new CompositeDisposable(hbSubscription, scheduledHbLoss);
        }

        private IDisposable scheduleHbLoss(long heartBeatInterval, Subject<TData> dataFeed, TContext context, DateTime? lastHb = null)
        {
            var hb = lastHb;

            var errorMessage = string.Format("Heartbeats for context {0} were lost", context);
            return Scheduler.Default.Schedule(
                DateTime.Now.AddMilliseconds(heartBeatInterval * Tolerance),
                (() =>
                {
                    if (hb.HasValue)
                        Logger.WarnFormat("Heartbeats lost (no hb in x{0} intervals). Context: {1}; Interval {2}ms; The last HB was at {3}ms", Tolerance, GetContextLogRepresentationString(context), heartBeatInterval, hb.Value);
                    else
                        Logger.WarnFormat("There are no heartbeats for context: {0}. Cancelling subscription...", GetContextLogRepresentationString(context));
                    dataFeed.OnError(new HeartbeatLostException(errorMessage));
                }
                ));
        }

        private void processHeartBeat(TContext context, THeartbeatMessage message, Subject<TData> dataFeed, long initialHeartBeatInterval, SerialDisposable scheduledHbLoss)
        {
            //Logger.DebugFormat(DateTime.Now.ToString()+": Heartbeat received. Context: {0}", GetContextLogRepresentationString(context));
            var interval = GetHeartbeatIntervalFromHeartbeatMessage(message, context);
            if (interval == -1) interval = initialHeartBeatInterval;

            var uptimeDate = GetUptimeDateFromHeartbeatMessage(message, context);
            if (uptimeDate != null)
            {
                if (LastKnownUptimeDate == null)
                {
                    LastKnownUptimeDate = uptimeDate;
                }
                else
                {
                    if (LastKnownUptimeDate != uptimeDate)
                    {
                        string errorMessage = String.Format("Uptime date changed: was {0:dd/MM/yyyy HH:mm:ss}, received {1:dd/MM/yyyy HH:mm:ss}. Context {2}", LastKnownUptimeDate, uptimeDate, GetContextLogRepresentationString(context));

                        LastKnownUptimeDate = uptimeDate;
                        dataFeed.OnError(new HeartbeatLostException(errorMessage));
                        return;
                    }
                }
            }


            scheduledHbLoss.Disposable = scheduleHbLoss(interval, dataFeed, context, DateTime.Now);
        }

        protected virtual Endpoint GetHbEndpoint(TContext context)
        {
            var endpoint = GetEndpoint(context);
            return new Endpoint
            {
                Destination = endpoint.Destination + ".hb",
                SharedDestination = false,
                TransportId = endpoint.TransportId,
                SerializationFormat = endpoint.SerializationFormat
            };
        }


        protected abstract long GetHeartbeatIntervalFromResponse(TInitResponse response, TContext context);
        protected virtual long GetHeartbeatIntervalFromHeartbeatMessage(THeartbeatMessage message, TContext context)
        {
            return -1;
        }

        protected virtual DateTime? GetUptimeDateFromHeartbeatMessage(THeartbeatMessage message, TContext context)
        {
            return null;
        }
    }
}