using System;

namespace Inceptum.Messaging.Contract
{
    public class TransportOutdateEventArgs : EventArgs
    {
        public string Message { get; private set; }
        public string TransportName { get; private set; }
//        internal TransportResolutionTag TransportTag { get; private set; }


        internal TransportOutdateEventArgs(string message, string transportName)
        {
            if (transportName == null) throw new ArgumentNullException("transportName");
            TransportName = transportName;
            Message = message??"";
        }

/*
        internal TransportOutdateEventArgs(string message, string transportName, TransportResolutionTag transportTag)
        {
            Message = message ?? "";
            TransportName = transportName;
            TransportTag = transportTag;
        }
*/
    }
}