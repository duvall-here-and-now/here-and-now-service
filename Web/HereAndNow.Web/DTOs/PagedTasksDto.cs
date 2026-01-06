using System.Text.Json.Serialization;

namespace HereAndNowService.DTOs;

/// <summary>
/// Data transfer object for paginated task list API responses
/// </summary>
public class PagedTasksDto
{
    /// <summary>
    /// The tasks in the current page
    /// </summary>
    [JsonPropertyName("items")]
    public IReadOnlyList<TaskDto> Items { get; set; } = Array.Empty<TaskDto>();

    /// <summary>
    /// Total count of all tasks matching the query (across all pages)
    /// </summary>
    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    /// <summary>
    /// Indicates whether more tasks exist beyond the current page
    /// </summary>
    [JsonPropertyName("hasMore")]
    public bool HasMore { get; set; }
}
