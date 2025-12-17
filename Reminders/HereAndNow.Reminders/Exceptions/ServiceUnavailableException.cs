namespace HereAndNowService.Exceptions;

/// <summary>
/// Exception thrown when a downstream service (e.g., Cosmos DB) is unavailable.
/// Should be mapped to HTTP 503 Service Unavailable.
/// </summary>
public class ServiceUnavailableException : Exception
{
    /// <summary>
    /// The name of the service that is unavailable.
    /// </summary>
    public string ServiceName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceUnavailableException"/> class.
    /// </summary>
    /// <param name="serviceName">The name of the unavailable service.</param>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public ServiceUnavailableException(string serviceName, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ServiceName = serviceName;
    }
}
