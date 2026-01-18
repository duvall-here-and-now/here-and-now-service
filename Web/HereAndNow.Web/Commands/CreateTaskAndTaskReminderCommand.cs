using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HereAndNowService.Commands;

/// <summary>
/// Command payload for atomically creating a new task and its associated reminder
/// with client-generated IDs. This enables optimistic UI patterns where the client
/// can display both entities immediately without waiting for server confirmation.
/// </summary>
/// <remarks>
/// Both the task and reminder are created in a single transactional batch operation,
/// ensuring either both succeed or both fail. The client provides both IDs to enable
/// immediate reference and optimistic UI updates.
/// </remarks>
public class CreateTaskAndTaskReminderCommand
{
    /// <summary>
    /// Client-generated unique identifier for the task.
    /// Must be a valid GUID format (e.g., "550e8400-e29b-41d4-a716-446655440000").
    /// </summary>
    [Required(ErrorMessage = "taskId is required")]
    [JsonPropertyName("taskId")]
    public string TaskId { get; set; } = string.Empty;

    /// <summary>
    /// Client-generated unique identifier for the task reminder.
    /// Must be a valid GUID format (e.g., "660e8400-e29b-41d4-a716-446655440001").
    /// </summary>
    [Required(ErrorMessage = "taskReminderId is required")]
    [JsonPropertyName("taskReminderId")]
    public string TaskReminderId { get; set; } = string.Empty;

    /// <summary>
    /// The name/title of the task. Cannot be empty or whitespace-only.
    /// </summary>
    [Required(ErrorMessage = "name is required")]
    [MinLength(1, ErrorMessage = "name cannot be empty")]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when the reminder should be triggered.
    /// Must be in the future at time of creation.
    /// </summary>
    [Required(ErrorMessage = "scheduledTime is required")]
    [JsonPropertyName("scheduledTime")]
    public DateTime ScheduledTime { get; set; }
}
