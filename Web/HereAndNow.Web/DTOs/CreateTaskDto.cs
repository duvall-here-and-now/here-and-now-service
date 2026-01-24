using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HereAndNowService.DTOs;

/// <summary>
/// Data transfer object for creating a new task
/// </summary>
public class CreateTaskDto
{
    /// <summary>
    /// The name/title of the task to create
    /// </summary>
    [Required(ErrorMessage = "Task name is required")]
    [MinLength(1, ErrorMessage = "Task name cannot be empty")]
    [MaxLength(500, ErrorMessage = "Task name cannot exceed 500 characters")]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional scheduled time for a reminder. When provided, creates both the task
    /// and an associated reminder. Should be a UTC datetime. Past times are accepted
    /// to support delayed sync scenarios from mobile clients.
    /// </summary>
    [DataType(DataType.DateTime)]
    [JsonPropertyName("scheduledTime")]
    public DateTime? ScheduledTime { get; set; }
}
