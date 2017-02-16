namespace Inceptum.Messaging.Contract
{
    /// <summary>
    /// Information for response
    /// </summary>
    public struct ReplyTo
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="destination"></param>
        public ReplyTo(string destination)
            : this()
        {
            Destination = destination;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="correlationId"></param>
        public ReplyTo(string destination, string correlationId) 
            : this()
        {
            Destination = destination;
            CorrelationId = correlationId;
        }

        /// <summary>
        /// Queue name with queue://
        /// </summary>
        public string Destination { get; set; }
        
        /// <summary>
        /// ReplyTo CorrelationId
        /// </summary>
        public string CorrelationId { get; set; }
    }
}
