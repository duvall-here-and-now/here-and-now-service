using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HereAndNowService.Commands;

/// <summary>
/// Command payload for updating a task's state.
/// Supports all state transitions: OnDeck, InProgress, Completed, Deleted.
/// When transitioning to Completed or Deleted and the task has an associated reminder,
/// the reminder is atomically dismissed (Task-Reminder Unity pattern).
/// Idempotent: transitioning to the same state returns success without changes.
/// </summary>
public class UpdateTaskStateCommand
{
    /// <summary>
    /// The ID of the task to update.
    /// Must be a valid GUID format (e.g., "550e8400-e29b-41d4-a716-446655440000").
    /// </summary>
    [Required(ErrorMessage = "taskId is required")]
    [JsonPropertyName("taskId")]
    public string TaskId { get; set; } = string.Empty;

    /// <summary>
    /// The target state for the task.
    /// Valid values: "OnDeck", "InProgress", "Completed", "Deleted" (case-sensitive).
    /// Note: Deleted tasks cannot transition to other states (terminal state).
    /// </summary>
    [Required(ErrorMessage = "state is required")]
    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;
}
