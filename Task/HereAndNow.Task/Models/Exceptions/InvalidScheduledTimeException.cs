namespace HereAndNowService.Models.Exceptions;

/// <summary>
/// Exception thrown when a scheduled time value is invalid (e.g., in the past)
/// </summary>
public class InvalidScheduledTimeException : Exception
{
    /// <summary>
    /// Creates a new InvalidScheduledTimeException
    /// </summary>
    /// <param name="message">The error message describing why the time is invalid</param>
    public InvalidScheduledTimeException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Creates a new InvalidScheduledTimeException with inner exception
    /// </summary>
    public InvalidScheduledTimeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
