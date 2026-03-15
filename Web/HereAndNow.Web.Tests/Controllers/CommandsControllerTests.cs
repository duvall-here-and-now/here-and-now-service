using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using HereAndNowService.Commands;
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

public class CommandsControllerTests
{
    private readonly Mock<ITaskService> _mockTaskService;
    private readonly Mock<ITaskReminderService> _mockReminderService;
    private readonly Mock<IRecurringTaskService> _mockRecurringTaskService;
    private readonly Mock<ILogger<CommandsController>> _mockLogger;
    private readonly CommandsController _controller;
    private const string TestUserId = "auth0|test-user-123";

    public CommandsControllerTests()
    {
        _mockTaskService = new Mock<ITaskService>();
        _mockReminderService = new Mock<ITaskReminderService>();
        _mockRecurringTaskService = new Mock<IRecurringTaskService>();
        _mockLogger = new Mock<ILogger<CommandsController>>();
        _controller = new CommandsController(
            _mockTaskService.Object,
            _mockReminderService.Object,
            _mockRecurringTaskService.Object,
            _mockLogger.Object);

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

    #region CreateTask Command Tests (AC: #1, #2, #3, #4)

    [Fact]
    public async Task CreateTask_WithValidGuid_Returns201Created()
    {
        // Arrange (AC: #1)
        var taskId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("CreateTask", new { taskId, name = "My New Task" });

        var createdTask = new TaskDocument
        {
            Id = taskId,
            UserId = TestUserId,
            Name = "My New Task",
            State = TaskState.OnDeck,
            CreatedAt = DateTime.UtcNow
        };

        _mockTaskService
            .Setup(s => s.CreateTaskWithIdAsync(TestUserId, taskId, "My New Task"))
            .ReturnsAsync(createdTask);

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.StatusCode.Should().Be(StatusCodes.Status201Created);
        var taskDto = createdResult.Value.Should().BeOfType<TaskDto>().Subject;
        taskDto.Id.Should().Be(taskId);
        taskDto.Name.Should().Be("My New Task");
        taskDto.State.Should().Be(TaskState.OnDeck);
    }

    [Fact]
    public async Task CreateTask_SetsStateToOnDeck()
    {
        // Arrange (AC: #1 - task is created with state "OnDeck")
        var taskId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("CreateTask", new { taskId, name = "New Task" });

        var createdTask = new TaskDocument
        {
            Id = taskId,
            UserId = TestUserId,
            Name = "New Task",
            State = TaskState.OnDeck,
            CreatedAt = DateTime.UtcNow
        };

        _mockTaskService
            .Setup(s => s.CreateTaskWithIdAsync(TestUserId, taskId, "New Task"))
            .ReturnsAsync(createdTask);

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var taskDto = createdResult.Value.Should().BeOfType<TaskDto>().Subject;
        taskDto.State.Should().Be(TaskState.OnDeck);
    }

    [Fact]
    public async Task CreateTask_WithExistingId_Returns409Conflict()
    {
        // Arrange (AC: #2)
        var taskId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("CreateTask", new { taskId, name = "Duplicate Task" });

        _mockTaskService
            .Setup(s => s.CreateTaskWithIdAsync(TestUserId, taskId, "Duplicate Task"))
            .ThrowsAsync(new TaskAlreadyExistsException(taskId));

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var conflictResult = result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflictResult.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        var errorResponse = conflictResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("TASK_ALREADY_EXISTS");
        errorResponse.Error.Message.Should().Contain(taskId);
    }

    [Fact]
    public async Task CreateTask_WithMissingTaskId_Returns400BadRequest()
    {
        // Arrange (AC: #3)
        var request = CreateCommandRequest("CreateTask", new { name = "Task Without ID" });

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("VALIDATION_ERROR");
        errorResponse.Error.Message.Should().Contain("taskId");
    }

    [Fact]
    public async Task CreateTask_WithEmptyTaskId_Returns400BadRequest()
    {
        // Arrange (AC: #3)
        var request = CreateCommandRequest("CreateTask", new { taskId = "", name = "Task With Empty ID" });

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("VALIDATION_ERROR");
        errorResponse.Error.Message.Should().Contain("taskId");
    }

    [Fact]
    public async Task CreateTask_WithInvalidGuidFormat_Returns400BadRequest()
    {
        // Arrange (AC: #4)
        var request = CreateCommandRequest("CreateTask", new { taskId = "not-a-guid", name = "Invalid GUID Task" });

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("VALIDATION_ERROR");
        errorResponse.Error.Message.Should().Contain("GUID");
    }

    [Fact]
    public async Task CreateTask_WithMissingName_Returns400BadRequest()
    {
        // Arrange
        var taskId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("CreateTask", new { taskId });

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("VALIDATION_ERROR");
        errorResponse.Error.Message.Should().Contain("name");
    }

    [Fact]
    public async Task CreateTask_WithEmptyName_Returns400BadRequest()
    {
        // Arrange
        var taskId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("CreateTask", new { taskId, name = "" });

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("VALIDATION_ERROR");
        errorResponse.Error.Message.Should().Contain("name");
    }

    [Fact]
    public async Task CreateTask_WithWhitespaceOnlyName_Returns400BadRequest()
    {
        // Arrange
        var taskId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("CreateTask", new { taskId, name = "   " });

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task CreateTask_NormalizesGuidToLowercase()
    {
        // Arrange - GUID with uppercase letters
        var uppercaseGuid = "550E8400-E29B-41D4-A716-446655440000";
        var normalizedGuid = uppercaseGuid.ToLowerInvariant();
        var request = CreateCommandRequest("CreateTask", new { taskId = uppercaseGuid, name = "Test Task" });

        var createdTask = new TaskDocument
        {
            Id = normalizedGuid,
            UserId = TestUserId,
            Name = "Test Task",
            State = TaskState.OnDeck,
            CreatedAt = DateTime.UtcNow
        };

        _mockTaskService
            .Setup(s => s.CreateTaskWithIdAsync(TestUserId, normalizedGuid, "Test Task"))
            .ReturnsAsync(createdTask);

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        _mockTaskService.Verify(
            s => s.CreateTaskWithIdAsync(TestUserId, normalizedGuid, "Test Task"),
            Times.Once);
    }

    [Fact]
    public async Task CreateTask_ReturnsTaskWithCreatedAt()
    {
        // Arrange (AC: #1 - response includes createdAt)
        var taskId = Guid.NewGuid().ToString();
        var createdAt = DateTime.UtcNow;
        var request = CreateCommandRequest("CreateTask", new { taskId, name = "Task With Timestamp" });

        var createdTask = new TaskDocument
        {
            Id = taskId,
            UserId = TestUserId,
            Name = "Task With Timestamp",
            State = TaskState.OnDeck,
            CreatedAt = createdAt
        };

        _mockTaskService
            .Setup(s => s.CreateTaskWithIdAsync(TestUserId, taskId, "Task With Timestamp"))
            .ReturnsAsync(createdTask);

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var taskDto = createdResult.Value.Should().BeOfType<TaskDto>().Subject;
        taskDto.CreatedAt.Should().BeCloseTo(createdAt, TimeSpan.FromSeconds(1));
    }

    #endregion

    #region Unknown Command Tests (AC: #5)

    [Fact]
    public async Task UnknownCommand_Returns400BadRequest()
    {
        // Arrange (AC: #5)
        var request = CreateCommandRequest("DoSomethingRandom", new { foo = "bar" });

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("UNKNOWN_COMMAND");
        errorResponse.Error.Message.Should().Contain("DoSomethingRandom");
    }

    [Fact]
    public async Task EmptyCommand_Returns400BadRequest()
    {
        // Arrange
        var request = CreateCommandRequest("", new { taskId = Guid.NewGuid().ToString() });

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("UNKNOWN_COMMAND");
    }

    [Fact]
    public async Task CaseSensitiveCommand_ReturnsUnknownForWrongCase()
    {
        // Arrange - command names are case-sensitive
        var taskId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("createtask", new { taskId, name = "Test" });

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("UNKNOWN_COMMAND");
    }

    #endregion

    #region Invalid Payload Tests

    [Fact]
    public async Task CreateTask_WithMalformedJson_Returns400BadRequest()
    {
        // Arrange - create a request with invalid JSON in payload
        var request = new CommandRequest
        {
            Command = "CreateTask",
            Payload = JsonDocument.Parse("{}").RootElement // Empty payload
        };

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("VALIDATION_ERROR");
    }

    #endregion

    #region User Isolation Tests

    [Fact]
    public async Task CreateTask_UsesAuthenticatedUserId()
    {
        // Arrange
        var taskId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("CreateTask", new { taskId, name = "User's Task" });

        var createdTask = new TaskDocument
        {
            Id = taskId,
            UserId = TestUserId,
            Name = "User's Task",
            State = TaskState.OnDeck,
            CreatedAt = DateTime.UtcNow
        };

        _mockTaskService
            .Setup(s => s.CreateTaskWithIdAsync(TestUserId, taskId, "User's Task"))
            .ReturnsAsync(createdTask);

        // Act
        await _controller.ExecuteCommand(request);

        // Assert - verify service was called with correct user ID from auth
        _mockTaskService.Verify(
            s => s.CreateTaskWithIdAsync(TestUserId, taskId, "User's Task"),
            Times.Once);
    }

    #endregion

    #region CreateTaskAndTaskReminder Command Tests (AC: #1-#7)

    [Fact]
    public async Task CreateTaskAndTaskReminder_WithValidData_Returns201Created()
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
            UserId = TestUserId,
            Name = "Call dentist",
            State = TaskState.OnDeck,
            CreatedAt = now,
            ReminderId = reminderId
        };
        var createdReminder = new TaskReminderDocument
        {
            Id = reminderId,
            UserId = TestUserId,
            TaskId = taskId,
            TaskName = "Call dentist",
            ScheduledTime = scheduledTime,
            IsDismissed = false,
            CreatedAt = now
        };

        _mockTaskService
            .Setup(s => s.CreateTaskWithReminderAsync(
                TestUserId,
                It.IsAny<string>(),
                It.IsAny<string>(),
                "Call dentist",
                scheduledTime))
            .ReturnsAsync((createdTask, createdReminder));

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.StatusCode.Should().Be(StatusCodes.Status201Created);
        var responseDto = createdResult.Value.Should().BeOfType<TaskAndReminderDto>().Subject;

        responseDto.Task.Id.Should().Be(taskId);
        responseDto.Task.Name.Should().Be("Call dentist");
        responseDto.Task.ReminderId.Should().Be(reminderId);
        responseDto.Reminder.Id.Should().Be(reminderId);
        responseDto.Reminder.TaskId.Should().Be(taskId);
        responseDto.Reminder.TaskName.Should().Be("Call dentist");
        responseDto.Reminder.IsDismissed.Should().BeFalse();
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

        _mockTaskService
            .Setup(s => s.CreateTaskWithReminderAsync(
                TestUserId,
                It.IsAny<string>(),
                It.IsAny<string>(),
                "Duplicate Task",
                scheduledTime))
            .ThrowsAsync(new TaskAlreadyExistsException(taskId));

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var conflictResult = result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflictResult.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        var errorResponse = conflictResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("TASK_ALREADY_EXISTS");
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

        _mockTaskService
            .Setup(s => s.CreateTaskWithReminderAsync(
                TestUserId,
                It.IsAny<string>(),
                It.IsAny<string>(),
                "Duplicate Reminder",
                scheduledTime))
            .ThrowsAsync(new TaskReminderAlreadyExistsException(reminderId));

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var conflictResult = result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflictResult.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        var errorResponse = conflictResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("TASK_REMINDER_ALREADY_EXISTS");
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
            name = "Past Task",
            scheduledTime = pastTime
        });

        var now = DateTime.UtcNow;
        var createdTask = new TaskDocument
        {
            Id = taskId,
            UserId = TestUserId,
            Name = "Past Task",
            State = TaskState.OnDeck,
            CreatedAt = now,
            ReminderId = reminderId
        };
        var createdReminder = new TaskReminderDocument
        {
            Id = reminderId,
            UserId = TestUserId,
            TaskId = taskId,
            TaskName = "Past Task",
            ScheduledTime = pastTime,
            IsDismissed = false,
            CreatedAt = now
        };

        _mockTaskService
            .Setup(s => s.CreateTaskWithReminderAsync(
                TestUserId,
                It.IsAny<string>(),
                It.IsAny<string>(),
                "Past Task",
                pastTime))
            .ReturnsAsync((createdTask, createdReminder));

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.StatusCode.Should().Be(StatusCodes.Status201Created);
        var responseDto = createdResult.Value.Should().BeOfType<TaskAndReminderDto>().Subject;
        responseDto.Task.Name.Should().Be("Past Task");
        responseDto.Reminder.ScheduledTime.Should().BeCloseTo(pastTime, TimeSpan.FromSeconds(1));
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
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("VALIDATION_ERROR");
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
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("VALIDATION_ERROR");
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
            name = "Invalid GUID",
            scheduledTime
        });

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("VALIDATION_ERROR");
        errorResponse.Error.Message.Should().Contain("taskId").And.Contain("GUID");
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
            name = "Invalid GUID",
            scheduledTime
        });

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("VALIDATION_ERROR");
        errorResponse.Error.Message.Should().Contain("taskReminderId").And.Contain("GUID");
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
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("VALIDATION_ERROR");
        errorResponse.Error.Message.Should().Contain("name");
    }

    [Fact]
    public async Task CreateTaskAndTaskReminder_WithWhitespaceOnlyName_Returns400BadRequest()
    {
        // Arrange (AC: #7)
        var taskId = Guid.NewGuid().ToString();
        var reminderId = Guid.NewGuid().ToString();
        var scheduledTime = DateTime.UtcNow.AddDays(1);
        var request = CreateCommandRequest("CreateTaskAndTaskReminder", new
        {
            taskId,
            taskReminderId = reminderId,
            name = "   ",
            scheduledTime
        });

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("VALIDATION_ERROR");
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
            UserId = TestUserId,
            Name = "Boundary Test",
            State = TaskState.OnDeck,
            CreatedAt = now,
            ReminderId = reminderId
        };
        var createdReminder = new TaskReminderDocument
        {
            Id = reminderId,
            UserId = TestUserId,
            TaskId = taskId,
            TaskName = "Boundary Test",
            ScheduledTime = exactlyNow,
            IsDismissed = false,
            CreatedAt = now
        };

        _mockTaskService
            .Setup(s => s.CreateTaskWithReminderAsync(
                TestUserId,
                It.IsAny<string>(),
                It.IsAny<string>(),
                "Boundary Test",
                exactlyNow))
            .ReturnsAsync((createdTask, createdReminder));

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.StatusCode.Should().Be(StatusCodes.Status201Created);
        var responseDto = createdResult.Value.Should().BeOfType<TaskAndReminderDto>().Subject;
        responseDto.Task.Name.Should().Be("Boundary Test");
    }

    [Fact]
    public async Task CreateTaskAndTaskReminder_NormalizesGuidsToLowercase()
    {
        // Arrange
        var uppercaseTaskId = "550E8400-E29B-41D4-A716-446655440000";
        var uppercaseReminderId = "660E8400-E29B-41D4-A716-446655440001";
        var normalizedTaskId = uppercaseTaskId.ToLowerInvariant();
        var normalizedReminderId = uppercaseReminderId.ToLowerInvariant();
        var scheduledTime = DateTime.UtcNow.AddDays(1);

        var request = CreateCommandRequest("CreateTaskAndTaskReminder", new
        {
            taskId = uppercaseTaskId,
            taskReminderId = uppercaseReminderId,
            name = "Normalize Test",
            scheduledTime
        });

        var now = DateTime.UtcNow;
        var createdTask = new TaskDocument
        {
            Id = normalizedTaskId,
            UserId = TestUserId,
            Name = "Normalize Test",
            State = TaskState.OnDeck,
            CreatedAt = now,
            ReminderId = normalizedReminderId
        };
        var createdReminder = new TaskReminderDocument
        {
            Id = normalizedReminderId,
            UserId = TestUserId,
            TaskId = normalizedTaskId,
            TaskName = "Normalize Test",
            ScheduledTime = scheduledTime,
            IsDismissed = false,
            CreatedAt = now
        };

        _mockTaskService
            .Setup(s => s.CreateTaskWithReminderAsync(
                TestUserId,
                normalizedTaskId,
                normalizedReminderId,
                "Normalize Test",
                scheduledTime))
            .ReturnsAsync((createdTask, createdReminder));

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        _mockTaskService.Verify(
            s => s.CreateTaskWithReminderAsync(
                TestUserId,
                normalizedTaskId,
                normalizedReminderId,
                "Normalize Test",
                scheduledTime),
            Times.Once);
    }

    #endregion

    #region UpdateTaskName Command Tests (AC: #1-#7)

    [Fact]
    public async Task UpdateTaskName_WithValidData_Returns200OK()
    {
        // Arrange (AC: #1)
        var taskId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("UpdateTaskName", new { taskId, name = "Updated task name" });

        var updatedTask = new TaskDocument
        {
            Id = taskId,
            UserId = TestUserId,
            Name = "Updated task name",
            State = TaskState.OnDeck,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            LastModifiedAt = DateTime.UtcNow
        };

        _mockTaskService
            .Setup(s => s.UpdateTaskNameAsync(TestUserId, taskId, "Updated task name"))
            .ReturnsAsync(updatedTask);

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        var taskDto = okResult.Value.Should().BeOfType<TaskDto>().Subject;
        taskDto.Id.Should().Be(taskId);
        taskDto.Name.Should().Be("Updated task name");
    }

    [Fact]
    public async Task UpdateTaskName_ForTaskWithReminder_UpdatesBothAtomically()
    {
        // Arrange (AC: #1 - denormalization sync)
        var taskId = Guid.NewGuid().ToString();
        var reminderId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("UpdateTaskName", new { taskId, name = "Synced name" });

        var updatedTask = new TaskDocument
        {
            Id = taskId,
            UserId = TestUserId,
            Name = "Synced name",
            State = TaskState.OnDeck,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            ReminderId = reminderId,
            LastModifiedAt = DateTime.UtcNow
        };

        _mockTaskService
            .Setup(s => s.UpdateTaskNameAsync(TestUserId, taskId, "Synced name"))
            .ReturnsAsync(updatedTask);

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var taskDto = okResult.Value.Should().BeOfType<TaskDto>().Subject;
        taskDto.Name.Should().Be("Synced name");
        taskDto.ReminderId.Should().Be(reminderId);
        _mockTaskService.Verify(
            s => s.UpdateTaskNameAsync(TestUserId, taskId, "Synced name"),
            Times.Once);
    }

    [Fact]
    public async Task UpdateTaskName_ForTaskWithoutReminder_UpdatesOnlyTask()
    {
        // Arrange (AC: #6)
        var taskId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("UpdateTaskName", new { taskId, name = "No reminder task" });

        var updatedTask = new TaskDocument
        {
            Id = taskId,
            UserId = TestUserId,
            Name = "No reminder task",
            State = TaskState.OnDeck,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            ReminderId = null,
            LastModifiedAt = DateTime.UtcNow
        };

        _mockTaskService
            .Setup(s => s.UpdateTaskNameAsync(TestUserId, taskId, "No reminder task"))
            .ReturnsAsync(updatedTask);

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var taskDto = okResult.Value.Should().BeOfType<TaskDto>().Subject;
        taskDto.Name.Should().Be("No reminder task");
        taskDto.ReminderId.Should().BeNull();
    }

    [Fact]
    public async Task UpdateTaskName_WithEmptyName_Returns400BadRequest()
    {
        // Arrange (AC: #2)
        var taskId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("UpdateTaskName", new { taskId, name = "" });

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("VALIDATION_ERROR");
        errorResponse.Error.Message.Should().Contain("name");
    }

    [Fact]
    public async Task UpdateTaskName_WithWhitespaceOnlyName_Returns400BadRequest()
    {
        // Arrange (AC: #2)
        var taskId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("UpdateTaskName", new { taskId, name = "   " });

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task UpdateTaskName_ForDeletedTask_Returns400BadRequest()
    {
        // Arrange (AC: #3)
        var taskId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("UpdateTaskName", new { taskId, name = "Update deleted" });

        _mockTaskService
            .Setup(s => s.UpdateTaskNameAsync(TestUserId, taskId, "Update deleted"))
            .ThrowsAsync(new InvalidStateTransitionException(
                taskId,
                TaskState.Deleted,
                "UpdateTaskName",
                "Deleted tasks cannot be modified"));

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("INVALID_STATE_TRANSITION");
        errorResponse.Error.Message.Should().Contain("Deleted");
    }

    [Fact]
    public async Task UpdateTaskName_ForNonExistentTask_Returns404NotFound()
    {
        // Arrange (AC: #4)
        var taskId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("UpdateTaskName", new { taskId, name = "Ghost task" });

        _mockTaskService
            .Setup(s => s.UpdateTaskNameAsync(TestUserId, taskId, "Ghost task"))
            .ThrowsAsync(new TaskNotFoundException(taskId));

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        var errorResponse = notFoundResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("TASK_NOT_FOUND");
        errorResponse.Error.Message.Should().Contain(taskId);
    }

    [Fact]
    public async Task UpdateTaskName_ForOtherUsersTask_Returns404NotFound()
    {
        // Arrange (AC: #5 - service will throw TaskNotFoundException for user isolation)
        var taskId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("UpdateTaskName", new { taskId, name = "Other user's task" });

        _mockTaskService
            .Setup(s => s.UpdateTaskNameAsync(TestUserId, taskId, "Other user's task"))
            .ThrowsAsync(new TaskNotFoundException(taskId));

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var errorResponse = notFoundResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("TASK_NOT_FOUND");
        // No information leakage - same error as non-existent
    }

    [Fact]
    public async Task UpdateTaskName_WithInvalidGuidFormat_Returns400BadRequest()
    {
        // Arrange (AC: #7)
        var request = CreateCommandRequest("UpdateTaskName", new { taskId = "not-a-guid", name = "Invalid GUID" });

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("VALIDATION_ERROR");
        errorResponse.Error.Message.Should().Contain("GUID");
    }

    [Fact]
    public async Task UpdateTaskName_NormalizesGuidToLowercase()
    {
        // Arrange
        var uppercaseGuid = "550E8400-E29B-41D4-A716-446655440000";
        var normalizedGuid = uppercaseGuid.ToLowerInvariant();
        var request = CreateCommandRequest("UpdateTaskName", new { taskId = uppercaseGuid, name = "Normalize Test" });

        var updatedTask = new TaskDocument
        {
            Id = normalizedGuid,
            UserId = TestUserId,
            Name = "Normalize Test",
            State = TaskState.OnDeck,
            CreatedAt = DateTime.UtcNow
        };

        _mockTaskService
            .Setup(s => s.UpdateTaskNameAsync(TestUserId, normalizedGuid, "Normalize Test"))
            .ReturnsAsync(updatedTask);

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        _mockTaskService.Verify(
            s => s.UpdateTaskNameAsync(TestUserId, normalizedGuid, "Normalize Test"),
            Times.Once);
    }

    [Fact]
    public async Task UpdateTaskName_WithMissingTaskId_Returns400BadRequest()
    {
        // Arrange
        var request = CreateCommandRequest("UpdateTaskName", new { name = "Missing taskId" });

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("VALIDATION_ERROR");
        errorResponse.Error.Message.Should().Contain("taskId");
    }

    [Fact]
    public async Task UpdateTaskName_WithMissingName_Returns400BadRequest()
    {
        // Arrange
        var taskId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("UpdateTaskName", new { taskId });

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("VALIDATION_ERROR");
        errorResponse.Error.Message.Should().Contain("name");
    }

    [Fact]
    public async Task UpdateTaskName_TrimsWhitespaceFromName()
    {
        // Arrange - name with leading/trailing whitespace
        var taskId = Guid.NewGuid().ToString();
        var nameWithWhitespace = "  Trimmed Task Name  ";
        var expectedTrimmedName = "Trimmed Task Name";
        var request = CreateCommandRequest("UpdateTaskName", new { taskId, name = nameWithWhitespace });

        var updatedTask = new TaskDocument
        {
            Id = taskId,
            UserId = TestUserId,
            Name = expectedTrimmedName, // Service trims the name
            State = TaskState.OnDeck,
            CreatedAt = DateTime.UtcNow
        };

        _mockTaskService
            .Setup(s => s.UpdateTaskNameAsync(TestUserId, taskId, nameWithWhitespace))
            .ReturnsAsync(updatedTask);

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert - service is called with original name (trimming happens in service layer)
        _mockTaskService.Verify(
            s => s.UpdateTaskNameAsync(TestUserId, taskId, nameWithWhitespace),
            Times.Once);
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var taskDto = okResult.Value.Should().BeOfType<TaskDto>().Subject;
        taskDto.Name.Should().Be(expectedTrimmedName);
    }

    #endregion

    #region UpdateTaskReminderScheduledTime Command Tests (AC: #1-#4, #7-#9)

    [Fact]
    public async Task UpdateTaskReminderScheduledTime_WithValidData_Returns200OK()
    {
        // Arrange (AC: #1)
        var reminderId = Guid.NewGuid().ToString();
        var newScheduledTime = DateTime.UtcNow.AddDays(1);
        var request = CreateCommandRequest("UpdateTaskReminderScheduledTime", new
        {
            taskReminderId = reminderId,
            scheduledTime = newScheduledTime
        });

        var updatedReminder = new TaskReminderDocument
        {
            Id = reminderId,
            UserId = TestUserId,
            TaskId = Guid.NewGuid().ToString(),
            TaskName = "Call dentist",
            ScheduledTime = newScheduledTime,
            IsDismissed = false,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            LastModifiedAt = DateTime.UtcNow
        };

        _mockReminderService
            .Setup(s => s.SnoozeAsync(TestUserId, reminderId, newScheduledTime))
            .ReturnsAsync(updatedReminder);

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        var reminderDto = okResult.Value.Should().BeOfType<TaskReminderDto>().Subject;
        reminderDto.Id.Should().Be(reminderId);
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

        _mockReminderService
            .Setup(s => s.SnoozeAsync(TestUserId, reminderId, newScheduledTime))
            .ThrowsAsync(new ReminderAlreadyDismissedException(reminderId));

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("REMINDER_ALREADY_DISMISSED");
        errorResponse.Error.Message.Should().Contain(reminderId);
    }

    [Fact]
    public async Task UpdateTaskReminderScheduledTime_WithPastTime_Returns200OK_ForDelayedSync()
    {
        // Arrange - Past times are now allowed to support delayed sync scenarios
        // where the snooze was valid when performed but the command arrives after
        // the scheduled time has passed
        var reminderId = Guid.NewGuid().ToString();
        var pastTime = DateTime.UtcNow.AddHours(-1);
        var request = CreateCommandRequest("UpdateTaskReminderScheduledTime", new
        {
            taskReminderId = reminderId,
            scheduledTime = pastTime
        });

        var snoozedReminder = new TaskReminderDocument
        {
            Id = reminderId,
            UserId = TestUserId,
            TaskId = "task-123",
            TaskName = "Test Task",
            ScheduledTime = pastTime,
            IsDismissed = false,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            LastModifiedAt = DateTime.UtcNow
        };

        _mockReminderService
            .Setup(s => s.SnoozeAsync(TestUserId, reminderId.ToLowerInvariant(), pastTime))
            .ReturnsAsync(snoozedReminder);

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert - Past times are accepted for delayed sync
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task UpdateTaskReminderScheduledTime_WithInvalidGuid_Returns400BadRequest()
    {
        // Arrange (AC: #4)
        var newScheduledTime = DateTime.UtcNow.AddDays(1);
        var request = CreateCommandRequest("UpdateTaskReminderScheduledTime", new
        {
            taskReminderId = "not-a-valid-guid",
            scheduledTime = newScheduledTime
        });

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("VALIDATION_ERROR");
        errorResponse.Error.Message.Should().Contain("GUID");
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

        _mockReminderService
            .Setup(s => s.SnoozeAsync(TestUserId, reminderId, newScheduledTime))
            .ThrowsAsync(new ReminderNotFoundException(reminderId));

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        var errorResponse = notFoundResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("REMINDER_NOT_FOUND");
        errorResponse.Error.Message.Should().Contain(reminderId);
    }

    [Fact]
    public async Task UpdateTaskReminderScheduledTime_WithMissingReminderId_Returns400BadRequest()
    {
        // Arrange (AC: #9)
        var newScheduledTime = DateTime.UtcNow.AddDays(1);
        var request = CreateCommandRequest("UpdateTaskReminderScheduledTime", new
        {
            scheduledTime = newScheduledTime
        });

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("VALIDATION_ERROR");
        errorResponse.Error.Message.Should().Contain("taskReminderId");
    }

    [Fact]
    public async Task UpdateTaskReminderScheduledTime_NormalizesGuidToLowercase()
    {
        // Arrange
        var uppercaseGuid = "660E8400-E29B-41D4-A716-446655440001";
        var normalizedGuid = uppercaseGuid.ToLowerInvariant();
        var newScheduledTime = DateTime.UtcNow.AddDays(1);
        var request = CreateCommandRequest("UpdateTaskReminderScheduledTime", new
        {
            taskReminderId = uppercaseGuid,
            scheduledTime = newScheduledTime
        });

        var updatedReminder = new TaskReminderDocument
        {
            Id = normalizedGuid,
            UserId = TestUserId,
            TaskId = Guid.NewGuid().ToString(),
            TaskName = "Test Task",
            ScheduledTime = newScheduledTime,
            IsDismissed = false,
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow
        };

        _mockReminderService
            .Setup(s => s.SnoozeAsync(TestUserId, normalizedGuid, newScheduledTime))
            .ReturnsAsync(updatedReminder);

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        _mockReminderService.Verify(
            s => s.SnoozeAsync(TestUserId, normalizedGuid, newScheduledTime),
            Times.Once);
    }

    #endregion

    #region DismissTaskReminder Command Tests (AC: #5-#9)

    [Fact]
    public async Task DismissTaskReminder_WithValidData_Returns204NoContent()
    {
        // Arrange (AC: #5)
        var reminderId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("DismissTaskReminder", new
        {
            taskReminderId = reminderId
        });

        _mockReminderService
            .Setup(s => s.DismissAsync(TestUserId, reminderId))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        var noContentResult = result as NoContentResult;
        noContentResult!.StatusCode.Should().Be(StatusCodes.Status204NoContent);
    }

    [Fact]
    public async Task DismissTaskReminder_ForAlreadyDismissedReminder_Returns204NoContent_Idempotent()
    {
        // Arrange (AC: #6 - idempotent operation)
        var reminderId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("DismissTaskReminder", new
        {
            taskReminderId = reminderId
        });

        // Service is now idempotent - does not throw for already dismissed
        _mockReminderService
            .Setup(s => s.DismissAsync(TestUserId, reminderId))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        _mockReminderService.Verify(
            s => s.DismissAsync(TestUserId, reminderId),
            Times.Once);
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

        _mockReminderService
            .Setup(s => s.DismissAsync(TestUserId, reminderId))
            .ThrowsAsync(new ReminderNotFoundException(reminderId));

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        var errorResponse = notFoundResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("REMINDER_NOT_FOUND");
    }

    [Fact]
    public async Task DismissTaskReminder_WithInvalidGuid_Returns400BadRequest()
    {
        // Arrange
        var request = CreateCommandRequest("DismissTaskReminder", new
        {
            taskReminderId = "not-a-valid-guid"
        });

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("VALIDATION_ERROR");
        errorResponse.Error.Message.Should().Contain("GUID");
    }

    [Fact]
    public async Task DismissTaskReminder_WithMissingReminderId_Returns400BadRequest()
    {
        // Arrange (AC: #9)
        var request = CreateCommandRequest("DismissTaskReminder", new { });

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("VALIDATION_ERROR");
        errorResponse.Error.Message.Should().Contain("taskReminderId");
    }

    [Fact]
    public async Task DismissTaskReminder_NormalizesGuidToLowercase()
    {
        // Arrange
        var uppercaseGuid = "660E8400-E29B-41D4-A716-446655440001";
        var normalizedGuid = uppercaseGuid.ToLowerInvariant();
        var request = CreateCommandRequest("DismissTaskReminder", new
        {
            taskReminderId = uppercaseGuid
        });

        _mockReminderService
            .Setup(s => s.DismissAsync(TestUserId, normalizedGuid))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        _mockReminderService.Verify(
            s => s.DismissAsync(TestUserId, normalizedGuid),
            Times.Once);
    }

    #endregion

    #region UpdateTaskState Command Tests - Basic Transitions (AC: #1, #2, #9, #14, #15)

    [Fact]
    public async Task UpdateTaskState_OnDeckToInProgress_Returns200OK()
    {
        // Arrange (AC: #1)
        var taskId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("UpdateTaskState", new { taskId, state = "InProgress" });

        var updatedTask = new TaskDocument
        {
            Id = taskId,
            UserId = TestUserId,
            Name = "Test Task",
            State = TaskState.InProgress,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            LastModifiedAt = DateTime.UtcNow
        };

        _mockTaskService
            .Setup(s => s.UpdateStateAsync(TestUserId, taskId, "InProgress"))
            .ReturnsAsync(updatedTask);

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        var taskDto = okResult.Value.Should().BeOfType<TaskDto>().Subject;
        taskDto.Id.Should().Be(taskId);
        taskDto.State.Should().Be(TaskState.InProgress);
    }

    [Fact]
    public async Task UpdateTaskState_InProgressToOnDeck_Returns200OK()
    {
        // Arrange (AC: #2)
        var taskId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("UpdateTaskState", new { taskId, state = "OnDeck" });

        var updatedTask = new TaskDocument
        {
            Id = taskId,
            UserId = TestUserId,
            Name = "Test Task",
            State = TaskState.OnDeck,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            LastModifiedAt = DateTime.UtcNow
        };

        _mockTaskService
            .Setup(s => s.UpdateStateAsync(TestUserId, taskId, "OnDeck"))
            .ReturnsAsync(updatedTask);

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var taskDto = okResult.Value.Should().BeOfType<TaskDto>().Subject;
        taskDto.State.Should().Be(TaskState.OnDeck);
    }

    [Fact]
    public async Task UpdateTaskState_CompletedToOnDeck_ClearsCompletedAt()
    {
        // Arrange (AC: #9)
        var taskId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("UpdateTaskState", new { taskId, state = "OnDeck" });

        var updatedTask = new TaskDocument
        {
            Id = taskId,
            UserId = TestUserId,
            Name = "Reopened Task",
            State = TaskState.OnDeck,
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            CompletedAt = null, // Should be cleared
            LastModifiedAt = DateTime.UtcNow
        };

        _mockTaskService
            .Setup(s => s.UpdateStateAsync(TestUserId, taskId, "OnDeck"))
            .ReturnsAsync(updatedTask);

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var taskDto = okResult.Value.Should().BeOfType<TaskDto>().Subject;
        taskDto.State.Should().Be(TaskState.OnDeck);
        taskDto.CompletedAt.Should().BeNull();
    }

    [Fact]
    public async Task UpdateTaskState_WithInvalidGuidFormat_Returns400ValidationError()
    {
        // Arrange (AC: #14)
        var request = CreateCommandRequest("UpdateTaskState", new { taskId = "not-a-guid", state = "InProgress" });

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("VALIDATION_ERROR");
        errorResponse.Error.Message.Should().Contain("GUID");
    }

    [Fact]
    public async Task UpdateTaskState_WithMissingTaskId_Returns400ValidationError()
    {
        // Arrange (AC: #15)
        var request = CreateCommandRequest("UpdateTaskState", new { state = "InProgress" });

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("VALIDATION_ERROR");
        errorResponse.Error.Message.Should().Contain("taskId");
    }

    [Fact]
    public async Task UpdateTaskState_WithMissingState_Returns400ValidationError()
    {
        // Arrange (AC: #15)
        var taskId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("UpdateTaskState", new { taskId });

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("VALIDATION_ERROR");
        errorResponse.Error.Message.Should().Contain("state");
    }

    [Fact]
    public async Task UpdateTaskState_NormalizesGuidToLowercase()
    {
        // Arrange
        var uppercaseGuid = "550E8400-E29B-41D4-A716-446655440000";
        var normalizedGuid = uppercaseGuid.ToLowerInvariant();
        var request = CreateCommandRequest("UpdateTaskState", new { taskId = uppercaseGuid, state = "InProgress" });

        var updatedTask = new TaskDocument
        {
            Id = normalizedGuid,
            UserId = TestUserId,
            Name = "Test Task",
            State = TaskState.InProgress,
            CreatedAt = DateTime.UtcNow
        };

        _mockTaskService
            .Setup(s => s.UpdateStateAsync(TestUserId, normalizedGuid, "InProgress"))
            .ReturnsAsync(updatedTask);

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        _mockTaskService.Verify(
            s => s.UpdateStateAsync(TestUserId, normalizedGuid, "InProgress"),
            Times.Once);
    }

    #endregion

    #region UpdateTaskState Command Tests - Completed/Deleted (AC: #3, #5, #6, #8)

    [Fact]
    public async Task UpdateTaskState_ToCompleted_SetsCompletedAt()
    {
        // Arrange (AC: #3)
        var taskId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("UpdateTaskState", new { taskId, state = "Completed" });

        var now = DateTime.UtcNow;
        var updatedTask = new TaskDocument
        {
            Id = taskId,
            UserId = TestUserId,
            Name = "Completed Task",
            State = TaskState.Completed,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            CompletedAt = now,
            LastModifiedAt = now
        };

        _mockTaskService
            .Setup(s => s.UpdateStateAsync(TestUserId, taskId, "Completed"))
            .ReturnsAsync(updatedTask);

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var taskDto = okResult.Value.Should().BeOfType<TaskDto>().Subject;
        taskDto.State.Should().Be(TaskState.Completed);
        taskDto.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateTaskState_AlreadyCompleted_ReturnsSuccess_Idempotent()
    {
        // Arrange (AC: #5 - idempotent)
        var taskId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("UpdateTaskState", new { taskId, state = "Completed" });

        var existingTask = new TaskDocument
        {
            Id = taskId,
            UserId = TestUserId,
            Name = "Already Completed",
            State = TaskState.Completed,
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            CompletedAt = DateTime.UtcNow.AddHours(-1),
            LastModifiedAt = DateTime.UtcNow.AddHours(-1)
        };

        _mockTaskService
            .Setup(s => s.UpdateStateAsync(TestUserId, taskId, "Completed"))
            .ReturnsAsync(existingTask);

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task UpdateTaskState_ToDeleted_Returns200OK()
    {
        // Arrange (AC: #6)
        var taskId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("UpdateTaskState", new { taskId, state = "Deleted" });

        var updatedTask = new TaskDocument
        {
            Id = taskId,
            UserId = TestUserId,
            Name = "Deleted Task",
            State = TaskState.Deleted,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            LastModifiedAt = DateTime.UtcNow
        };

        _mockTaskService
            .Setup(s => s.UpdateStateAsync(TestUserId, taskId, "Deleted"))
            .ReturnsAsync(updatedTask);

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var taskDto = okResult.Value.Should().BeOfType<TaskDto>().Subject;
        taskDto.State.Should().Be(TaskState.Deleted);
    }

    [Fact]
    public async Task UpdateTaskState_AlreadyDeleted_ReturnsSuccess_Idempotent()
    {
        // Arrange (AC: #8 - idempotent)
        var taskId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("UpdateTaskState", new { taskId, state = "Deleted" });

        var existingTask = new TaskDocument
        {
            Id = taskId,
            UserId = TestUserId,
            Name = "Already Deleted",
            State = TaskState.Deleted,
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            LastModifiedAt = DateTime.UtcNow.AddHours(-1)
        };

        _mockTaskService
            .Setup(s => s.UpdateStateAsync(TestUserId, taskId, "Deleted"))
            .ReturnsAsync(existingTask);

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    #endregion

    #region UpdateTaskState Command Tests - Error Cases (AC: #10, #11, #12, #13)

    [Fact]
    public async Task UpdateTaskState_FromDeletedToOnDeck_Returns400InvalidTransition()
    {
        // Arrange (AC: #10)
        var taskId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("UpdateTaskState", new { taskId, state = "OnDeck" });

        _mockTaskService
            .Setup(s => s.UpdateStateAsync(TestUserId, taskId, "OnDeck"))
            .ThrowsAsync(new InvalidStateTransitionException(
                taskId,
                TaskState.Deleted,
                "UpdateState to OnDeck",
                "Cannot transition from 'Deleted' to 'OnDeck'"));

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("INVALID_STATE_TRANSITION");
        errorResponse.Error.Message.Should().Contain("Deleted");
    }

    [Fact]
    public async Task UpdateTaskState_FromDeletedToInProgress_Returns400InvalidTransition()
    {
        // Arrange (AC: #10)
        var taskId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("UpdateTaskState", new { taskId, state = "InProgress" });

        _mockTaskService
            .Setup(s => s.UpdateStateAsync(TestUserId, taskId, "InProgress"))
            .ThrowsAsync(new InvalidStateTransitionException(
                taskId,
                TaskState.Deleted,
                "UpdateState to InProgress",
                "Cannot transition from 'Deleted' to 'InProgress'"));

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("INVALID_STATE_TRANSITION");
    }

    [Fact]
    public async Task UpdateTaskState_FromDeletedToCompleted_Returns400InvalidTransition()
    {
        // Arrange (AC: #10)
        var taskId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("UpdateTaskState", new { taskId, state = "Completed" });

        _mockTaskService
            .Setup(s => s.UpdateStateAsync(TestUserId, taskId, "Completed"))
            .ThrowsAsync(new InvalidStateTransitionException(
                taskId,
                TaskState.Deleted,
                "UpdateState to Completed",
                "Cannot transition from 'Deleted' to 'Completed'"));

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("INVALID_STATE_TRANSITION");
    }

    [Theory]
    [InlineData("Invalid")]
    [InlineData("todo")]
    [InlineData("COMPLETED")]
    [InlineData("on_deck")]
    [InlineData("in-progress")]
    public async Task UpdateTaskState_WithInvalidStateValue_Returns400ValidationError(string invalidState)
    {
        // Arrange (AC: #11)
        var taskId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("UpdateTaskState", new { taskId, state = invalidState });

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("VALIDATION_ERROR");
        errorResponse.Error.Message.Should().Contain("OnDeck").And.Contain("InProgress").And.Contain("Completed").And.Contain("Deleted");
    }

    [Fact]
    public async Task UpdateTaskState_ForNonExistentTask_Returns404NotFound()
    {
        // Arrange (AC: #12)
        var taskId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("UpdateTaskState", new { taskId, state = "InProgress" });

        _mockTaskService
            .Setup(s => s.UpdateStateAsync(TestUserId, taskId, "InProgress"))
            .ThrowsAsync(new TaskNotFoundException(taskId));

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        var errorResponse = notFoundResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("TASK_NOT_FOUND");
        errorResponse.Error.Message.Should().Contain(taskId);
    }

    [Fact]
    public async Task UpdateTaskState_ForOtherUsersTask_Returns404NotFound()
    {
        // Arrange (AC: #13 - security - no information leakage)
        var taskId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("UpdateTaskState", new { taskId, state = "InProgress" });

        _mockTaskService
            .Setup(s => s.UpdateStateAsync(TestUserId, taskId, "InProgress"))
            .ThrowsAsync(new TaskNotFoundException(taskId));

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var errorResponse = notFoundResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("TASK_NOT_FOUND");
        // Same error as non-existent - no information leakage
    }

    #endregion

    #region UpdateTaskState Command Tests - Task-Reminder Unity (AC: #4, #7)

    [Fact]
    public async Task UpdateTaskState_ToCompleted_WithReminder_DismissesReminder()
    {
        // Arrange (AC: #4 - Unity: complete task with reminder dismisses reminder)
        var taskId = Guid.NewGuid().ToString();
        var reminderId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("UpdateTaskState", new { taskId, state = "Completed" });

        var now = DateTime.UtcNow;
        var updatedTask = new TaskDocument
        {
            Id = taskId,
            UserId = TestUserId,
            Name = "Task With Reminder",
            State = TaskState.Completed,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            CompletedAt = now,
            ReminderId = reminderId,
            LastModifiedAt = now
        };

        _mockTaskService
            .Setup(s => s.UpdateStateAsync(TestUserId, taskId, "Completed"))
            .ReturnsAsync(updatedTask);

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var taskDto = okResult.Value.Should().BeOfType<TaskDto>().Subject;
        taskDto.State.Should().Be(TaskState.Completed);
        // Service handles Unity internally - we just verify the call was made
        _mockTaskService.Verify(
            s => s.UpdateStateAsync(TestUserId, taskId, "Completed"),
            Times.Once);
    }

    [Fact]
    public async Task UpdateTaskState_ToDeleted_WithReminder_DismissesReminder()
    {
        // Arrange (AC: #7 - Unity: delete task with reminder dismisses reminder)
        var taskId = Guid.NewGuid().ToString();
        var reminderId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("UpdateTaskState", new { taskId, state = "Deleted" });

        var now = DateTime.UtcNow;
        var updatedTask = new TaskDocument
        {
            Id = taskId,
            UserId = TestUserId,
            Name = "Task With Reminder",
            State = TaskState.Deleted,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            ReminderId = reminderId,
            LastModifiedAt = now
        };

        _mockTaskService
            .Setup(s => s.UpdateStateAsync(TestUserId, taskId, "Deleted"))
            .ReturnsAsync(updatedTask);

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var taskDto = okResult.Value.Should().BeOfType<TaskDto>().Subject;
        taskDto.State.Should().Be(TaskState.Deleted);
        _mockTaskService.Verify(
            s => s.UpdateStateAsync(TestUserId, taskId, "Deleted"),
            Times.Once);
    }

    [Fact]
    public async Task UpdateTaskState_ToCompleted_WithoutReminder_Succeeds()
    {
        // Arrange (AC: #3 - no reminder case)
        var taskId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("UpdateTaskState", new { taskId, state = "Completed" });

        var now = DateTime.UtcNow;
        var updatedTask = new TaskDocument
        {
            Id = taskId,
            UserId = TestUserId,
            Name = "Task Without Reminder",
            State = TaskState.Completed,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            CompletedAt = now,
            ReminderId = null, // No reminder
            LastModifiedAt = now
        };

        _mockTaskService
            .Setup(s => s.UpdateStateAsync(TestUserId, taskId, "Completed"))
            .ReturnsAsync(updatedTask);

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var taskDto = okResult.Value.Should().BeOfType<TaskDto>().Subject;
        taskDto.State.Should().Be(TaskState.Completed);
        taskDto.ReminderId.Should().BeNull();
    }

    #endregion

    #region CreateRecurringTaskConfig Command Tests

    [Fact]
    public async Task CreateRecurringTaskConfig_WithExistingId_Returns409Conflict()
    {
        // Arrange
        var configId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("CreateRecurringTaskConfig", new
        {
            id = configId,
            text = "Duplicate Config",
            recurrenceRule = "FREQ=DAILY;BYHOUR=7;BYMINUTE=0;BYSECOND=0",
            startDateAndTime = DateTime.UtcNow
        });

        _mockRecurringTaskService
            .Setup(s => s.CreateConfigAsync(
                TestUserId, configId, "Duplicate Config",
                "FREQ=DAILY;BYHOUR=7;BYMINUTE=0;BYSECOND=0",
                It.IsAny<DateTime>()))
            .ThrowsAsync(new RecurringTaskConfigAlreadyExistsException(configId));

        // Act
        var result = await _controller.ExecuteCommand(request);

        // Assert
        var conflictResult = result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflictResult.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        var errorResponse = conflictResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("RECURRING_TASK_CONFIG_ALREADY_EXISTS");
        errorResponse.Error.Message.Should().Contain(configId);
    }

    #endregion

    #region Helper Methods

    private static CommandRequest CreateCommandRequest(string command, object payload)
    {
        var payloadJson = JsonSerializer.Serialize(payload);
        return new CommandRequest
        {
            Command = command,
            Payload = JsonDocument.Parse(payloadJson).RootElement
        };
    }

    #endregion
}
