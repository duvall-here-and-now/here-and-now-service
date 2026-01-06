using HereAndNowService.DTOs;
using HereAndNowService.Models;

namespace HereAndNowService.Mappers;

/// <summary>
/// Mapper for converting between TaskReminderDocument and TaskReminderDto
/// </summary>
public static class ReminderMapper
{
    /// <summary>
    /// Maps a TaskReminderDocument to a TaskReminderDto
    /// </summary>
    /// <param name="document">The reminder document from Cosmos DB</param>
    /// <returns>The DTO for API response</returns>
    public static TaskReminderDto ToDto(TaskReminderDocument document)
    {
        return new TaskReminderDto
        {
            Id = document.Id,
            TaskId = document.TaskId,
            TaskName = document.TaskName,
            ScheduledTime = document.ScheduledTime,
            IsDismissed = document.IsDismissed,
            DismissedAt = document.DismissedAt,
            CreatedAt = document.CreatedAt
        };
    }

    /// <summary>
    /// Maps a collection of TaskReminderDocuments to TaskReminderDtos
    /// </summary>
    /// <param name="documents">The reminder documents</param>
    /// <returns>Collection of DTOs for API response</returns>
    public static IEnumerable<TaskReminderDto> ToDtoList(IEnumerable<TaskReminderDocument> documents)
    {
        return documents.Select(ToDto);
    }
}
