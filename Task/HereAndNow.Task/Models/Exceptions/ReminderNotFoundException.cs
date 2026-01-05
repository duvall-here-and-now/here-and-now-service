namespace HereAndNowService.Models.Exceptions;

/// <summary>
/// Exception thrown when a requested reminder is not found
/// </summary>
public class ReminderNotFoundException : Exception
{
    /// <summary>
    /// The ID of the reminder that was not found
    /// </summary>
    public string ReminderId { get; }

    /// <summary>
    /// Creates a new ReminderNotFoundException
    /// </summary>
    /// <param name="reminderId">The ID of the reminder that was not found</param>
    public ReminderNotFoundException(string reminderId)
        : base($"Reminder with ID {reminderId} not found")
    {
        ReminderId = reminderId;
    }

    /// <summary>
    /// Creates a new ReminderNotFoundException with inner exception
    /// </summary>
    public ReminderNotFoundException(string reminderId, Exception innerException)
        : base($"Reminder with ID {reminderId} not found", innerException)
    {
        ReminderId = reminderId;
    }
}
