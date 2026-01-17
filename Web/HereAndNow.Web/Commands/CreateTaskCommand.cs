using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HereAndNowService.Commands;

/// <summary>
/// Command payload for creating a new task with a client-generated ID.
/// This enables optimistic UI patterns where the client can display the task
/// immediately without waiting for server confirmation.
/// </summary>
public class CreateTaskCommand
{
    /// <summary>
    /// Client-generated unique identifier for the task.
    /// Must be a valid GUID format (e.g., "550e8400-e29b-41d4-a716-446655440000").
    /// </summary>
    [Required(ErrorMessage = "taskId is required")]
    [JsonPropertyName("taskId")]
    public string TaskId { get; set; } = string.Empty;

    /// <summary>
    /// The name/title of the task. Cannot be empty.
    /// </summary>
    [Required(ErrorMessage = "name is required")]
    [MinLength(1, ErrorMessage = "name cannot be empty")]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
