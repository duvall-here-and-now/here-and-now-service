using HereAndNowService.DTOs;
using HereAndNowService.Models;

namespace HereAndNowService.Mappers;

/// <summary>
/// Mapper for converting between TaskDocument and TaskDto
/// </summary>
public static class TaskMapper
{
    /// <summary>
    /// Maps a TaskDocument to a TaskDto
    /// </summary>
    /// <param name="document">The task document from Cosmos DB</param>
    /// <returns>The DTO for API response</returns>
    public static TaskDto ToDto(TaskDocument document)
    {
        return new TaskDto
        {
            Id = document.Id,
            Name = document.Name,
            State = document.State,
            CreatedAt = document.CreatedAt,
            CompletedAt = document.CompletedAt,
            ReminderId = document.ReminderId,
            LastModifiedAt = document.LastModifiedAt
        };
    }

    /// <summary>
    /// Maps a collection of TaskDocuments to TaskDtos
    /// </summary>
    /// <param name="documents">The task documents</param>
    /// <returns>Collection of DTOs for API response</returns>
    public static IEnumerable<TaskDto> ToDtoList(IEnumerable<TaskDocument> documents)
    {
        return documents.Select(ToDto);
    }

    /// <summary>
    /// Maps a PagedResult of TaskDocuments to PagedTasksDto
    /// </summary>
    /// <param name="pagedResult">The paginated result from the service</param>
    /// <returns>The paginated DTO for API response</returns>
    public static PagedTasksDto ToPagedDto(PagedResult<TaskDocument> pagedResult)
    {
        return new PagedTasksDto
        {
            Items = pagedResult.Items.Select(ToDto).ToArray(),
            TotalCount = pagedResult.TotalCount,
            HasMore = pagedResult.HasMore
        };
    }
}
