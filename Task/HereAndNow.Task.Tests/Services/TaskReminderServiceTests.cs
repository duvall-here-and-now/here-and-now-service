using FluentAssertions;
using HereAndNowService.Models;
using HereAndNowService.Models.Exceptions;
using HereAndNowService.Repositories;
using HereAndNowService.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace HereAndNowService.TaskTests.Services;

public class TaskReminderServiceTests
{
    private readonly Mock<ITaskReminderRepository> _mockReminderRepository;
    private readonly Mock<ITaskRepository> _mockTaskRepository;
    private readonly Mock<ILogger<TaskReminderService>> _mockLogger;
    private readonly TaskReminderService _reminderService;
    private const string TestUserId = "auth0|test-user-123";

    public TaskReminderServiceTests()
    {
        _mockReminderRepository = new Mock<ITaskReminderRepository>();
        _mockTaskRepository = new Mock<ITaskRepository>();
        _mockLogger = new Mock<ILogger<TaskReminderService>>();
        _reminderService = new TaskReminderService(
            _mockReminderRepository.Object,
            _mockTaskRepository.Object,
            _mockLogger.Object);
    }

    #region CreateReminderAsync Tests

    [Fact]
    public async Task CreateReminderAsync_WithValidInput_SetsLastModifiedAtEqualToCreatedAt()
    {
        // Arrange
        var taskId = "task-123";
        var scheduledTime = DateTime.UtcNow.AddHours(2);
        TaskReminderDocument? capturedReminder = null;

        var existingTask = new TaskDocument
        {
            Id = taskId,
            UserId = TestUserId,
            Name = "Test Task",
            State = TaskState.OnDeck
        };

        _mockTaskRepository
            .Setup(r => r.GetByIdAsync(TestUserId, taskId))
            .ReturnsAsync(existingTask);

        _mockReminderRepository
            .Setup(r => r.GetByTaskIdAsync(TestUserId, taskId))
            .ReturnsAsync((TaskReminderDocument?)null);

        _mockReminderRepository
            .Setup(r => r.CreateWithTaskLinkAsync(It.IsAny<TaskReminderDocument>(), taskId))
            .Callback<TaskReminderDocument, string>((r, _) => capturedReminder = r)
            .ReturnsAsync((TaskReminderDocument r, string _) => r);

        // Act
        var result = await _reminderService.CreateReminderAsync(TestUserId, taskId, scheduledTime);

        // Assert
        result.LastModifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.LastModifiedAt.Should().Be(result.CreatedAt);
    }

    #endregion

    #region SnoozeAsync Tests

    [Fact]
    public async Task SnoozeAsync_WithValidInput_UpdatesLastModifiedAt()
    {
        // Arrange
        var reminderId = "reminder-123";
        var originalLastModified = DateTime.UtcNow.AddDays(-1);
        var newScheduledTime = DateTime.UtcNow.AddHours(2);

        var existingReminder = new TaskReminderDocument
        {
            Id = reminderId,
            UserId = TestUserId,
            TaskId = "task-123",
            TaskName = "Test Task",
            ScheduledTime = DateTime.UtcNow.AddMinutes(5),
            IsDismissed = false,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            LastModifiedAt = originalLastModified
        };

        _mockReminderRepository
            .Setup(r => r.GetByIdAsync(TestUserId, reminderId))
            .ReturnsAsync(existingReminder);

        _mockReminderRepository
            .Setup(r => r.UpdateAsync(It.IsAny<TaskReminderDocument>()))
            .ReturnsAsync((TaskReminderDocument r) => r);

        // Act
        var result = await _reminderService.SnoozeAsync(TestUserId, reminderId, newScheduledTime);

        // Assert
        result.LastModifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.LastModifiedAt.Should().BeAfter(originalLastModified);
    }

    #endregion

    #region DismissAsync Tests

    [Fact]
    public async Task DismissAsync_WithValidInput_UpdatesLastModifiedAt()
    {
        // Arrange
        var reminderId = "reminder-123";
        var originalLastModified = DateTime.UtcNow.AddDays(-1);
        TaskReminderDocument? capturedReminder = null;

        var existingReminder = new TaskReminderDocument
        {
            Id = reminderId,
            UserId = TestUserId,
            TaskId = "task-123",
            TaskName = "Test Task",
            ScheduledTime = DateTime.UtcNow.AddMinutes(5),
            IsDismissed = false,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            LastModifiedAt = originalLastModified
        };

        _mockReminderRepository
            .Setup(r => r.GetByIdAsync(TestUserId, reminderId))
            .ReturnsAsync(existingReminder);

        _mockReminderRepository
            .Setup(r => r.UpdateAsync(It.IsAny<TaskReminderDocument>()))
            .Callback<TaskReminderDocument>(r => capturedReminder = r)
            .ReturnsAsync((TaskReminderDocument r) => r);

        // Act
        await _reminderService.DismissAsync(TestUserId, reminderId);

        // Assert
        capturedReminder.Should().NotBeNull();
        capturedReminder!.LastModifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        capturedReminder.LastModifiedAt.Should().BeAfter(originalLastModified);
        capturedReminder.IsDismissed.Should().BeTrue();
        capturedReminder.DismissedAt.Should().NotBeNull();
    }

    #endregion
}
