using System.Text;
using Newtonsoft.Json;

namespace Inceptum.Messaging.Serialization.Json
{
    class JsonSerializer<TMessage> : IMessageSerializer<TMessage>
    {
        public byte[] Serialize(TMessage message)
        {
            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message));
        }

        public TMessage Deserialize(byte[] message)
        {
            return JsonConvert.DeserializeObject<TMessage>(Encoding.UTF8.GetString(message));
        }
    }
}
