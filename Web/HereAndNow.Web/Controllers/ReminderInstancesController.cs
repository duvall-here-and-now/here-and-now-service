using HereAndNowService.DTOs;
using HereAndNowService.Mappers;
using HereAndNowService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HereAndNowService.Controllers;

/// <summary>
/// Controller for managing reminder instances. All endpoints require authorization.
/// </summary>
[ApiController]
[Route("api/reminder-instances")]
[Authorize]
public class ReminderInstancesController : ControllerBase
{
    private readonly IReminderInstanceService _reminderInstanceService;
    private readonly ILogger<ReminderInstancesController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReminderInstancesController"/> class.
    /// </summary>
    /// <param name="reminderInstanceService">The reminder instance service.</param>
    /// <param name="logger">The logger instance.</param>
    public ReminderInstancesController(IReminderInstanceService reminderInstanceService, ILogger<ReminderInstancesController> logger)
    {
        _reminderInstanceService = reminderInstanceService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all reminder instances.
    /// </summary>
    /// <returns>A collection of all reminder instances.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ReminderInstanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<IEnumerable<ReminderInstanceDto>> GetAll()
    {
        _logger.LogInformation("GET /api/reminder-instances - Request received to get all reminders");
        var reminders = _reminderInstanceService.GetAll();
        var dtos = ReminderInstanceMapper.ToDtos(reminders);
        _logger.LogInformation("GET /api/reminder-instances - Returning {Count} reminders with Status 200 OK", dtos.Count());
        return Ok(dtos);
    }

    /// <summary>
    /// Gets a specific reminder instance by ID.
    /// </summary>
    /// <param name="id">The unique identifier of the reminder.</param>
    /// <returns>The reminder instance if found.</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ReminderInstanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<ReminderInstanceDto> GetById(Guid id)
    {
        _logger.LogInformation("GET /api/reminder-instances/{ReminderId} - Request received", id);
        var reminder = _reminderInstanceService.GetById(id);

        if (reminder == null)
        {
            _logger.LogWarning("GET /api/reminder-instances/{ReminderId} - Reminder not found, returning 404 Not Found", id);
            return NotFound(new { message = $"Reminder with ID {id} not found." });
        }

        _logger.LogInformation("GET /api/reminder-instances/{ReminderId} - Returning reminder with Status 200 OK", id);
        return Ok(ReminderInstanceMapper.ToDto(reminder));
    }

    /// <summary>
    /// Creates a new reminder instance.
    /// </summary>
    /// <param name="reminderDto">The reminder instance to create.</param>
    /// <returns>The created reminder instance.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ReminderInstanceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<ReminderInstanceDto> Create([FromBody] ReminderInstanceDto reminderDto)
    {
        _logger.LogInformation("POST /api/reminder-instances - Request received to create new reminder");
        var domain = ReminderInstanceMapper.ToDomain(reminderDto);
        var createdReminder = _reminderInstanceService.Create(domain);
        var resultDto = ReminderInstanceMapper.ToDto(createdReminder);
        _logger.LogInformation("POST /api/reminder-instances - Successfully created reminder with ID: {ReminderId}, returning 201 Created", createdReminder.Id);

        return CreatedAtAction(
            nameof(GetById),
            new { id = resultDto.Id },
            resultDto
        );
    }

    /// <summary>
    /// Updates an existing reminder instance.
    /// </summary>
    /// <param name="id">The unique identifier of the reminder to update.</param>
    /// <param name="reminderDto">The updated reminder data.</param>
    /// <returns>The updated reminder instance.</returns>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ReminderInstanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<ReminderInstanceDto> Update(Guid id, [FromBody] ReminderInstanceDto reminderDto)
    {
        _logger.LogInformation("PUT /api/reminder-instances/{ReminderId} - Request received to update reminder", id);

        if (reminderDto.Id != Guid.Empty && reminderDto.Id != id)
        {
            _logger.LogWarning("PUT /api/reminder-instances/{ReminderId} - ID mismatch: URL ID does not match body ID {BodyId}, returning 400 Bad Request", id, reminderDto.Id);
            return BadRequest(new { message = "ID in URL and body do not match." });
        }

        var domain = ReminderInstanceMapper.ToDomain(reminderDto);
        var updatedReminder = _reminderInstanceService.Update(id, domain);

        if (updatedReminder == null)
        {
            _logger.LogWarning("PUT /api/reminder-instances/{ReminderId} - Reminder not found, returning 404 Not Found", id);
            return NotFound(new { message = $"Reminder with ID {id} not found." });
        }

        _logger.LogInformation("PUT /api/reminder-instances/{ReminderId} - Successfully updated reminder, returning 200 OK", id);
        return Ok(ReminderInstanceMapper.ToDto(updatedReminder));
    }

    /// <summary>
    /// Soft-deletes a reminder instance.
    /// </summary>
    /// <param name="id">The unique identifier of the reminder to delete.</param>
    /// <returns>No content if successful.</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult Delete(Guid id)
    {
        _logger.LogInformation("DELETE /api/reminder-instances/{ReminderId} - Request received to delete reminder", id);
        var deleted = _reminderInstanceService.Delete(id);

        if (!deleted)
        {
            _logger.LogWarning("DELETE /api/reminder-instances/{ReminderId} - Reminder not found, returning 404 Not Found", id);
            return NotFound(new { message = $"Reminder with ID {id} not found." });
        }

        _logger.LogInformation("DELETE /api/reminder-instances/{ReminderId} - Successfully deleted reminder, returning 204 No Content", id);
        return NoContent();
    }
}
