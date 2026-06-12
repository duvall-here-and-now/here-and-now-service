using System.Security.Claims;
using HereAndNowService.DTOs;
using HereAndNowService.Mappers;
using HereAndNowService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HereAndNowService.Controllers;

/// <summary>
/// Controller for computed recurring task instance queries.
/// Returns computed instances for a date range with resolved states.
/// </summary>
[ApiController]
[Route("api/v1/recurring-tasks")]
[Authorize]
public class RecurringTasksController : ControllerBase
{
    private readonly IRecurringTaskService _recurringTaskService;
    private readonly ILogger<RecurringTasksController> _logger;

    /// <summary>
    /// Creates a new RecurringTasksController instance
    /// </summary>
    public RecurringTasksController(
        IRecurringTaskService recurringTaskService,
        ILogger<RecurringTasksController> logger)
    {
        _recurringTaskService = recurringTaskService;
        _logger = logger;
    }

    /// <summary>
    /// Gets computed recurring task instances for a date range.
    /// Returns flat DTOs with resolved states (Scheduled, OnDeck, InProgress, Completed, Skipped).
    /// </summary>
    /// <param name="from">Start of date range (inclusive, ISO 8601 UTC)</param>
    /// <param name="to">End of date range (inclusive, ISO 8601 UTC)</param>
    /// <returns>List of computed recurring task instances</returns>
    /// <response code="200">Returns the computed instances</response>
    /// <response code="400">If date parameters are missing, invalid, or range exceeds 365 days</response>
    /// <response code="401">If the user is not authenticated</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<RecurringTaskDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<RecurringTaskDto>>> GetInstances(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var userId = GetUserId();

        if (!from.HasValue || !to.HasValue)
        {
            _logger.LogWarning(
                "Missing date parameters for recurring tasks query. from={From}, to={To}, user={UserId}",
                from, to, userId);
            return BadRequest(CreateErrorResponse(
                "VALIDATION_ERROR",
                "Both 'from' and 'to' query parameters are required"));
        }

        // Normalize DateTimeKind — ASP.NET Core query binding often produces Unspecified
        var fromUtc = DateTime.SpecifyKind(from.Value, DateTimeKind.Utc);
        var toUtc = DateTime.SpecifyKind(to.Value, DateTimeKind.Utc);

        if (fromUtc >= toUtc)
        {
            _logger.LogWarning(
                "Invalid date range: from ({From}) must be before to ({To}), user={UserId}",
                fromUtc, toUtc, userId);
            return BadRequest(CreateErrorResponse(
                "VALIDATION_ERROR",
                "'from' must be before 'to'"));
        }

        _logger.LogInformation(
            "Getting computed recurring task instances for user {UserId}: from={From}, to={To}",
            userId, fromUtc, toUtc);

        try
        {
            var instances = await _recurringTaskService.GetComputedInstancesForAllConfigsAsync(userId, fromUtc, toUtc);
            return Ok(RecurringTaskMapper.ToDtoList(instances));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex,
                "Validation error for recurring tasks query. user={UserId}", userId);
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", ex.Message));
        }
    }

    /// <summary>
    /// Extracts the user ID from the JWT claims
    /// </summary>
    private string GetUserId()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            throw new UnauthorizedAccessException("User ID not found in token");
        }

        return userId;
    }

    /// <summary>
    /// Creates a standard error response
    /// </summary>
    private static ErrorResponseDto CreateErrorResponse(string code, string message)
    {
        return new ErrorResponseDto
        {
            Error = new ErrorDetailsDto
            {
                Code = code,
                Message = message
            }
        };
    }
}
