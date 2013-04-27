namespace Inceptum.Messaging.Contract
{

    

	/// <summary>
	/// Endpoint
	/// </summary>
	public struct Endpoint
	{
		/// <summary>
		/// 
		/// </summary>
		public Endpoint(string transportId, string destination, bool sharedDestination = false )
		{
		  
		    m_TransportId = transportId;
			m_Destination = destination;
			m_SharedDestination = sharedDestination;
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

		private string m_Destination;

		/// <summary>
		/// Gets or sets the destination.
		/// </summary>
		/// <value>
		/// The destination.
		/// </value>
		public string Destination
		{
			get { return m_Destination; }
			set { m_Destination = value; }
		}

		private bool m_SharedDestination;
	   

	    /// <summary>
		/// Shared destination
		/// </summary>
		public bool SharedDestination
		{
			get { return m_SharedDestination; }
			set { m_SharedDestination = value; }
		}

	    public override string ToString()
	    {
	        return string.Format("[Transport: {0}, Destination: {1}]",TransportId,Destination);
	    }
	}
}
