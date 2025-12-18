using HereAndNowService.DTOs;
using HereAndNowService.Models;

namespace HereAndNowService.Mappers;

/// <summary>
/// Provides mapping methods between ReminderInstance domain models and DTOs.
/// </summary>
public static class ReminderInstanceMapper
{
    /// <summary>
    /// Maps a domain model to a response DTO.
    /// </summary>
    /// <param name="domain">The domain model to map.</param>
    /// <returns>The mapped DTO.</returns>
    public static ReminderInstanceDto ToDto(ReminderInstance domain)
    {
        return new ReminderInstanceDto
        {
            Id = domain.Id,
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
    /// Maps a collection of domain models to DTOs.
    /// </summary>
    /// <param name="domains">The domain models to map.</param>
    /// <returns>The mapped DTOs.</returns>
    public static IEnumerable<ReminderInstanceDto> ToDtos(IEnumerable<ReminderInstance> domains)
    {
        return domains.Select(ToDto);
    }
}
