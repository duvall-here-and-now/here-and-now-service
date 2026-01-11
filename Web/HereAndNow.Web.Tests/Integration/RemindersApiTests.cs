using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using HereAndNow.Web.Tests.Helpers;
using HereAndNowService.DTOs;
using HereAndNowService.Models;
using HereAndNowService.Models.Exceptions;
using Moq;

namespace HereAndNow.Web.Tests.Integration;

public class RemindersApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public RemindersApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    #region Create Reminder Tests (AC: 1, 4, 5)

    [Fact]
    public async Task POST_CreateReminder_ReturnsCreatedWithReminder()
    {
        // Arrange (AC: 1)
        var createDto = new CreateReminderDto
        {
            TaskId = "task-123",
            ScheduledTime = new DateTime(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc)
        };

        var createdReminder = new TaskReminderDocument
        {
            Id = "reminder-456",
            UserId = TestAuthHandler.TestUserId,
            TaskId = "task-123",
            TaskName = "Submit expense report",
            ScheduledTime = new DateTime(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc),
            IsDismissed = false,
            CreatedAt = DateTime.UtcNow
        };

        _factory.MockReminderService
            .Setup(s => s.CreateReminderAsync(
                TestAuthHandler.TestUserId,
                "task-123",
                It.IsAny<DateTime>()))
            .ReturnsAsync(createdReminder);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/reminders", createDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var reminderDto = await response.Content.ReadFromJsonAsync<TaskReminderDto>();
        reminderDto.Should().NotBeNull();
        reminderDto!.Id.Should().Be("reminder-456");
        reminderDto.TaskId.Should().Be("task-123");
        reminderDto.TaskName.Should().Be("Submit expense report");
        reminderDto.ScheduledTime.Should().Be(new DateTime(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc));
        reminderDto.IsDismissed.Should().BeFalse();
    }

    [Fact]
    public async Task POST_CreateReminder_UpdatesTaskReminderId()
    {
        // Arrange (AC: 1 - bidirectional link)
        var createDto = new CreateReminderDto
        {
            TaskId = "task-for-reminder",
            ScheduledTime = DateTime.UtcNow.AddDays(1)
        };

        var createdReminder = new TaskReminderDocument
        {
            Id = "new-reminder-id",
            UserId = TestAuthHandler.TestUserId,
            TaskId = "task-for-reminder",
            TaskName = "Linked Task",
            ScheduledTime = DateTime.UtcNow.AddDays(1),
            IsDismissed = false,
            CreatedAt = DateTime.UtcNow
        };

        _factory.MockReminderService
            .Setup(s => s.CreateReminderAsync(
                TestAuthHandler.TestUserId,
                "task-for-reminder",
                It.IsAny<DateTime>()))
            .ReturnsAsync(createdReminder);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/reminders", createDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // Verify the service was called (which handles updating task's reminderId)
        _factory.MockReminderService.Verify(
            s => s.CreateReminderAsync(
                TestAuthHandler.TestUserId,
                "task-for-reminder",
                It.IsAny<DateTime>()),
            Times.Once);
    }

    [Fact]
    public async Task POST_CreateReminder_InvalidTaskId_Returns404()
    {
        // Arrange (AC: 4)
        var createDto = new CreateReminderDto
        {
            TaskId = "non-existent-task",
            ScheduledTime = DateTime.UtcNow.AddDays(1)
        };

        _factory.MockReminderService
            .Setup(s => s.CreateReminderAsync(
                TestAuthHandler.TestUserId,
                "non-existent-task",
                It.IsAny<DateTime>()))
            .ThrowsAsync(new TaskNotFoundException("non-existent-task"));

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/reminders", createDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        errorResponse!.Error.Code.Should().Be("TASK_NOT_FOUND");
    }

    [Fact]
    public async Task POST_CreateReminder_TaskAlreadyHasReminder_Returns400()
    {
        // Arrange (AC: 5)
        var createDto = new CreateReminderDto
        {
            TaskId = "task-with-reminder",
            ScheduledTime = DateTime.UtcNow.AddDays(1)
        };

        _factory.MockReminderService
            .Setup(s => s.CreateReminderAsync(
                TestAuthHandler.TestUserId,
                "task-with-reminder",
                It.IsAny<DateTime>()))
            .ThrowsAsync(new ReminderAlreadyExistsException("task-with-reminder"));

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/reminders", createDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        errorResponse!.Error.Code.Should().Be("REMINDER_ALREADY_EXISTS");
    }

    #endregion

    #region Get Reminders Tests (AC: 2, 3)

    [Fact]
    public async Task GET_Reminders_ReturnsOnlyNonDismissedSortedByTime()
    {
        // Arrange (AC: 2)
        var reminders = new List<TaskReminderDocument>
        {
            new TaskReminderDocument
            {
                Id = "reminder-1",
                UserId = TestAuthHandler.TestUserId,
                TaskId = "task-1",
                TaskName = "Early Task",
                ScheduledTime = new DateTime(2026, 1, 4, 9, 0, 0, DateTimeKind.Utc),
                IsDismissed = false,
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            },
            new TaskReminderDocument
            {
                Id = "reminder-2",
                UserId = TestAuthHandler.TestUserId,
                TaskId = "task-2",
                TaskName = "Later Task",
                ScheduledTime = new DateTime(2026, 1, 5, 14, 0, 0, DateTimeKind.Utc),
                IsDismissed = false,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            }
        };

        _factory.MockReminderService
            .Setup(s => s.GetRemindersAsync(TestAuthHandler.TestUserId))
            .ReturnsAsync(reminders);

        // Act
        var response = await _client.GetAsync("/api/v1/reminders");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var reminderDtos = await response.Content.ReadFromJsonAsync<List<TaskReminderDto>>();

        reminderDtos.Should().HaveCount(2);
        reminderDtos.Should().NotContain(r => r.IsDismissed);

        // Verify sorted by scheduledTime (earlier first)
        reminderDtos![0].ScheduledTime.Should().BeBefore(reminderDtos[1].ScheduledTime);
    }

    [Fact]
    public async Task GET_Reminders_IncludesTaskName()
    {
        // Arrange (AC: 3)
        var reminders = new List<TaskReminderDocument>
        {
            new TaskReminderDocument
            {
                Id = "reminder-with-taskname",
                UserId = TestAuthHandler.TestUserId,
                TaskId = "task-abc",
                TaskName = "Important Meeting Prep",
                ScheduledTime = DateTime.UtcNow.AddDays(1),
                IsDismissed = false,
                CreatedAt = DateTime.UtcNow
            }
        };

        _factory.MockReminderService
            .Setup(s => s.GetRemindersAsync(TestAuthHandler.TestUserId))
            .ReturnsAsync(reminders);

        // Act
        var response = await _client.GetAsync("/api/v1/reminders");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var reminderDtos = await response.Content.ReadFromJsonAsync<List<TaskReminderDto>>();

        reminderDtos.Should().HaveCount(1);
        reminderDtos![0].TaskName.Should().Be("Important Meeting Prep");
    }

    [Fact]
    public async Task GET_ReminderById_ReturnsReminder()
    {
        // Arrange
        var reminder = new TaskReminderDocument
        {
            Id = "specific-reminder",
            UserId = TestAuthHandler.TestUserId,
            TaskId = "task-xyz",
            TaskName = "Specific Task",
            ScheduledTime = DateTime.UtcNow.AddDays(2),
            IsDismissed = false,
            CreatedAt = DateTime.UtcNow
        };

        _factory.MockReminderService
            .Setup(s => s.GetReminderByIdAsync(TestAuthHandler.TestUserId, "specific-reminder"))
            .ReturnsAsync(reminder);

        // Act
        var response = await _client.GetAsync("/api/v1/reminders/specific-reminder");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var reminderDto = await response.Content.ReadFromJsonAsync<TaskReminderDto>();
        reminderDto!.Id.Should().Be("specific-reminder");
        reminderDto.TaskName.Should().Be("Specific Task");
    }

    [Fact]
    public async Task GET_ReminderById_NotFound_Returns404()
    {
        // Arrange
        _factory.MockReminderService
            .Setup(s => s.GetReminderByIdAsync(TestAuthHandler.TestUserId, "non-existent-reminder"))
            .ReturnsAsync((TaskReminderDocument?)null);

        // Act
        var response = await _client.GetAsync("/api/v1/reminders/non-existent-reminder");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        errorResponse!.Error.Code.Should().Be("REMINDER_NOT_FOUND");
    }

    #endregion

    #region User Isolation Tests (AC: 7)

    [Fact]
    public async Task GET_Reminders_UserIsolation_ReturnsOnlyOwnReminders()
    {
        // Arrange (AC: 7 - can't access other user's reminders)
        var currentUserReminders = new List<TaskReminderDocument>
        {
            new TaskReminderDocument
            {
                Id = "my-reminder",
                UserId = TestAuthHandler.TestUserId,
                TaskId = "my-task",
                TaskName = "My Reminder",
                ScheduledTime = DateTime.UtcNow.AddDays(1),
                IsDismissed = false,
                CreatedAt = DateTime.UtcNow
            }
        };

        _factory.MockReminderService
            .Setup(s => s.GetRemindersAsync(TestAuthHandler.TestUserId))
            .ReturnsAsync(currentUserReminders);

        // Act
        var response = await _client.GetAsync("/api/v1/reminders");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify the service was called with the correct user ID
        _factory.MockReminderService.Verify(
            s => s.GetRemindersAsync(TestAuthHandler.TestUserId),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task POST_CreateReminder_UsesAuthenticatedUserId()
    {
        // Arrange
        var createDto = new CreateReminderDto
        {
            TaskId = "user-task",
            ScheduledTime = DateTime.UtcNow.AddDays(1)
        };

        var createdReminder = new TaskReminderDocument
        {
            Id = "user-reminder",
            UserId = TestAuthHandler.TestUserId,
            TaskId = "user-task",
            TaskName = "User's Task",
            ScheduledTime = DateTime.UtcNow.AddDays(1),
            IsDismissed = false,
            CreatedAt = DateTime.UtcNow
        };

        _factory.MockReminderService
            .Setup(s => s.CreateReminderAsync(
                TestAuthHandler.TestUserId,
                "user-task",
                It.IsAny<DateTime>()))
            .ReturnsAsync(createdReminder);

        // Act
        await _client.PostAsJsonAsync("/api/v1/reminders", createDto);

        // Assert - verify service was called with correct user ID from auth
        _factory.MockReminderService.Verify(
            s => s.CreateReminderAsync(
                TestAuthHandler.TestUserId,
                "user-task",
                It.IsAny<DateTime>()),
            Times.Once);
    }

    #endregion

    #region Authentication Tests

    [Fact]
    public async Task GET_Reminders_WithoutAuthentication_Returns401()
    {
        // Arrange
        _client.DefaultRequestHeaders.Add("X-Test-Unauthenticated", "true");

        // Act
        var response = await _client.GetAsync("/api/v1/reminders");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task POST_CreateReminder_WithoutAuthentication_Returns401()
    {
        // Arrange
        _client.DefaultRequestHeaders.Add("X-Test-Unauthenticated", "true");
        var createDto = new CreateReminderDto
        {
            TaskId = "task-123",
            ScheduledTime = DateTime.UtcNow.AddDays(1)
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/reminders", createDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task POST_CreateReminder_MissingTaskId_Returns400()
    {
        // Arrange - send request without taskId
        var createDto = new { scheduledTime = DateTime.UtcNow.AddDays(1) };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/reminders", createDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        errorResponse!.Error.Code.Should().Be("VALIDATION_ERROR");
    }

    #endregion

    #region LastModifiedAt Tests

    [Fact]
    public async Task POST_CreateReminder_ResponseIncludesLastModifiedAt()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var createDto = new CreateReminderDto
        {
            TaskId = "task-lastmodified",
            ScheduledTime = DateTime.UtcNow.AddDays(1)
        };

        var createdReminder = new TaskReminderDocument
        {
            Id = "reminder-lastmodified",
            UserId = TestAuthHandler.TestUserId,
            TaskId = "task-lastmodified",
            TaskName = "Task with LastModifiedAt",
            ScheduledTime = DateTime.UtcNow.AddDays(1),
            IsDismissed = false,
            CreatedAt = now,
            LastModifiedAt = now
        };

        _factory.MockReminderService
            .Setup(s => s.CreateReminderAsync(
                TestAuthHandler.TestUserId,
                "task-lastmodified",
                It.IsAny<DateTime>()))
            .ReturnsAsync(createdReminder);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/reminders", createDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var reminderDto = await response.Content.ReadFromJsonAsync<TaskReminderDto>();
        reminderDto.Should().NotBeNull();
        reminderDto!.LastModifiedAt.Should().BeCloseTo(now, TimeSpan.FromSeconds(1));
        reminderDto.LastModifiedAt.Should().Be(reminderDto.CreatedAt);
    }

    [Fact]
    public async Task PUT_SnoozeReminder_ResponseIncludesUpdatedLastModifiedAt()
    {
        // Arrange
        var reminderId = "reminder-snooze-lastmod";
        var originalCreatedAt = DateTime.UtcNow.AddDays(-1);
        var updatedLastModified = DateTime.UtcNow;
        var newScheduledTime = DateTime.UtcNow.AddHours(2);

        var updatedReminder = new TaskReminderDocument
        {
            Id = reminderId,
            UserId = TestAuthHandler.TestUserId,
            TaskId = "task-snooze",
            TaskName = "Snoozed Task",
            ScheduledTime = newScheduledTime,
            IsDismissed = false,
            CreatedAt = originalCreatedAt,
            LastModifiedAt = updatedLastModified
        };

        _factory.MockReminderService
            .Setup(s => s.SnoozeAsync(
                TestAuthHandler.TestUserId,
                reminderId,
                It.IsAny<DateTime>()))
            .ReturnsAsync(updatedReminder);

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/v1/reminders/{reminderId}",
            new SnoozeReminderDto { ScheduledTime = newScheduledTime });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var reminderDto = await response.Content.ReadFromJsonAsync<TaskReminderDto>();
        reminderDto.Should().NotBeNull();
        reminderDto!.LastModifiedAt.Should().BeCloseTo(updatedLastModified, TimeSpan.FromSeconds(1));
        reminderDto.LastModifiedAt.Should().BeAfter(reminderDto.CreatedAt);
    }

    #endregion

    #region Snooze Reminder Tests (Story 4-1 AC: 1-5)

    [Fact]
    public async Task PUT_SnoozeReminder_UpdatesScheduledTime()
    {
        // Arrange (AC: 1)
        var reminderId = "reminder-to-snooze";
        var newScheduledTime = DateTime.UtcNow.AddHours(2);
        var snoozeDto = new SnoozeReminderDto
        {
            ScheduledTime = newScheduledTime
        };

        var updatedReminder = new TaskReminderDocument
        {
            Id = reminderId,
            UserId = TestAuthHandler.TestUserId,
            TaskId = "task-123",
            TaskName = "Submit expense report",
            ScheduledTime = newScheduledTime,
            IsDismissed = false,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        _factory.MockReminderService
            .Setup(s => s.SnoozeAsync(
                TestAuthHandler.TestUserId,
                reminderId,
                It.IsAny<DateTime>()))
            .ReturnsAsync(updatedReminder);

        // Act
        var response = await _client.PutAsJsonAsync($"/api/v1/reminders/{reminderId}", snoozeDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var reminderDto = await response.Content.ReadFromJsonAsync<TaskReminderDto>();
        reminderDto.Should().NotBeNull();
        reminderDto!.Id.Should().Be(reminderId);
        reminderDto.ScheduledTime.Should().BeCloseTo(newScheduledTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task PUT_SnoozeReminder_ReturnsUpdatedReminder()
    {
        // Arrange (AC: 1)
        var reminderId = "reminder-456";
        var newTime = DateTime.UtcNow.AddDays(1);

        var updatedReminder = new TaskReminderDocument
        {
            Id = reminderId,
            UserId = TestAuthHandler.TestUserId,
            TaskId = "task-789",
            TaskName = "Updated Task Name",
            ScheduledTime = newTime,
            IsDismissed = false,
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        };

        _factory.MockReminderService
            .Setup(s => s.SnoozeAsync(
                TestAuthHandler.TestUserId,
                reminderId,
                It.IsAny<DateTime>()))
            .ReturnsAsync(updatedReminder);

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/v1/reminders/{reminderId}",
            new { scheduledTime = newTime.ToString("o") });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var reminderDto = await response.Content.ReadFromJsonAsync<TaskReminderDto>();
        reminderDto!.TaskName.Should().Be("Updated Task Name");
        reminderDto.IsDismissed.Should().BeFalse();
    }

    [Fact]
    public async Task PUT_SnoozeReminder_InvalidId_Returns404()
    {
        // Arrange (AC: 3)
        var invalidId = "non-existent-reminder";
        var snoozeDto = new SnoozeReminderDto
        {
            ScheduledTime = DateTime.UtcNow.AddHours(1)
        };

        _factory.MockReminderService
            .Setup(s => s.SnoozeAsync(
                TestAuthHandler.TestUserId,
                invalidId,
                It.IsAny<DateTime>()))
            .ThrowsAsync(new ReminderNotFoundException(invalidId));

        // Act
        var response = await _client.PutAsJsonAsync($"/api/v1/reminders/{invalidId}", snoozeDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        errorResponse!.Error.Code.Should().Be("REMINDER_NOT_FOUND");
    }

    [Fact]
    public async Task PUT_SnoozeReminder_PastTime_Returns400()
    {
        // Arrange (AC: 4)
        var reminderId = "reminder-past-time";
        var pastTime = DateTime.UtcNow.AddHours(-1);

        _factory.MockReminderService
            .Setup(s => s.SnoozeAsync(
                TestAuthHandler.TestUserId,
                reminderId,
                It.IsAny<DateTime>()))
            .ThrowsAsync(new InvalidScheduledTimeException("Scheduled time must be in the future"));

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/v1/reminders/{reminderId}",
            new { scheduledTime = pastTime.ToString("o") });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        errorResponse!.Error.Code.Should().Be("INVALID_SCHEDULED_TIME");
    }

    [Fact]
    public async Task PUT_SnoozeReminder_DismissedReminder_Returns400()
    {
        // Arrange (AC: 5)
        var reminderId = "dismissed-reminder";
        var snoozeDto = new SnoozeReminderDto
        {
            ScheduledTime = DateTime.UtcNow.AddHours(1)
        };

        _factory.MockReminderService
            .Setup(s => s.SnoozeAsync(
                TestAuthHandler.TestUserId,
                reminderId,
                It.IsAny<DateTime>()))
            .ThrowsAsync(new ReminderAlreadyDismissedException(reminderId));

        // Act
        var response = await _client.PutAsJsonAsync($"/api/v1/reminders/{reminderId}", snoozeDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        errorResponse!.Error.Code.Should().Be("REMINDER_ALREADY_DISMISSED");
    }

    [Fact]
    public async Task PUT_SnoozeReminder_AnotherUsersReminder_Returns404()
    {
        // Arrange (AC: 6 - user isolation)
        var reminderId = "another-users-reminder";
        var snoozeDto = new SnoozeReminderDto
        {
            ScheduledTime = DateTime.UtcNow.AddHours(1)
        };

        // The service throws ReminderNotFoundException because the reminder
        // doesn't exist for the authenticated user (user isolation)
        _factory.MockReminderService
            .Setup(s => s.SnoozeAsync(
                TestAuthHandler.TestUserId,
                reminderId,
                It.IsAny<DateTime>()))
            .ThrowsAsync(new ReminderNotFoundException(reminderId));

        // Act
        var response = await _client.PutAsJsonAsync($"/api/v1/reminders/{reminderId}", snoozeDto);

        // Assert - 404 because user B can't see user A's reminder
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PUT_SnoozeReminder_ActiveReminderToFuture_Succeeds()
    {
        // Arrange (AC: 2 - snoozing an active/overdue reminder)
        var reminderId = "active-reminder";
        var futureTime = DateTime.UtcNow.AddHours(3);

        var updatedReminder = new TaskReminderDocument
        {
            Id = reminderId,
            UserId = TestAuthHandler.TestUserId,
            TaskId = "task-active",
            TaskName = "Overdue Task",
            ScheduledTime = futureTime, // Moved to future
            IsDismissed = false,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        _factory.MockReminderService
            .Setup(s => s.SnoozeAsync(
                TestAuthHandler.TestUserId,
                reminderId,
                It.IsAny<DateTime>()))
            .ReturnsAsync(updatedReminder);

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/v1/reminders/{reminderId}",
            new { scheduledTime = futureTime.ToString("o") });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var reminderDto = await response.Content.ReadFromJsonAsync<TaskReminderDto>();
        reminderDto!.ScheduledTime.Should().BeCloseTo(futureTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task PUT_SnoozeReminder_WithoutAuthentication_Returns401()
    {
        // Arrange
        _client.DefaultRequestHeaders.Add("X-Test-Unauthenticated", "true");
        var snoozeDto = new SnoozeReminderDto
        {
            ScheduledTime = DateTime.UtcNow.AddHours(1)
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/v1/reminders/any-reminder", snoozeDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion
}
