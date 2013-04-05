using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Inceptum.Core.Messaging;

namespace Inceptum.Messaging.RabbitMq
{
    public class RabbitMqMessagingEngine:IMessagingEngine
    {
        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public IDisposable SubscribeOnTransportEvents(TrasnportEventHandler handler)
        {
            throw new NotImplementedException();
        }

        public void Send<TMessage>(TMessage message, Endpoint endpoint)
        {
            throw new NotImplementedException();
        }

        public void Send<TMessage>(TMessage message, Endpoint endpoint, int ttl)
        {
            throw new NotImplementedException();
        }

        public IDisposable Subscribe<TMessage>(Endpoint endpoint, Action<TMessage> callback)
        {
            throw new NotImplementedException();
        }

        public TResponse SendRequest<TRequest, TResponse>(TRequest request, Endpoint endpoint, long timeout = TransportConstants.DEFAULT_REQUEST_TIMEOUT)
        {
            throw new NotImplementedException();
        }

        public IDisposable SendRequestAsync<TRequest, TResponse>(TRequest request, Endpoint endpoint, Action<TResponse> callback, Action<Exception> onFailure,
                                                                 long timeout = TransportConstants.DEFAULT_REQUEST_TIMEOUT)
        {
            throw new NotImplementedException();
        }

        public IDisposable RegisterHandler<TRequest, TResponse>(Func<TRequest, TResponse> handler, Endpoint endpoint) where TResponse : class
        {
            throw new NotImplementedException();
        }
    }
}
