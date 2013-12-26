using System;
using System.Collections.Generic;
using System.Linq;

namespace Inceptum.Messaging.Weblogic.Tests
{
    internal class TransportConstants
    {
        public const string QUEUE1 = "queue://./MoneyMailJMSModule!sminkov";
        public const string QUEUE2 = "queue://./MoneyMailJMSModule!sminkov";
        public const string TOPIC = "topic://./MoneyMailJMSModule!sminkovtopic";
        public const string TRANSPORT_ID1 = "tr1";
        public const string TRANSPORT_ID2 = "tr2";
        public const string USERNAME = "weblogic";
        public const string PASSWORD = "12345678";
        public const string BROKER = "TST-3CARDWSL1.office.finam.ru:7001";
    }
}
