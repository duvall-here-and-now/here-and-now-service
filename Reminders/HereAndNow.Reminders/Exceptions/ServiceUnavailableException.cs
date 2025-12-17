namespace HereAndNowService.Exceptions;

/// <summary>
/// Exception thrown when a required external service is unavailable.
/// </summary>
public class ServiceUnavailableException : Exception
{
    /// <summary>
    /// The name of the unavailable service.
    /// </summary>
    public string ServiceName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceUnavailableException"/> class.
    /// </summary>
    /// <param name="serviceName">The name of the unavailable service.</param>
    /// <param name="message">The error message.</param>
    public ServiceUnavailableException(string serviceName, string message)
        : base(message)
    {
        ServiceName = serviceName;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceUnavailableException"/> class.
    /// </summary>
    /// <param name="serviceName">The name of the unavailable service.</param>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public ServiceUnavailableException(string serviceName, string message, Exception innerException)
        : base(message, innerException)
    {
        ServiceName = serviceName;
    }
}
