using HereAndNowService.Models;

namespace HereAndNowService.Persistence;

/// <summary>
/// Maps between domain ReminderInstance and Cosmos ReminderDocument.
/// </summary>
internal static class ReminderDocumentMapper
{
    /// <summary>
    /// Converts a domain model to a Cosmos document.
    /// </summary>
    public static ReminderDocument ToDocument(ReminderInstance domain)
    {
        return new ReminderDocument
        {
            Id = domain.Id.ToString(),
            UserId = domain.UserId,
            Text = domain.Text,
            ScheduledDateAndTime = domain.ScheduledDateAndTime,
            IsCompleted = domain.IsCompleted,
            IsDeleted = domain.IsDeleted,
            ShouldPlaySound = domain.ShouldPlaySound,
            ShouldDoVibration = domain.ShouldDoVibration
        };
    }

    /// <summary>
    /// Converts a Cosmos document to a domain model.
    /// </summary>
    public static ReminderInstance ToDomain(ReminderDocument document)
    {
        return new ReminderInstance
        {
            Id = Guid.Parse(document.Id),
            UserId = document.UserId,
            Text = document.Text,
            ScheduledDateAndTime = document.ScheduledDateAndTime,
            IsCompleted = document.IsCompleted,
            IsDeleted = document.IsDeleted,
            ShouldPlaySound = document.ShouldPlaySound,
            ShouldDoVibration = document.ShouldDoVibration
        };
    }
}
