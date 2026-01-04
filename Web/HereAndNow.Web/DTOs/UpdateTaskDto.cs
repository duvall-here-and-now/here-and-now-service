using System.Text.Json.Serialization;
using HereAndNowService.Models;

namespace HereAndNowService.DTOs;

/// <summary>
/// Data transfer object for updating an existing task
/// </summary>
public class UpdateTaskDto
{
    /// <summary>
    /// The updated name/title of the task (optional)
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// The updated state of the task (optional).
    /// Valid values: OnDeck, InProgress, Completed, Deleted
    /// </summary>
    [JsonPropertyName("state")]
    public string? State { get; set; }

    /// <summary>
    /// Validates that the DTO contains at least one field to update
    /// and that the state value (if provided) is valid
    /// </summary>
    /// <param name="validationError">The validation error message if validation fails</param>
    /// <returns>True if valid, false otherwise</returns>
    public bool IsValid(out string? validationError)
    {
        // Check if at least one field is provided
        if (string.IsNullOrWhiteSpace(Name) && string.IsNullOrWhiteSpace(State))
        {
            validationError = "At least one field (name or state) must be provided";
            return false;
        }

        // Validate name if provided (not just whitespace)
        if (Name is not null && string.IsNullOrWhiteSpace(Name))
        {
            validationError = "Task name cannot be empty or whitespace";
            return false;
        }

        // Validate state if provided
        if (State is not null && !TaskState.IsValid(State))
        {
            validationError = $"Invalid state value. Must be one of: {string.Join(", ", TaskState.AllStates)}";
            return false;
        }

        validationError = null;
        return true;
    }
}
