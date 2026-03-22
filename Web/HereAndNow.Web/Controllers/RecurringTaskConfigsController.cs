using System.Security.Claims;
using HereAndNowService.DTOs;
using HereAndNowService.Mappers;
using HereAndNowService.Models.Exceptions;
using HereAndNowService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HereAndNowService.Controllers;

/// <summary>
/// Controller for recurring task configuration query operations.
/// Provides list and get-by-id endpoints for recurring task configs.
/// </summary>
[ApiController]
[Route("api/v1/recurring-task-configs")]
[Authorize]
public class RecurringTaskConfigsController : ControllerBase
{
    private readonly IRecurringTaskService _recurringTaskService;
    private readonly ILogger<RecurringTaskConfigsController> _logger;

    /// <summary>
    /// Creates a new RecurringTaskConfigsController instance
    /// </summary>
    public RecurringTaskConfigsController(
        IRecurringTaskService recurringTaskService,
        ILogger<RecurringTaskConfigsController> logger)
    {
        _recurringTaskService = recurringTaskService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all recurring task configurations for the authenticated user
    /// </summary>
    /// <returns>List of recurring task configurations</returns>
    /// <response code="200">Returns the list of configurations</response>
    /// <response code="401">If the user is not authenticated</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<RecurringTaskConfigDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<RecurringTaskConfigDto>>> GetConfigs()
    {
        var userId = GetUserId();

        _logger.LogInformation("Getting all recurring task configs for user {UserId}", userId);

        var configs = await _recurringTaskService.GetAllConfigsAsync(userId);
        return Ok(RecurringTaskConfigMapper.ToDtoList(configs));
    }

    /// <summary>
    /// Gets a specific recurring task configuration by ID
    /// </summary>
    /// <param name="id">The configuration ID</param>
    /// <returns>The recurring task configuration</returns>
    /// <response code="200">Returns the configuration</response>
    /// <response code="404">If the configuration is not found</response>
    /// <response code="401">If the user is not authenticated</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(RecurringTaskConfigDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<RecurringTaskConfigDto>> GetConfigById(string id)
    {
        var userId = GetUserId();

        _logger.LogInformation("Getting recurring task config {ConfigId} for user {UserId}", id, userId);

        try
        {
            var config = await _recurringTaskService.GetConfigByIdAsync(userId, id);
            return Ok(RecurringTaskConfigMapper.ToDto(config));
        }
        catch (RecurringTaskConfigNotFoundException)
        {
            _logger.LogWarning("Recurring task config {ConfigId} not found for user {UserId}", id, userId);
            return NotFound(CreateErrorResponse(
                "RECURRING_TASK_CONFIG_NOT_FOUND",
                $"Recurring task config with ID {id} not found"));
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
