using HereAndNowService.Models;

namespace HereAndNowService.Persistence;

/// <summary>
/// Cosmos DB document representation of a reminder instance.
/// This internal class handles Cosmos-specific serialization concerns.
/// </summary>
internal class ReminderDocument
{
    /// <summary>
    /// Document identifier (required by Cosmos DB). Maps to ReminderInstance.Id.ToString().
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// The user identifier. Used as the partition key (/userId).
    /// </summary>
    public required string UserId { get; set; }

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

    /// <summary>
    /// Converts a domain model to a Cosmos document.
    /// </summary>
    public static ReminderDocument FromDomain(ReminderInstance domain)
    {
        return new ReminderDocument
        {
            Id = domain.Id.ToString(),
            UserId = domain.UserId ?? throw new ArgumentException("UserId is required", nameof(domain)),
            Text = domain.Text,
            ScheduledDateAndTime = domain.ScheduledDateAndTime,
            IsCompleted = domain.IsCompleted,
            IsDeleted = domain.IsDeleted,
            ShouldPlaySound = domain.ShouldPlaySound,
            ShouldDoVibration = domain.ShouldDoVibration,
            CreatedDateAndTime = domain.CreatedDateAndTime,
            CompletedDateAndTime = domain.CompletedDateAndTime,
            DeletedDateAndTime = domain.DeletedDateAndTime
        };
    }

    /// <summary>
    /// Converts this Cosmos document to a domain model.
    /// </summary>
    public ReminderInstance ToDomain()
    {
        return new ReminderInstance
        {
            Id = Guid.Parse(Id),
            UserId = UserId,
            Text = Text,
            ScheduledDateAndTime = ScheduledDateAndTime,
            IsCompleted = IsCompleted,
            IsDeleted = IsDeleted,
            ShouldPlaySound = ShouldPlaySound,
            ShouldDoVibration = ShouldDoVibration,
            CreatedDateAndTime = CreatedDateAndTime,
            CompletedDateAndTime = CompletedDateAndTime,
            DeletedDateAndTime = DeletedDateAndTime
        };
    }
}
