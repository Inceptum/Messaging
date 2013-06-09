namespace Inceptum.Cqrs.Configuration
{
    interface IBoundContextDescriptor
    {
        void Create(BoundContext boundContext);
    }
}