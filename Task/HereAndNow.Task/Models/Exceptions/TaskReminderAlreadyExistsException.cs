namespace HereAndNowService.Models.Exceptions;

/// <summary>
/// Exception thrown when attempting to create a task reminder with an ID that already exists.
/// This typically occurs when a client retries a CreateTaskAndTaskReminder command and the previous
/// attempt succeeded, or when there's an ID collision.
/// </summary>
public class TaskReminderAlreadyExistsException : Exception
{
    /// <summary>
    /// The ID of the reminder that already exists
    /// </summary>
    public string ReminderId { get; }

    /// <summary>
    /// Creates a new TaskReminderAlreadyExistsException
    /// </summary>
    /// <param name="reminderId">The ID of the reminder that already exists</param>
    public TaskReminderAlreadyExistsException(string reminderId)
        : base($"TaskReminder with ID {reminderId} already exists")
    {
        ReminderId = reminderId;
    }

    /// <summary>
    /// Creates a new TaskReminderAlreadyExistsException with inner exception
    /// </summary>
    public TaskReminderAlreadyExistsException(string reminderId, Exception innerException)
        : base($"TaskReminder with ID {reminderId} already exists", innerException)
    {
        ReminderId = reminderId;
    }
}
