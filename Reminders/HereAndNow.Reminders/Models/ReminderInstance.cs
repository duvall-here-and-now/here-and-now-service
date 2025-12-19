namespace HereAndNowService.Models;

/// <summary>
/// Represents a reminder instance with scheduling and completion tracking.
/// This model maps directly to the Cosmos DB storage schema.
/// </summary>
public class ReminderInstance
{
    /// <summary>
    /// Unique identifier for the reminder instance.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The user identifier who owns this reminder. Used as the Cosmos DB partition key.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// The text content of the reminder.
    /// </summary>
    public required string Text { get; set; }

    /// <summary>
    /// The date and time when the reminder is scheduled to occur.
    /// </summary>
    public DateTime ScheduledDateAndTime { get; set; }

    /// <summary>
    /// Indicates whether the reminder has been completed.
    /// </summary>
    public bool IsCompleted { get; set; }

    /// <summary>
    /// Indicates whether the reminder has been soft-deleted.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Indicates whether the reminder should play a sound when triggered.
    /// </summary>
    public bool ShouldPlaySound { get; set; }

    /// <summary>
    /// Indicates whether the reminder should vibrate the device when triggered.
    /// </summary>
    public bool ShouldDoVibration { get; set; }

    /// <summary>
    /// The date and time when the reminder was created.
    /// </summary>
    public DateTime CreatedDateAndTime { get; set; }

    /// <summary>
    /// The date and time when the reminder was completed. Null if not completed.
    /// </summary>
    public DateTime? CompletedDateAndTime { get; set; }

    /// <summary>
    /// The date and time when the reminder was deleted. Null if not deleted.
    /// </summary>
    public DateTime? DeletedDateAndTime { get; set; }
}
