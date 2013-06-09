namespace Inceptum.Cqrs.Configuration
{
    class NameDescriptor : IBoundContextDescriptor
    {
        private string m_Name;

        public NameDescriptor(string name)
        {
            m_Name = name;
        }

        public void Create(BoundContext boundContext)
        {
            boundContext.Name = m_Name;
        }
    }
}