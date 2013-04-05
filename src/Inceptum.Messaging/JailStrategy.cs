using System;

namespace Inceptum.Messaging
{
    public class JailStrategy
    {
        public static JailStrategy None = new JailStrategy(() => null);
        public static JailStrategy MachineName = new JailStrategy(() => Environment.MachineName);
        public static JailStrategy Guid = new JailStrategy(() => System.Guid.NewGuid().ToString());

        private readonly Func<string> m_CreateTag;

        private JailStrategy(Func<string> createTag)
        {
            m_CreateTag = createTag;
        }

        //TODO: changed internal to public while extracting sonic dependent code to dedicated assembly. Need to review.
        public Func<string> CreateTag
        {
            get { return m_CreateTag; }
        }

        public static JailStrategy Custom(Func<string> createTag)
        {
            return new JailStrategy(createTag);
        }
    }
}
