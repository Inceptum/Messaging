namespace Inceptum.Cqrs.Configuration
{
    interface IBoundedContextDescriptor
    {
        void Create(BoundedContext boundedContext);
    }
}