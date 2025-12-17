using HereAndNowService.DTOs;
using HereAndNowService.Models;

namespace HereAndNowService.Mappers;

/// <summary>
/// Provides mapping methods between ReminderInstance domain models and DTOs.
/// </summary>
public static class ReminderInstanceMapper
{
    /// <summary>
    /// Maps a domain model to a DTO.
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
            ShouldDoVibration = domain.ShouldDoVibration
        };
    }

    /// <summary>
    /// Maps a DTO to a domain model.
    /// </summary>
    /// <param name="dto">The DTO to map.</param>
    /// <returns>The mapped domain model.</returns>
    public static ReminderInstance ToDomain(ReminderInstanceDto dto)
    {
        return new ReminderInstance
        {
            Id = dto.Id,
            Text = dto.Text,
            ScheduledDateAndTime = dto.ScheduledDateAndTime,
            IsCompleted = dto.IsCompleted,
            IsDeleted = dto.IsDeleted,
            ShouldPlaySound = dto.ShouldPlaySound,
            ShouldDoVibration = dto.ShouldDoVibration
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
