using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HereAndNowService.Validation;

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
    /// and an associated reminder. Must be a UTC datetime in the future.
    /// </summary>
    [DataType(DataType.DateTime)]
    [FutureTimeValidation(ErrorMessage = "Scheduled time must be in the future")]
    [JsonPropertyName("scheduledTime")]
    public DateTime? ScheduledTime { get; set; }
}
