using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HereAndNowService.Commands;

/// <summary>
/// Command payload for dismissing a task reminder.
/// Dismissing a reminder silences it without affecting the associated task.
/// This is an idempotent operation - dismissing an already-dismissed reminder succeeds.
/// </summary>
public class DismissTaskReminderCommand
{
    /// <summary>
    /// The ID of the task reminder to dismiss.
    /// Must be a valid GUID format (e.g., "660e8400-e29b-41d4-a716-446655440001").
    /// </summary>
    [Required(ErrorMessage = "taskReminderId is required")]
    [JsonPropertyName("taskReminderId")]
    public string TaskReminderId { get; set; } = string.Empty;
}
