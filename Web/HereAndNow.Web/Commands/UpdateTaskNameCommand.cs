using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HereAndNowService.Commands;

/// <summary>
/// Command payload for updating a task's name.
/// When the task has an associated reminder, the reminder's denormalized TaskName
/// is also updated atomically to maintain data consistency.
/// </summary>
public class UpdateTaskNameCommand
{
    /// <summary>
    /// The ID of the task to update.
    /// Must be a valid GUID format (e.g., "550e8400-e29b-41d4-a716-446655440000").
    /// </summary>
    [Required(ErrorMessage = "taskId is required")]
    [JsonPropertyName("taskId")]
    public string TaskId { get; set; } = string.Empty;

    /// <summary>
    /// The new name for the task. Cannot be empty or whitespace-only.
    /// </summary>
    [Required(ErrorMessage = "name is required")]
    [MinLength(1, ErrorMessage = "name cannot be empty")]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
