/*namespace Inceptum.DataBus.Messaging
{
    public struct HeartbeatInfo
    {
        public string Destination { get; set; }
        public long Interval { get; set; }

        public bool Equals(HeartbeatInfo other)
        {
            return Destination == other.Destination && Interval == other.Interval;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (obj.GetType() != typeof(HeartbeatInfo)) return false;
            return Equals((HeartbeatInfo)obj);
        }

        public override int GetHashCode()
        {
            return 0;
        }

        public static bool operator ==(HeartbeatInfo left, HeartbeatInfo right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(HeartbeatInfo left, HeartbeatInfo right)
        {
            return !left.Equals(right);
        }
    }
}*/