using System;

namespace Inceptum.DataBus
{
    /// <summary>
    /// Indicates that a class is a channel and sets the name to be used to locate it in DataBus. This class cannot be inherited.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class ChannelAttribute : Attribute
    {
        private readonly string _name;

        public ChannelAttribute(string name)
        {
            _name = name;
        }

        public ChannelAttribute()
        {
        }

        public string Name
        {
            get { return _name; }
        }
    }
}
