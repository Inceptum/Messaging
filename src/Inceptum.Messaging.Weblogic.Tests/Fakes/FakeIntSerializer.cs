using System.Text;

namespace Inceptum.Messaging.Weblogic.Tests.Fakes
{
    internal class FakeIntSerializer : IMessageSerializer<int>
    {
        #region IMessageSerializer<int> Members

        public byte[] Serialize(int message)
        {
            return Encoding.UTF8.GetBytes(message.ToString());
        }

        public int Deserialize(byte[] message)
        {
            return int.Parse(Encoding.UTF8.GetString(message));
        }

        #endregion
    }
}