using System.Text.Json.Serialization;

namespace HereAndNowService.DTOs;

/// <summary>
/// Combined response DTO for CreateTaskAndTaskReminder command.
/// Contains both the task and its associated reminder that were created atomically.
/// </summary>
public class TaskAndReminderDto
{
    /// <summary>
    /// The created task
    /// </summary>
    [JsonPropertyName("task")]
    public TaskDto Task { get; set; } = null!;

    /// <summary>
    /// The created reminder
    /// </summary>
    [JsonPropertyName("reminder")]
    public TaskReminderDto Reminder { get; set; } = null!;
}
