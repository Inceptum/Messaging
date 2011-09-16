using System;

namespace Inceptum.DataBus
{
    public class DataBusUnhandledExceptionEventArgs : EventArgs
    {
        public string ChannelName { get; private set; }
        public Exception Exception { get; private set; }

        public DataBusUnhandledExceptionEventArgs(string channelName, Exception exception)
        {
            ChannelName = channelName;
            Exception = exception;
        }
    }
}