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

public class RemindersControllerTests
{
    private readonly Mock<ITaskReminderService> _mockReminderService;
    private readonly Mock<ILogger<RemindersController>> _mockLogger;
    private readonly RemindersController _controller;
    private const string TestUserId = "auth0|test-user-123";

    public RemindersControllerTests()
    {
        _mockReminderService = new Mock<ITaskReminderService>();
        _mockLogger = new Mock<ILogger<RemindersController>>();
        _controller = new RemindersController(_mockReminderService.Object, _mockLogger.Object);

        // Set up authenticated user
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

    #region DismissReminder Tests

    [Fact]
    public async Task DismissReminder_WithValidId_Returns204NoContent()
    {
        // Arrange
        var reminderId = "reminder-123";

        _mockReminderService
            .Setup(s => s.DismissAsync(TestUserId, reminderId))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.DismissReminder(reminderId);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        _mockReminderService.Verify(s => s.DismissAsync(TestUserId, reminderId), Times.Once);
    }

    [Fact]
    public async Task DismissReminder_WithNonExistentId_Returns404NotFound()
    {
        // Arrange
        var reminderId = "nonexistent-reminder";

        _mockReminderService
            .Setup(s => s.DismissAsync(TestUserId, reminderId))
            .ThrowsAsync(new ReminderNotFoundException(reminderId));

        // Act
        var result = await _controller.DismissReminder(reminderId);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var errorResponse = notFoundResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("REMINDER_NOT_FOUND");
        errorResponse.Error.Message.Should().Contain(reminderId);
    }

    [Fact]
    public async Task DismissReminder_WhenAlreadyDismissed_Returns400BadRequest()
    {
        // Arrange
        var reminderId = "already-dismissed-reminder";

        _mockReminderService
            .Setup(s => s.DismissAsync(TestUserId, reminderId))
            .ThrowsAsync(new ReminderAlreadyDismissedException(reminderId));

        // Act
        var result = await _controller.DismissReminder(reminderId);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("REMINDER_ALREADY_DISMISSED");
        errorResponse.Error.Message.Should().Contain(reminderId);
    }

    [Fact]
    public async Task DismissReminder_TaskRemainUnchanged_NoTaskServiceCalled()
    {
        // Arrange
        // This test verifies that dismissing a reminder does NOT affect the task
        // The dismiss endpoint should only update the reminder, not the task
        var reminderId = "reminder-123";

        _mockReminderService
            .Setup(s => s.DismissAsync(TestUserId, reminderId))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.DismissReminder(reminderId);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        // Verify only reminder service was called (no task service injection in this controller)
        _mockReminderService.Verify(s => s.DismissAsync(TestUserId, reminderId), Times.Once);
    }

    #endregion

    #region SnoozeReminder Tests

    [Fact]
    public async Task SnoozeReminder_WithValidInput_ReturnsOkWithReminder()
    {
        // Arrange
        var reminderId = "reminder-123";
        var newScheduledTime = DateTime.UtcNow.AddHours(1);
        var snoozeDto = new SnoozeReminderDto { ScheduledTime = newScheduledTime };

        var updatedReminder = new TaskReminderDocument
        {
            Id = reminderId,
            UserId = TestUserId,
            TaskId = "task-123",
            TaskName = "Test Task",
            ScheduledTime = newScheduledTime,
            IsDismissed = false
        };

        _mockReminderService
            .Setup(s => s.SnoozeAsync(TestUserId, reminderId, newScheduledTime))
            .ReturnsAsync(updatedReminder);

        // Act
        var result = await _controller.SnoozeReminder(reminderId, snoozeDto);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var reminderDto = okResult.Value.Should().BeOfType<TaskReminderDto>().Subject;
        reminderDto.Id.Should().Be(reminderId);
        reminderDto.ScheduledTime.Should().Be(newScheduledTime);
    }

    [Fact]
    public async Task SnoozeReminder_WithNonExistentId_Returns404NotFound()
    {
        // Arrange
        var reminderId = "nonexistent-reminder";
        var snoozeDto = new SnoozeReminderDto { ScheduledTime = DateTime.UtcNow.AddHours(1) };

        _mockReminderService
            .Setup(s => s.SnoozeAsync(TestUserId, reminderId, It.IsAny<DateTime>()))
            .ThrowsAsync(new ReminderNotFoundException(reminderId));

        // Act
        var result = await _controller.SnoozeReminder(reminderId, snoozeDto);

        // Assert
        var notFoundResult = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var errorResponse = notFoundResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("REMINDER_NOT_FOUND");
    }

    [Fact]
    public async Task SnoozeReminder_WhenAlreadyDismissed_Returns400BadRequest()
    {
        // Arrange
        var reminderId = "dismissed-reminder";
        var snoozeDto = new SnoozeReminderDto { ScheduledTime = DateTime.UtcNow.AddHours(1) };

        _mockReminderService
            .Setup(s => s.SnoozeAsync(TestUserId, reminderId, It.IsAny<DateTime>()))
            .ThrowsAsync(new ReminderAlreadyDismissedException(reminderId));

        // Act
        var result = await _controller.SnoozeReminder(reminderId, snoozeDto);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("REMINDER_ALREADY_DISMISSED");
    }

    #endregion

    #region GetReminders Tests

    [Fact]
    public async Task GetReminders_ReturnsAllUserReminders()
    {
        // Arrange
        var reminders = new List<TaskReminderDocument>
        {
            new TaskReminderDocument
            {
                Id = "reminder-1",
                UserId = TestUserId,
                TaskId = "task-1",
                TaskName = "Task 1",
                ScheduledTime = DateTime.UtcNow.AddHours(1),
                IsDismissed = false
            },
            new TaskReminderDocument
            {
                Id = "reminder-2",
                UserId = TestUserId,
                TaskId = "task-2",
                TaskName = "Task 2",
                ScheduledTime = DateTime.UtcNow.AddHours(2),
                IsDismissed = false
            }
        };

        _mockReminderService
            .Setup(s => s.GetRemindersAsync(TestUserId))
            .ReturnsAsync(reminders);

        // Act
        var result = await _controller.GetReminders();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var reminderDtos = okResult.Value.Should().BeAssignableTo<IEnumerable<TaskReminderDto>>().Subject;
        reminderDtos.Should().HaveCount(2);
    }

    #endregion

    #region GetReminderById Tests

    [Fact]
    public async Task GetReminderById_WithValidId_ReturnsReminder()
    {
        // Arrange
        var reminderId = "reminder-123";
        var reminder = new TaskReminderDocument
        {
            Id = reminderId,
            UserId = TestUserId,
            TaskId = "task-123",
            TaskName = "Test Task",
            ScheduledTime = DateTime.UtcNow.AddHours(1),
            IsDismissed = false
        };

        _mockReminderService
            .Setup(s => s.GetReminderByIdAsync(TestUserId, reminderId))
            .ReturnsAsync(reminder);

        // Act
        var result = await _controller.GetReminderById(reminderId);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var reminderDto = okResult.Value.Should().BeOfType<TaskReminderDto>().Subject;
        reminderDto.Id.Should().Be(reminderId);
    }

    [Fact]
    public async Task GetReminderById_WithNonExistentId_Returns404NotFound()
    {
        // Arrange
        var reminderId = "nonexistent-reminder";

        _mockReminderService
            .Setup(s => s.GetReminderByIdAsync(TestUserId, reminderId))
            .ReturnsAsync((TaskReminderDocument?)null);

        // Act
        var result = await _controller.GetReminderById(reminderId);

        // Assert
        var notFoundResult = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var errorResponse = notFoundResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("REMINDER_NOT_FOUND");
    }

    #endregion
}
