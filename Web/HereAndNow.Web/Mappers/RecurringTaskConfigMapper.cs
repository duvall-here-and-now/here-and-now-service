using HereAndNowService.DTOs;
using HereAndNowService.Models;

namespace HereAndNowService.Mappers;

/// <summary>
/// Mapper for converting between RecurringTaskConfigDocument and RecurringTaskConfigDto
/// </summary>
public static class RecurringTaskConfigMapper
{
    /// <summary>
    /// Maps a RecurringTaskConfigDocument to a RecurringTaskConfigDto
    /// </summary>
    /// <param name="document">The recurring task config document from Cosmos DB</param>
    /// <returns>The DTO for API response</returns>
    public static RecurringTaskConfigDto ToDto(RecurringTaskConfigDocument document)
    {
        return new RecurringTaskConfigDto
        {
            Id = document.Id,
            Text = document.Text,
            Rrule = document.Rrule,
            StartDateAndTime = document.StartDateAndTime,
            CreatedAt = document.CreatedAt,
            HasReminder = document.HasReminder
        };
    }

    /// <summary>
    /// Maps a collection of RecurringTaskConfigDocuments to RecurringTaskConfigDtos
    /// </summary>
    /// <param name="documents">The recurring task config documents</param>
    /// <returns>Collection of DTOs for API response</returns>
    public static IEnumerable<RecurringTaskConfigDto> ToDtoList(IEnumerable<RecurringTaskConfigDocument> documents)
    {
        return documents.Select(ToDto);
    }
}
