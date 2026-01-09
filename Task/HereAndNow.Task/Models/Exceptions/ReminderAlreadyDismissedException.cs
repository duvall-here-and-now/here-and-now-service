namespace HereAndNowService.Models.Exceptions;

/// <summary>
/// Exception thrown when attempting to snooze a reminder that has already been dismissed
/// </summary>
public class ReminderAlreadyDismissedException : Exception
{
    /// <summary>
    /// The ID of the reminder that has been dismissed
    /// </summary>
    public string ReminderId { get; }

    /// <summary>
    /// Creates a new ReminderAlreadyDismissedException
    /// </summary>
    /// <param name="reminderId">The ID of the reminder that has been dismissed</param>
    public ReminderAlreadyDismissedException(string reminderId)
        : base($"Reminder with ID {reminderId} has already been dismissed")
    {
        ReminderId = reminderId;
    }

    /// <summary>
    /// Creates a new ReminderAlreadyDismissedException with inner exception
    /// </summary>
    public ReminderAlreadyDismissedException(string reminderId, Exception innerException)
        : base($"Reminder with ID {reminderId} has already been dismissed", innerException)
    {
        ReminderId = reminderId;
    }
}
