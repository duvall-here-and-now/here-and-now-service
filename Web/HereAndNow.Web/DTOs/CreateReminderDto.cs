using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HereAndNowService.DTOs;

/// <summary>
/// Data transfer object for creating a new reminder
/// </summary>
public class CreateReminderDto
{
    /// <summary>
    /// The ID of the task to attach the reminder to
    /// </summary>
    [Required(ErrorMessage = "TaskId is required")]
    [JsonPropertyName("taskId")]
    public string TaskId { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when the reminder should trigger
    /// </summary>
    [Required(ErrorMessage = "ScheduledTime is required")]
    [JsonPropertyName("scheduledTime")]
    public DateTime ScheduledTime { get; set; }
}
