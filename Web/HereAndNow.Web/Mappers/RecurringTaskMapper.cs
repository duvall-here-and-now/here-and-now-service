using HereAndNowService.DTOs;
using HereAndNowService.Models;

namespace HereAndNowService.Mappers;

/// <summary>
/// Mapper for converting RecurringTaskInstance to flat RecurringTaskDto
/// </summary>
public static class RecurringTaskMapper
{
    /// <summary>
    /// Maps a RecurringTaskInstance to a flat RecurringTaskDto
    /// </summary>
    /// <param name="instance">The computed recurring task instance</param>
    /// <returns>The flat DTO for API response</returns>
    public static RecurringTaskDto ToDto(RecurringTaskInstance instance)
    {
        return new RecurringTaskDto
        {
            Id = instance.Id,
            ConfigId = instance.RecurringTaskConfigId,
            Text = instance.Text,
            RecurrenceDateAndTime = instance.RecurrenceDateAndTime,
            State = instance.State,
            RecurrenceRule = instance.RecurrenceRule
        };
    }

    /// <summary>
    /// Maps a collection of RecurringTaskInstances to RecurringTaskDtos
    /// </summary>
    /// <param name="instances">The computed recurring task instances</param>
    /// <returns>Collection of flat DTOs for API response</returns>
    public static IEnumerable<RecurringTaskDto> ToDtoList(IEnumerable<RecurringTaskInstance> instances)
    {
        return instances.Select(ToDto);
    }
}
