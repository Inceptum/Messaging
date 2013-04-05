using System;
using Sonic.Jms;

namespace Inceptum.Messaging.Sonic
{
    static class SonicDestinationExt
    {
        public static Destination CreateTempDestination(this Session session)
        {
            if(session is QueueSession)
                return session.createTemporaryQueue();
         /*   if(session is TopicSession)*/
                return session.createTemporaryTopic();
            throw new InvalidOperationException("Unknown swssion type");
        }
        
        public static void Delete(this Destination dest)
        {
            var temporaryQueue = dest as TemporaryQueue;
            if (temporaryQueue != null)
            {
                temporaryQueue.delete();
                return;
            }
            var temporaryTopic = dest as TemporaryTopic;
            if (temporaryTopic != null)
            {
                temporaryTopic.delete();
                return;
            }
            throw new InvalidOperationException("Destination is not temporary");
        }
    }
}
