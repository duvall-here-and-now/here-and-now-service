using HereAndNowService.Models;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="ReminderInstancesController"/> class.
    /// </summary>
    /// <param name="reminderInstanceService">The reminder instance service.</param>
    public ReminderInstancesController(IReminderInstanceService reminderInstanceService)
    {
        _reminderInstanceService = reminderInstanceService;
    }

    /// <summary>
    /// Gets all reminder instances.
    /// </summary>
    /// <returns>A collection of all reminder instances.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ReminderInstance>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<IEnumerable<ReminderInstance>> GetAll()
    {
        var reminders = _reminderInstanceService.GetAll();
        return Ok(reminders);
    }

    /// <summary>
    /// Gets a specific reminder instance by ID.
    /// </summary>
    /// <param name="id">The unique identifier of the reminder.</param>
    /// <returns>The reminder instance if found.</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ReminderInstance), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<ReminderInstance> GetById(Guid id)
    {
        var reminder = _reminderInstanceService.GetById(id);

        if (reminder == null)
        {
            return NotFound(new { message = $"Reminder with ID {id} not found." });
        }

        return Ok(reminder);
    }

    /// <summary>
    /// Creates a new reminder instance.
    /// </summary>
    /// <param name="reminder">The reminder instance to create.</param>
    /// <returns>The created reminder instance.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ReminderInstance), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<ReminderInstance> Create([FromBody] ReminderInstance reminder)
    {
        if (reminder == null)
        {
            return BadRequest(new { message = "Reminder data is required." });
        }

        var createdReminder = _reminderInstanceService.Create(reminder);

        return CreatedAtAction(
            nameof(GetById),
            new { id = createdReminder.id },
            createdReminder
        );
    }

    /// <summary>
    /// Updates an existing reminder instance.
    /// </summary>
    /// <param name="id">The unique identifier of the reminder to update.</param>
    /// <param name="reminder">The updated reminder data.</param>
    /// <returns>The updated reminder instance.</returns>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ReminderInstance), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<ReminderInstance> Update(Guid id, [FromBody] ReminderInstance reminder)
    {
        if (reminder == null)
        {
            return BadRequest(new { message = "Reminder data is required." });
        }

        var updatedReminder = _reminderInstanceService.Update(id, reminder);

        if (updatedReminder == null)
        {
            return NotFound(new { message = $"Reminder with ID {id} not found." });
        }

        return Ok(updatedReminder);
    }

    /// <summary>
    /// Deletes a reminder instance.
    /// </summary>
    /// <param name="id">The unique identifier of the reminder to delete.</param>
    /// <returns>No content if successful.</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult Delete(Guid id)
    {
        var deleted = _reminderInstanceService.Delete(id);

        if (!deleted)
        {
            return NotFound(new { message = $"Reminder with ID {id} not found." });
        }

        return NoContent();
    }
}
