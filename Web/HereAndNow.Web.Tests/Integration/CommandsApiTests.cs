using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using HereAndNow.Web.Tests.Helpers;
using HereAndNowService.DTOs;
using HereAndNowService.Models;
using HereAndNowService.Models.Exceptions;
using Moq;

namespace HereAndNow.Web.Tests.Integration;

public class CommandsApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public CommandsApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    #region Authentication Tests (AC: #6)

    [Fact]
    public async Task ExecuteCommand_WithoutAuthentication_Returns401Unauthorized()
    {
        // Arrange (AC: #6)
        _client.DefaultRequestHeaders.Add("X-Test-Unauthenticated", "true");
        var request = CreateCommandRequest("CreateTask", new
        {
            taskId = Guid.NewGuid().ToString(),
            name = "Unauthenticated Task"
        });

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region CreateTask Command Tests (AC: #1)

    [Fact]
    public async Task CreateTask_WithValidRequest_Returns201Created()
    {
        // Arrange (AC: #1)
        var taskId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("CreateTask", new { taskId, name = "My Command Task" });

        var createdTask = new TaskDocument
        {
            Id = taskId,
            UserId = TestAuthHandler.TestUserId,
            Name = "My Command Task",
            State = TaskState.OnDeck,
            CreatedAt = DateTime.UtcNow
        };

        _factory.MockTaskService
            .Setup(s => s.CreateTaskWithIdAsync(TestAuthHandler.TestUserId, taskId, "My Command Task"))
            .ReturnsAsync(createdTask);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var taskDto = await response.Content.ReadFromJsonAsync<TaskDto>();
        taskDto.Should().NotBeNull();
        taskDto!.Id.Should().Be(taskId);
        taskDto.Name.Should().Be("My Command Task");
        taskDto.State.Should().Be(TaskState.OnDeck);
    }

    [Fact]
    public async Task CreateTask_SetsStateToOnDeck()
    {
        // Arrange (AC: #1 - task is created with state "OnDeck")
        var taskId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("CreateTask", new { taskId, name = "OnDeck Test Task" });

        var createdTask = new TaskDocument
        {
            Id = taskId,
            UserId = TestAuthHandler.TestUserId,
            Name = "OnDeck Test Task",
            State = TaskState.OnDeck,
            CreatedAt = DateTime.UtcNow
        };

        _factory.MockTaskService
            .Setup(s => s.CreateTaskWithIdAsync(TestAuthHandler.TestUserId, taskId, "OnDeck Test Task"))
            .ReturnsAsync(createdTask);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var taskDto = await response.Content.ReadFromJsonAsync<TaskDto>();
        taskDto!.State.Should().Be(TaskState.OnDeck);
    }

    [Fact]
    public async Task CreateTask_ReturnsResponseWithAllRequiredFields()
    {
        // Arrange (AC: #1 - response includes id, name, state, createdAt)
        var taskId = Guid.NewGuid().ToString();
        var createdAt = new DateTime(2026, 1, 17, 10, 0, 0, DateTimeKind.Utc);
        var request = CreateCommandRequest("CreateTask", new { taskId, name = "Response Fields Task" });

        var createdTask = new TaskDocument
        {
            Id = taskId,
            UserId = TestAuthHandler.TestUserId,
            Name = "Response Fields Task",
            State = TaskState.OnDeck,
            CreatedAt = createdAt,
            LastModifiedAt = createdAt
        };

        _factory.MockTaskService
            .Setup(s => s.CreateTaskWithIdAsync(TestAuthHandler.TestUserId, taskId, "Response Fields Task"))
            .ReturnsAsync(createdTask);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        var taskDto = await response.Content.ReadFromJsonAsync<TaskDto>();
        taskDto!.Id.Should().Be(taskId);
        taskDto.Name.Should().Be("Response Fields Task");
        taskDto.State.Should().Be(TaskState.OnDeck);
        taskDto.CreatedAt.Should().Be(createdAt);
    }

    #endregion

    #region Duplicate TaskId Tests (AC: #2)

    [Fact]
    public async Task CreateTask_WithExistingId_Returns409Conflict()
    {
        // Arrange (AC: #2)
        var taskId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("CreateTask", new { taskId, name = "Duplicate Task" });

        _factory.MockTaskService
            .Setup(s => s.CreateTaskWithIdAsync(TestAuthHandler.TestUserId, taskId, "Duplicate Task"))
            .ThrowsAsync(new TaskAlreadyExistsException(taskId));

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        errorResponse.Should().NotBeNull();
        errorResponse!.Error.Code.Should().Be("TASK_ALREADY_EXISTS");
    }

    #endregion

    #region Missing TaskId Tests (AC: #3)

    [Fact]
    public async Task CreateTask_WithMissingTaskId_Returns400BadRequest()
    {
        // Arrange (AC: #3)
        var request = CreateCommandRequest("CreateTask", new { name = "Task Without ID" });

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        errorResponse.Should().NotBeNull();
        errorResponse!.Error.Code.Should().Be("VALIDATION_ERROR");
        errorResponse.Error.Message.Should().Contain("taskId");
    }

    #endregion

    #region Invalid GUID Format Tests (AC: #4)

    [Fact]
    public async Task CreateTask_WithInvalidGuidFormat_Returns400BadRequest()
    {
        // Arrange (AC: #4)
        var request = CreateCommandRequest("CreateTask", new { taskId = "not-a-guid", name = "Invalid GUID Task" });

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        errorResponse.Should().NotBeNull();
        errorResponse!.Error.Code.Should().Be("VALIDATION_ERROR");
        errorResponse.Error.Message.Should().Contain("GUID");
    }

    [Fact]
    public async Task CreateTask_WithPartialGuid_Returns400BadRequest()
    {
        // Arrange (AC: #4 - various invalid GUID formats)
        var request = CreateCommandRequest("CreateTask", new { taskId = "550e8400-e29b-41d4", name = "Partial GUID Task" });

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        errorResponse!.Error.Code.Should().Be("VALIDATION_ERROR");
    }

    #endregion

    #region Unknown Command Tests (AC: #5)

    [Fact]
    public async Task UnknownCommand_Returns400BadRequest()
    {
        // Arrange (AC: #5)
        var request = CreateCommandRequest("DoSomethingRandom", new { foo = "bar" });

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        errorResponse.Should().NotBeNull();
        errorResponse!.Error.Code.Should().Be("UNKNOWN_COMMAND");
        errorResponse.Error.Message.Should().Contain("DoSomethingRandom");
    }

    #endregion

    #region User Isolation Tests

    [Fact]
    public async Task CreateTask_UsesAuthenticatedUserId()
    {
        // Arrange
        var taskId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("CreateTask", new { taskId, name = "User Isolation Task" });

        var createdTask = new TaskDocument
        {
            Id = taskId,
            UserId = TestAuthHandler.TestUserId,
            Name = "User Isolation Task",
            State = TaskState.OnDeck,
            CreatedAt = DateTime.UtcNow
        };

        _factory.MockTaskService
            .Setup(s => s.CreateTaskWithIdAsync(TestAuthHandler.TestUserId, taskId, "User Isolation Task"))
            .ReturnsAsync(createdTask);

        // Act
        await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert - verify service was called with correct user ID from auth
        _factory.MockTaskService.Verify(
            s => s.CreateTaskWithIdAsync(TestAuthHandler.TestUserId, taskId, "User Isolation Task"),
            Times.Once);
    }

    #endregion

    #region End-to-End Flow Tests

    [Fact]
    public async Task CreateTask_EndToEndFlow_CreatesTaskSuccessfully()
    {
        // Arrange (AC: #1, #2, #6 combined - full end-to-end test)
        var taskId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("CreateTask", new { taskId, name = "E2E Test Task" });

        var createdTask = new TaskDocument
        {
            Id = taskId,
            UserId = TestAuthHandler.TestUserId,
            Name = "E2E Test Task",
            State = TaskState.OnDeck,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = null,
            ReminderId = null,
            LastModifiedAt = DateTime.UtcNow
        };

        _factory.MockTaskService
            .Setup(s => s.CreateTaskWithIdAsync(TestAuthHandler.TestUserId, taskId, "E2E Test Task"))
            .ReturnsAsync(createdTask);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain(taskId);

        var taskDto = await response.Content.ReadFromJsonAsync<TaskDto>();
        taskDto.Should().NotBeNull();
        taskDto!.Id.Should().Be(taskId);
        taskDto.Name.Should().Be("E2E Test Task");
        taskDto.State.Should().Be(TaskState.OnDeck);
        taskDto.CompletedAt.Should().BeNull();
        taskDto.ReminderId.Should().BeNull();
    }

    #endregion

    #region CreateTaskAndTaskReminder Command Tests (AC: #1-#7)

    [Fact]
    public async Task CreateTaskAndTaskReminder_WithValidRequest_Returns201Created()
    {
        // Arrange (AC: #1)
        var taskId = Guid.NewGuid().ToString();
        var reminderId = Guid.NewGuid().ToString();
        var scheduledTime = DateTime.UtcNow.AddDays(1);
        var request = CreateCommandRequest("CreateTaskAndTaskReminder", new
        {
            taskId,
            taskReminderId = reminderId,
            name = "Call dentist",
            scheduledTime
        });

        var now = DateTime.UtcNow;
        var createdTask = new TaskDocument
        {
            Id = taskId,
            UserId = TestAuthHandler.TestUserId,
            Name = "Call dentist",
            State = TaskState.OnDeck,
            CreatedAt = now,
            ReminderId = reminderId,
            LastModifiedAt = now
        };
        var createdReminder = new TaskReminderDocument
        {
            Id = reminderId,
            UserId = TestAuthHandler.TestUserId,
            TaskId = taskId,
            TaskName = "Call dentist",
            ScheduledTime = scheduledTime,
            IsDismissed = false,
            CreatedAt = now,
            LastModifiedAt = now
        };

        _factory.MockTaskService
            .Setup(s => s.CreateTaskWithReminderAsync(
                TestAuthHandler.TestUserId,
                It.IsAny<string>(),
                It.IsAny<string>(),
                "Call dentist",
                scheduledTime))
            .ReturnsAsync((createdTask, createdReminder));

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var responseDto = await response.Content.ReadFromJsonAsync<TaskAndReminderDto>();
        responseDto.Should().NotBeNull();
        responseDto!.Task.Id.Should().Be(taskId);
        responseDto.Task.Name.Should().Be("Call dentist");
        responseDto.Task.ReminderId.Should().Be(reminderId);
        responseDto.Reminder.Id.Should().Be(reminderId);
        responseDto.Reminder.TaskId.Should().Be(taskId);
        responseDto.Reminder.TaskName.Should().Be("Call dentist");
        responseDto.Reminder.IsDismissed.Should().BeFalse();
    }

    [Fact]
    public async Task CreateTaskAndTaskReminder_ResponseHasBidirectionalLinks()
    {
        // Arrange (AC: #1 - verify bidirectional links)
        var taskId = Guid.NewGuid().ToString();
        var reminderId = Guid.NewGuid().ToString();
        var scheduledTime = DateTime.UtcNow.AddDays(1);
        var request = CreateCommandRequest("CreateTaskAndTaskReminder", new
        {
            taskId,
            taskReminderId = reminderId,
            name = "Bidirectional Test",
            scheduledTime
        });

        var now = DateTime.UtcNow;
        var createdTask = new TaskDocument
        {
            Id = taskId,
            UserId = TestAuthHandler.TestUserId,
            Name = "Bidirectional Test",
            State = TaskState.OnDeck,
            CreatedAt = now,
            ReminderId = reminderId,
            LastModifiedAt = now
        };
        var createdReminder = new TaskReminderDocument
        {
            Id = reminderId,
            UserId = TestAuthHandler.TestUserId,
            TaskId = taskId,
            TaskName = "Bidirectional Test",
            ScheduledTime = scheduledTime,
            IsDismissed = false,
            CreatedAt = now,
            LastModifiedAt = now
        };

        _factory.MockTaskService
            .Setup(s => s.CreateTaskWithReminderAsync(
                TestAuthHandler.TestUserId,
                It.IsAny<string>(),
                It.IsAny<string>(),
                "Bidirectional Test",
                scheduledTime))
            .ReturnsAsync((createdTask, createdReminder));

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        var responseDto = await response.Content.ReadFromJsonAsync<TaskAndReminderDto>();

        // Task references Reminder
        responseDto!.Task.ReminderId.Should().Be(responseDto.Reminder.Id);
        // Reminder references Task
        responseDto.Reminder.TaskId.Should().Be(responseDto.Task.Id);
        // TaskName is denormalized
        responseDto.Reminder.TaskName.Should().Be(responseDto.Task.Name);
    }

    [Fact]
    public async Task CreateTaskAndTaskReminder_WithExistingTaskId_Returns409Conflict()
    {
        // Arrange (AC: #2)
        var taskId = Guid.NewGuid().ToString();
        var reminderId = Guid.NewGuid().ToString();
        var scheduledTime = DateTime.UtcNow.AddDays(1);
        var request = CreateCommandRequest("CreateTaskAndTaskReminder", new
        {
            taskId,
            taskReminderId = reminderId,
            name = "Duplicate Task",
            scheduledTime
        });

        _factory.MockTaskService
            .Setup(s => s.CreateTaskWithReminderAsync(
                TestAuthHandler.TestUserId,
                It.IsAny<string>(),
                It.IsAny<string>(),
                "Duplicate Task",
                scheduledTime))
            .ThrowsAsync(new TaskAlreadyExistsException(taskId));

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        errorResponse!.Error.Code.Should().Be("TASK_ALREADY_EXISTS");
    }

    [Fact]
    public async Task CreateTaskAndTaskReminder_WithExistingReminderId_Returns409Conflict()
    {
        // Arrange (AC: #2)
        var taskId = Guid.NewGuid().ToString();
        var reminderId = Guid.NewGuid().ToString();
        var scheduledTime = DateTime.UtcNow.AddDays(1);
        var request = CreateCommandRequest("CreateTaskAndTaskReminder", new
        {
            taskId,
            taskReminderId = reminderId,
            name = "Duplicate Reminder",
            scheduledTime
        });

        _factory.MockTaskService
            .Setup(s => s.CreateTaskWithReminderAsync(
                TestAuthHandler.TestUserId,
                It.IsAny<string>(),
                It.IsAny<string>(),
                "Duplicate Reminder",
                scheduledTime))
            .ThrowsAsync(new TaskReminderAlreadyExistsException(reminderId));

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        errorResponse!.Error.Code.Should().Be("TASK_REMINDER_ALREADY_EXISTS");
    }

    [Fact]
    public async Task CreateTaskAndTaskReminder_WithPastScheduledTime_Returns201Created()
    {
        // Arrange - past times are now accepted to support delayed sync scenarios
        var taskId = Guid.NewGuid().ToString();
        var reminderId = Guid.NewGuid().ToString();
        var pastTime = DateTime.UtcNow.AddHours(-1);
        var request = CreateCommandRequest("CreateTaskAndTaskReminder", new
        {
            taskId,
            taskReminderId = reminderId,
            name = "Past Time Task",
            scheduledTime = pastTime
        });

        var now = DateTime.UtcNow;
        var createdTask = new TaskDocument
        {
            Id = taskId,
            UserId = TestAuthHandler.TestUserId,
            Name = "Past Time Task",
            State = TaskState.OnDeck,
            CreatedAt = now,
            ReminderId = reminderId,
            LastModifiedAt = now
        };
        var createdReminder = new TaskReminderDocument
        {
            Id = reminderId,
            UserId = TestAuthHandler.TestUserId,
            TaskId = taskId,
            TaskName = "Past Time Task",
            ScheduledTime = pastTime,
            IsDismissed = false,
            CreatedAt = now,
            LastModifiedAt = now
        };

        _factory.MockTaskService
            .Setup(s => s.CreateTaskWithReminderAsync(
                TestAuthHandler.TestUserId,
                It.IsAny<string>(),
                It.IsAny<string>(),
                "Past Time Task",
                pastTime))
            .ReturnsAsync((createdTask, createdReminder));

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var responseDto = await response.Content.ReadFromJsonAsync<TaskAndReminderDto>();
        responseDto.Should().NotBeNull();
        responseDto!.Task.Name.Should().Be("Past Time Task");
        responseDto.Reminder.ScheduledTime.Should().BeCloseTo(pastTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task CreateTaskAndTaskReminder_WithScheduledTimeExactlyNow_Returns201Created()
    {
        // Arrange - "exactly now" times are accepted for delayed sync scenarios
        var taskId = Guid.NewGuid().ToString();
        var reminderId = Guid.NewGuid().ToString();
        var exactlyNow = DateTime.UtcNow;
        var request = CreateCommandRequest("CreateTaskAndTaskReminder", new
        {
            taskId,
            taskReminderId = reminderId,
            name = "Boundary Test",
            scheduledTime = exactlyNow
        });

        var now = DateTime.UtcNow;
        var createdTask = new TaskDocument
        {
            Id = taskId,
            UserId = TestAuthHandler.TestUserId,
            Name = "Boundary Test",
            State = TaskState.OnDeck,
            CreatedAt = now,
            ReminderId = reminderId,
            LastModifiedAt = now
        };
        var createdReminder = new TaskReminderDocument
        {
            Id = reminderId,
            UserId = TestAuthHandler.TestUserId,
            TaskId = taskId,
            TaskName = "Boundary Test",
            ScheduledTime = exactlyNow,
            IsDismissed = false,
            CreatedAt = now,
            LastModifiedAt = now
        };

        _factory.MockTaskService
            .Setup(s => s.CreateTaskWithReminderAsync(
                TestAuthHandler.TestUserId,
                It.IsAny<string>(),
                It.IsAny<string>(),
                "Boundary Test",
                exactlyNow))
            .ReturnsAsync((createdTask, createdReminder));

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var responseDto = await response.Content.ReadFromJsonAsync<TaskAndReminderDto>();
        responseDto.Should().NotBeNull();
        responseDto!.Task.Name.Should().Be("Boundary Test");
    }

    [Fact]
    public async Task CreateTaskAndTaskReminder_WithMissingReminderId_Returns400BadRequest()
    {
        // Arrange (AC: #4)
        var taskId = Guid.NewGuid().ToString();
        var scheduledTime = DateTime.UtcNow.AddDays(1);
        var request = CreateCommandRequest("CreateTaskAndTaskReminder", new
        {
            taskId,
            name = "Missing Reminder ID",
            scheduledTime
        });

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        errorResponse!.Error.Code.Should().Be("VALIDATION_ERROR");
        errorResponse.Error.Message.Should().Contain("taskReminderId");
    }

    [Fact]
    public async Task CreateTaskAndTaskReminder_WithMissingTaskId_Returns400BadRequest()
    {
        // Arrange (AC: #5)
        var reminderId = Guid.NewGuid().ToString();
        var scheduledTime = DateTime.UtcNow.AddDays(1);
        var request = CreateCommandRequest("CreateTaskAndTaskReminder", new
        {
            taskReminderId = reminderId,
            name = "Missing Task ID",
            scheduledTime
        });

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        errorResponse!.Error.Code.Should().Be("VALIDATION_ERROR");
        errorResponse.Error.Message.Should().Contain("taskId");
    }

    [Fact]
    public async Task CreateTaskAndTaskReminder_WithInvalidTaskIdGuid_Returns400BadRequest()
    {
        // Arrange (AC: #6)
        var reminderId = Guid.NewGuid().ToString();
        var scheduledTime = DateTime.UtcNow.AddDays(1);
        var request = CreateCommandRequest("CreateTaskAndTaskReminder", new
        {
            taskId = "not-a-valid-guid",
            taskReminderId = reminderId,
            name = "Invalid Task GUID",
            scheduledTime
        });

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        errorResponse!.Error.Code.Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task CreateTaskAndTaskReminder_WithInvalidReminderIdGuid_Returns400BadRequest()
    {
        // Arrange (AC: #6)
        var taskId = Guid.NewGuid().ToString();
        var scheduledTime = DateTime.UtcNow.AddDays(1);
        var request = CreateCommandRequest("CreateTaskAndTaskReminder", new
        {
            taskId,
            taskReminderId = "not-a-valid-guid",
            name = "Invalid Reminder GUID",
            scheduledTime
        });

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        errorResponse!.Error.Code.Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task CreateTaskAndTaskReminder_WithEmptyName_Returns400BadRequest()
    {
        // Arrange (AC: #7)
        var taskId = Guid.NewGuid().ToString();
        var reminderId = Guid.NewGuid().ToString();
        var scheduledTime = DateTime.UtcNow.AddDays(1);
        var request = CreateCommandRequest("CreateTaskAndTaskReminder", new
        {
            taskId,
            taskReminderId = reminderId,
            name = "",
            scheduledTime
        });

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        errorResponse!.Error.Code.Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task CreateTaskAndTaskReminder_UsesAuthenticatedUserId()
    {
        // Arrange
        var taskId = Guid.NewGuid().ToString();
        var reminderId = Guid.NewGuid().ToString();
        var scheduledTime = DateTime.UtcNow.AddDays(1);
        var request = CreateCommandRequest("CreateTaskAndTaskReminder", new
        {
            taskId,
            taskReminderId = reminderId,
            name = "User Isolation Test",
            scheduledTime
        });

        var now = DateTime.UtcNow;
        var createdTask = new TaskDocument
        {
            Id = taskId,
            UserId = TestAuthHandler.TestUserId,
            Name = "User Isolation Test",
            State = TaskState.OnDeck,
            CreatedAt = now,
            ReminderId = reminderId,
            LastModifiedAt = now
        };
        var createdReminder = new TaskReminderDocument
        {
            Id = reminderId,
            UserId = TestAuthHandler.TestUserId,
            TaskId = taskId,
            TaskName = "User Isolation Test",
            ScheduledTime = scheduledTime,
            IsDismissed = false,
            CreatedAt = now,
            LastModifiedAt = now
        };

        _factory.MockTaskService
            .Setup(s => s.CreateTaskWithReminderAsync(
                TestAuthHandler.TestUserId,
                It.IsAny<string>(),
                It.IsAny<string>(),
                "User Isolation Test",
                scheduledTime))
            .ReturnsAsync((createdTask, createdReminder));

        // Act
        await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert - verify service was called with correct user ID from auth
        _factory.MockTaskService.Verify(
            s => s.CreateTaskWithReminderAsync(
                TestAuthHandler.TestUserId,
                It.IsAny<string>(),
                It.IsAny<string>(),
                "User Isolation Test",
                scheduledTime),
            Times.Once);
    }

    #endregion

    #region UpdateTaskName Command Tests (AC: #1-#7)

    [Fact]
    public async Task UpdateTaskName_WithValidRequest_Returns200OK()
    {
        // Arrange (AC: #1)
        var taskId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("UpdateTaskName", new { taskId, name = "Updated task name" });

        var updatedTask = new TaskDocument
        {
            Id = taskId,
            UserId = TestAuthHandler.TestUserId,
            Name = "Updated task name",
            State = TaskState.OnDeck,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            LastModifiedAt = DateTime.UtcNow
        };

        _factory.MockTaskService
            .Setup(s => s.UpdateTaskNameAsync(TestAuthHandler.TestUserId, taskId, "Updated task name"))
            .ReturnsAsync(updatedTask);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var taskDto = await response.Content.ReadFromJsonAsync<TaskDto>();
        taskDto.Should().NotBeNull();
        taskDto!.Id.Should().Be(taskId);
        taskDto.Name.Should().Be("Updated task name");
    }

    [Fact]
    public async Task UpdateTaskName_EndToEndFlow_UpdatesTaskSuccessfully()
    {
        // Arrange (AC: #1, #6 - task without reminder)
        var taskId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("UpdateTaskName", new { taskId, name = "E2E Updated Name" });

        var updatedTask = new TaskDocument
        {
            Id = taskId,
            UserId = TestAuthHandler.TestUserId,
            Name = "E2E Updated Name",
            State = TaskState.OnDeck,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            ReminderId = null,
            LastModifiedAt = DateTime.UtcNow
        };

        _factory.MockTaskService
            .Setup(s => s.UpdateTaskNameAsync(TestAuthHandler.TestUserId, taskId, "E2E Updated Name"))
            .ReturnsAsync(updatedTask);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var taskDto = await response.Content.ReadFromJsonAsync<TaskDto>();
        taskDto!.Id.Should().Be(taskId);
        taskDto.Name.Should().Be("E2E Updated Name");
        taskDto.ReminderId.Should().BeNull();
    }

    [Fact]
    public async Task UpdateTaskName_ForTaskWithReminder_UpdatesBothAtomically()
    {
        // Arrange (AC: #1 - denormalization sync verification)
        var taskId = Guid.NewGuid().ToString();
        var reminderId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("UpdateTaskName", new { taskId, name = "Synced name update" });

        var updatedTask = new TaskDocument
        {
            Id = taskId,
            UserId = TestAuthHandler.TestUserId,
            Name = "Synced name update",
            State = TaskState.OnDeck,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            ReminderId = reminderId,
            LastModifiedAt = DateTime.UtcNow
        };

        _factory.MockTaskService
            .Setup(s => s.UpdateTaskNameAsync(TestAuthHandler.TestUserId, taskId, "Synced name update"))
            .ReturnsAsync(updatedTask);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var taskDto = await response.Content.ReadFromJsonAsync<TaskDto>();
        taskDto!.Name.Should().Be("Synced name update");
        taskDto.ReminderId.Should().Be(reminderId);

        // Verify service was called (which handles the atomic update internally)
        _factory.MockTaskService.Verify(
            s => s.UpdateTaskNameAsync(TestAuthHandler.TestUserId, taskId, "Synced name update"),
            Times.Once);
    }

    [Fact]
    public async Task UpdateTaskName_ForNonExistentTask_Returns404NotFound()
    {
        // Arrange (AC: #4)
        var taskId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("UpdateTaskName", new { taskId, name = "Ghost task" });

        _factory.MockTaskService
            .Setup(s => s.UpdateTaskNameAsync(TestAuthHandler.TestUserId, taskId, "Ghost task"))
            .ThrowsAsync(new TaskNotFoundException(taskId));

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        errorResponse.Should().NotBeNull();
        errorResponse!.Error.Code.Should().Be("TASK_NOT_FOUND");
    }

    [Fact]
    public async Task UpdateTaskName_ForDeletedTask_Returns400BadRequest()
    {
        // Arrange (AC: #3)
        var taskId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("UpdateTaskName", new { taskId, name = "Update deleted" });

        _factory.MockTaskService
            .Setup(s => s.UpdateTaskNameAsync(TestAuthHandler.TestUserId, taskId, "Update deleted"))
            .ThrowsAsync(new InvalidStateTransitionException(
                taskId,
                TaskState.Deleted,
                "UpdateTaskName",
                "Deleted tasks cannot be modified"));

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        errorResponse.Should().NotBeNull();
        errorResponse!.Error.Code.Should().Be("INVALID_STATE_TRANSITION");
        errorResponse.Error.Message.Should().Contain("Deleted");
    }

    [Fact]
    public async Task UpdateTaskName_WithEmptyName_Returns400BadRequest()
    {
        // Arrange (AC: #2)
        var taskId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("UpdateTaskName", new { taskId, name = "" });

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        errorResponse!.Error.Code.Should().Be("VALIDATION_ERROR");
        errorResponse.Error.Message.Should().Contain("name");
    }

    [Fact]
    public async Task UpdateTaskName_WithInvalidGuidFormat_Returns400BadRequest()
    {
        // Arrange (AC: #7)
        var request = CreateCommandRequest("UpdateTaskName", new { taskId = "not-a-guid", name = "Invalid GUID" });

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        errorResponse!.Error.Code.Should().Be("VALIDATION_ERROR");
        errorResponse.Error.Message.Should().Contain("GUID");
    }

    [Fact]
    public async Task UpdateTaskName_UsesAuthenticatedUserId()
    {
        // Arrange (AC: #5 - user isolation)
        var taskId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("UpdateTaskName", new { taskId, name = "User Isolation Test" });

        var updatedTask = new TaskDocument
        {
            Id = taskId,
            UserId = TestAuthHandler.TestUserId,
            Name = "User Isolation Test",
            State = TaskState.OnDeck,
            CreatedAt = DateTime.UtcNow
        };

        _factory.MockTaskService
            .Setup(s => s.UpdateTaskNameAsync(TestAuthHandler.TestUserId, taskId, "User Isolation Test"))
            .ReturnsAsync(updatedTask);

        // Act
        await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert - verify service was called with correct user ID from auth
        _factory.MockTaskService.Verify(
            s => s.UpdateTaskNameAsync(TestAuthHandler.TestUserId, taskId, "User Isolation Test"),
            Times.Once);
    }

    #endregion

    #region UpdateTaskReminderScheduledTime Command Tests (AC: #1, #2, #7)

    [Fact]
    public async Task UpdateTaskReminderScheduledTime_EndToEndFlow_Returns200OK()
    {
        // Arrange (AC: #1)
        var reminderId = Guid.NewGuid().ToString();
        var taskId = Guid.NewGuid().ToString();
        var newScheduledTime = DateTime.UtcNow.AddDays(1);
        var request = CreateCommandRequest("UpdateTaskReminderScheduledTime", new
        {
            taskReminderId = reminderId,
            scheduledTime = newScheduledTime
        });

        var updatedReminder = new TaskReminderDocument
        {
            Id = reminderId,
            UserId = TestAuthHandler.TestUserId,
            TaskId = taskId,
            TaskName = "Call dentist",
            ScheduledTime = newScheduledTime,
            IsDismissed = false,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            LastModifiedAt = DateTime.UtcNow
        };

        _factory.MockReminderService
            .Setup(s => s.SnoozeAsync(TestAuthHandler.TestUserId, reminderId, newScheduledTime))
            .ReturnsAsync(updatedReminder);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var reminderDto = await response.Content.ReadFromJsonAsync<TaskReminderDto>();
        reminderDto.Should().NotBeNull();
        reminderDto!.Id.Should().Be(reminderId);
        reminderDto.ScheduledTime.Should().Be(newScheduledTime);
    }

    [Fact]
    public async Task UpdateTaskReminderScheduledTime_ForDismissedReminder_Returns400BadRequest()
    {
        // Arrange (AC: #2)
        var reminderId = Guid.NewGuid().ToString();
        var newScheduledTime = DateTime.UtcNow.AddDays(1);
        var request = CreateCommandRequest("UpdateTaskReminderScheduledTime", new
        {
            taskReminderId = reminderId,
            scheduledTime = newScheduledTime
        });

        _factory.MockReminderService
            .Setup(s => s.SnoozeAsync(TestAuthHandler.TestUserId, reminderId, newScheduledTime))
            .ThrowsAsync(new ReminderAlreadyDismissedException(reminderId));

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        errorResponse!.Error.Code.Should().Be("REMINDER_ALREADY_DISMISSED");
    }

    [Fact]
    public async Task UpdateTaskReminderScheduledTime_ForNonExistentReminder_Returns404NotFound()
    {
        // Arrange (AC: #7)
        var reminderId = Guid.NewGuid().ToString();
        var newScheduledTime = DateTime.UtcNow.AddDays(1);
        var request = CreateCommandRequest("UpdateTaskReminderScheduledTime", new
        {
            taskReminderId = reminderId,
            scheduledTime = newScheduledTime
        });

        _factory.MockReminderService
            .Setup(s => s.SnoozeAsync(TestAuthHandler.TestUserId, reminderId, newScheduledTime))
            .ThrowsAsync(new ReminderNotFoundException(reminderId));

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        errorResponse!.Error.Code.Should().Be("REMINDER_NOT_FOUND");
    }

    #endregion

    #region DismissTaskReminder Command Tests (AC: #5, #6, #7)

    [Fact]
    public async Task DismissTaskReminder_EndToEndFlow_Returns204NoContent()
    {
        // Arrange (AC: #5)
        var reminderId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("DismissTaskReminder", new
        {
            taskReminderId = reminderId
        });

        _factory.MockReminderService
            .Setup(s => s.DismissAsync(TestAuthHandler.TestUserId, reminderId))
            .Returns(Task.CompletedTask);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DismissTaskReminder_Idempotency_DismissTwiceReturns204BothTimes()
    {
        // Arrange (AC: #6 - idempotency test)
        var reminderId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("DismissTaskReminder", new
        {
            taskReminderId = reminderId
        });

        // Service is idempotent - always returns successfully
        _factory.MockReminderService
            .Setup(s => s.DismissAsync(TestAuthHandler.TestUserId, reminderId))
            .Returns(Task.CompletedTask);

        // Act - first dismiss
        var response1 = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Act - second dismiss (idempotent - should succeed)
        var response2 = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert - both should return 204 No Content
        response1.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response2.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify service was called twice
        _factory.MockReminderService.Verify(
            s => s.DismissAsync(TestAuthHandler.TestUserId, reminderId),
            Times.Exactly(2));
    }

    [Fact]
    public async Task DismissTaskReminder_ForNonExistentReminder_Returns404NotFound()
    {
        // Arrange (AC: #7)
        var reminderId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("DismissTaskReminder", new
        {
            taskReminderId = reminderId
        });

        _factory.MockReminderService
            .Setup(s => s.DismissAsync(TestAuthHandler.TestUserId, reminderId))
            .ThrowsAsync(new ReminderNotFoundException(reminderId));

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        errorResponse!.Error.Code.Should().Be("REMINDER_NOT_FOUND");
    }

    #endregion

    #region Helper Methods

    private static object CreateCommandRequest(string command, object payload)
    {
        return new
        {
            command,
            payload
        };
    }

    #endregion
}
