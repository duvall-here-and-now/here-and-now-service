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
    private readonly Mock<ILogger<CommandsController>> _mockLogger;
    private readonly CommandsController _controller;
    private const string TestUserId = "auth0|test-user-123";

    public CommandsControllerTests()
    {
        _mockTaskService = new Mock<ITaskService>();
        _mockLogger = new Mock<ILogger<CommandsController>>();
        _controller = new CommandsController(_mockTaskService.Object, _mockLogger.Object);

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
