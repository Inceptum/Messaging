using System;

namespace Inceptum.Messaging.Contract
{

    public struct Destination
    {
        public string Publish { get; set; }
        public string Subscribe { get; set; }

        public static implicit operator Destination(string destination)
        {
            return new Destination {Publish=destination, Subscribe = destination}; 
        }

        public bool Equals(Destination other)
        {
            return string.Equals(Publish, other.Publish) && string.Equals(Subscribe, other.Subscribe);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is Destination && Equals((Destination) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Publish != null ? Publish.GetHashCode() : 0)*397) ^ (Subscribe != null ? Subscribe.GetHashCode() : 0);
            }
        }

        public static bool operator ==(Destination left, Destination right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Destination left, Destination right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            if (Subscribe == Publish)
                return "["+Subscribe+"]";
            return string.Format("[s:{0}, p:{1}]", Subscribe, Publish);
        }
    }



    /// <summary>
	/// Endpoint
	/// </summary>
	public struct Endpoint
	{
		/// <summary>
		/// 
		/// </summary>
        public Endpoint(string transportId, string destination, bool sharedDestination = false, string serializationFormat="protobuf")
		{
		    if (destination == null) throw new ArgumentNullException("destination");

		    m_TransportId = transportId;
			m_Destination = destination;
			m_SharedDestination = sharedDestination;
		    m_SerializationFormat = serializationFormat;
		}
        
        /// <summary>
		/// 
		/// </summary>
        public Endpoint(string transportId, string publish, string subscribe, bool sharedDestination = false, string serializationFormat="protobuf")
		{
		  
		    m_TransportId = transportId;
			m_Destination = new Destination {Publish=publish, Subscribe = subscribe};
			m_SharedDestination = sharedDestination;
		    m_SerializationFormat = serializationFormat;
		}

		private string m_TransportId;

		/// <summary>
		/// Gets or sets the transport id.
		/// </summary>
		/// <value>
		/// The transport id.
		/// </value>
		public string TransportId
		{
			get { return m_TransportId; }
			set { m_TransportId = value; }
		}

        private Destination m_Destination;

		/// <summary>
		/// Gets or sets the destination.
		/// </summary>
		/// <value>
		/// The destination.
		/// </value>
        public Destination Destination
		{
			get { return m_Destination; }
			set { m_Destination = value; }
		}

		private bool m_SharedDestination;
	    private string m_SerializationFormat;


	    /// <summary>
		/// Shared destination
		/// </summary>
		public bool SharedDestination
		{
			get { return m_SharedDestination; }
			set { m_SharedDestination = value; }
		}


	    /// <summary>
		/// Shared destination
		/// </summary>
		public string SerializationFormat
		{
            get { return m_SerializationFormat ?? "protobuf"; }
            set { m_SerializationFormat = value??"protobuf"; }
		}

	    public override string ToString()
	    {
	        return string.Format("[Transport: {0}, Destination: {1}]",TransportId,Destination);
	    }
	}
}
