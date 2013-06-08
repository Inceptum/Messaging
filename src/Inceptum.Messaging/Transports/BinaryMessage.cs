using System.Collections.Generic;

namespace Inceptum.Messaging.Transports
{
    public class BinaryMessage
    {
        public BinaryMessage()
        {
            Headers=new Dictionary<string, string>();
        }

        public byte[] Bytes { get; set; }
        public string Type { get; set; }
        public Dictionary<string,string> Headers { get; private set; }
    }
}