using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HereAndNowService.Commands;

/// <summary>
/// Command payload for deleting a recurring task configuration and all its state overrides.
/// </summary>
public class DeleteRecurringTaskConfigCommand
{
    /// <summary>
    /// The ID of the recurring task config to delete (GUID format).
    /// </summary>
    [Required(ErrorMessage = "id is required")]
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
}
