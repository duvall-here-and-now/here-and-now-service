using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HereAndNowService.Commands;

/// <summary>
/// Base class for command requests. All commands use this structure with
/// a discriminator field and a type-specific payload.
/// </summary>
public class CommandRequest
{
    /// <summary>
    /// The command type discriminator (e.g., "CreateTask", "UpdateTaskState")
    /// </summary>
    [Required(ErrorMessage = "Command type is required")]
    [JsonPropertyName("command")]
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// The command-specific payload as a JSON element.
    /// This is deserialized to the appropriate command type based on the Command discriminator.
    /// </summary>
    [Required(ErrorMessage = "Payload is required")]
    [JsonPropertyName("payload")]
    public JsonElement Payload { get; set; }
}
