using Rhino.Mocks;

namespace Inceptum.Messaging.Weblogic.Tests.Fakes
{
    public class ObjectMother
    {
        public static ITransportResolver MockTransportResolver(string transportFactory)
        {
            var resolver = MockRepository.GenerateMock<ITransportResolver>();
            resolver.Expect(r => r.GetTransport(TransportConstants.TRANSPORT_ID1)).Return(new TransportInfo(TransportConstants.BROKER, TransportConstants.USERNAME, TransportConstants.PASSWORD, "MachineName", transportFactory) { JailStrategy = JailStrategy.MachineName });
            resolver.Expect(r => r.GetTransport(TransportConstants.TRANSPORT_ID2)).Return(new TransportInfo(TransportConstants.BROKER, TransportConstants.USERNAME, TransportConstants.PASSWORD, "MachineName", transportFactory) { JailStrategy = JailStrategy.MachineName });
            return resolver;
        }        
    }
}