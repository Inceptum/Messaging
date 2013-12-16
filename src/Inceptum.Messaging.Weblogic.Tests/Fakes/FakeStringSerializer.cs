using System.Text;

namespace Inceptum.Messaging.Weblogic.Tests.Fakes
{
    internal class FakeStringSerializer : IMessageSerializer<string>
    {
        #region IMessageSerializer<string> Members

        public byte[] Serialize(string message)
        {
            return Encoding.UTF8.GetBytes(message);
        }

        public string Deserialize(byte[] message)
        {
            return Encoding.UTF8.GetString(message);
        }

        #endregion
    }
}