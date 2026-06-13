using System.Security.Claims;
using FluentAssertions;
using HereAndNowService.Controllers;
using HereAndNowService.DTOs;
using HereAndNowService.Models;
using HereAndNowService.Models.Exceptions;
using HereAndNowService.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace HereAndNow.Web.Tests.Controllers;

/// <summary>
/// Unit tests for RecurringTaskConfigsController (Story 9.5).
/// Tests list configs, get by ID, and error handling.
/// </summary>
public class RecurringTaskConfigsControllerTests
{
    private readonly Mock<IRecurringTaskService> _mockRecurringTaskService;
    private readonly Mock<ILogger<RecurringTaskConfigsController>> _mockLogger;
    private readonly RecurringTaskConfigsController _controller;
    private const string TestUserId = "auth0|test-user-123";

    public RecurringTaskConfigsControllerTests()
    {
        _mockRecurringTaskService = new Mock<IRecurringTaskService>();
        _mockLogger = new Mock<ILogger<RecurringTaskConfigsController>>();
        _controller = new RecurringTaskConfigsController(
            _mockRecurringTaskService.Object,
            _mockLogger.Object);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, TestUserId)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    #region GetConfigs Tests

    [Fact]
    public async Task GetConfigs_ReturnsListOfConfigsMappedToDtos()
    {
        // Arrange
        var configs = new List<RecurringTaskConfigDocument>
        {
            new RecurringTaskConfigDocument
            {
                Id = "config-1",
                UserId = TestUserId,
                Text = "Daily standup",
                Rrule = "FREQ=DAILY;BYHOUR=9",
                StartDateAndTime = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new RecurringTaskConfigDocument
            {
                Id = "config-2",
                UserId = TestUserId,
                Text = "Weekly review",
                Rrule = "FREQ=WEEKLY;BYDAY=FR",
                StartDateAndTime = new DateTime(2026, 1, 3, 14, 0, 0, DateTimeKind.Utc),
                CreatedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)
            }
        }.AsReadOnly();

        _mockRecurringTaskService
            .Setup(s => s.GetAllConfigsAsync(TestUserId))
            .ReturnsAsync(configs);

        // Act
        var result = await _controller.GetConfigs();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dtos = okResult.Value.Should().BeAssignableTo<IEnumerable<RecurringTaskConfigDto>>().Subject.ToList();
        dtos.Should().HaveCount(2);
        dtos[0].Id.Should().Be("config-1");
        dtos[0].Text.Should().Be("Daily standup");
        dtos[0].Rrule.Should().Be("FREQ=DAILY;BYHOUR=9");
        dtos[1].Id.Should().Be("config-2");
        dtos[1].Text.Should().Be("Weekly review");
    }

    [Fact]
    public async Task GetConfigs_EmptyList_Returns200WithEmptyArray()
    {
        // Arrange
        _mockRecurringTaskService
            .Setup(s => s.GetAllConfigsAsync(TestUserId))
            .ReturnsAsync(new List<RecurringTaskConfigDocument>().AsReadOnly());

        // Act
        var result = await _controller.GetConfigs();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
        var dtos = okResult.Value.Should().BeAssignableTo<IEnumerable<RecurringTaskConfigDto>>().Subject.ToList();
        dtos.Should().BeEmpty();
    }

    #endregion

    #region GetConfigById Tests

    [Fact]
    public async Task GetConfigById_ExistingConfig_ReturnsMappedDto()
    {
        // Arrange
        var config = new RecurringTaskConfigDocument
        {
            Id = "config-1",
            UserId = TestUserId,
            Text = "Daily standup",
            Rrule = "FREQ=DAILY;BYHOUR=9",
            StartDateAndTime = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        _mockRecurringTaskService
            .Setup(s => s.GetConfigByIdAsync(TestUserId, "config-1"))
            .ReturnsAsync(config);

        // Act
        var result = await _controller.GetConfigById("config-1");

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<RecurringTaskConfigDto>().Subject;
        dto.Id.Should().Be("config-1");
        dto.Text.Should().Be("Daily standup");
        dto.Rrule.Should().Be("FREQ=DAILY;BYHOUR=9");
        dto.StartDateAndTime.Should().Be(new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc));
        dto.CreatedAt.Should().Be(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task GetConfigById_NonExistentId_Returns404WithErrorCode()
    {
        // Arrange
        _mockRecurringTaskService
            .Setup(s => s.GetConfigByIdAsync(TestUserId, "non-existent"))
            .ThrowsAsync(new RecurringTaskConfigNotFoundException("non-existent"));

        // Act
        var result = await _controller.GetConfigById("non-existent");

        // Assert
        var notFoundResult = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(404);
        var errorResponse = notFoundResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("RECURRING_TASK_CONFIG_NOT_FOUND");
    }

    #endregion

    #region HasReminder DTO Tests

    [Fact]
    public async Task GetConfigs_ReturnsHasReminderTrue_WhenDocumentHasReminderTrue()
    {
        // Arrange
        var configs = new List<RecurringTaskConfigDocument>
        {
            new RecurringTaskConfigDocument
            {
                Id = "config-reminder",
                UserId = TestUserId,
                Text = "Daily standup",
                Rrule = "FREQ=DAILY;BYHOUR=9",
                StartDateAndTime = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                HasReminder = true
            }
        }.AsReadOnly();

        _mockRecurringTaskService
            .Setup(s => s.GetAllConfigsAsync(TestUserId))
            .ReturnsAsync(configs);

        // Act
        var result = await _controller.GetConfigs();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dtos = okResult.Value.Should().BeAssignableTo<IEnumerable<RecurringTaskConfigDto>>().Subject.ToList();
        dtos[0].HasReminder.Should().BeTrue();
    }

    [Fact]
    public async Task GetConfigById_ReturnsHasReminderFalse_WhenDocumentHasReminderFalse()
    {
        // Arrange
        var config = new RecurringTaskConfigDocument
        {
            Id = "config-no-reminder",
            UserId = TestUserId,
            Text = "Weekly review",
            Rrule = "FREQ=WEEKLY;BYDAY=FR",
            StartDateAndTime = new DateTime(2026, 1, 3, 14, 0, 0, DateTimeKind.Utc),
            CreatedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            HasReminder = false
        };

        _mockRecurringTaskService
            .Setup(s => s.GetConfigByIdAsync(TestUserId, "config-no-reminder"))
            .ReturnsAsync(config);

        // Act
        var result = await _controller.GetConfigById("config-no-reminder");

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<RecurringTaskConfigDto>().Subject;
        dto.HasReminder.Should().BeFalse();
    }

    #endregion
}
