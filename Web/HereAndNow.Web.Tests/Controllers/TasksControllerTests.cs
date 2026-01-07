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

public class TasksControllerTests
{
    private readonly Mock<ITaskService> _mockTaskService;
    private readonly Mock<ILogger<TasksController>> _mockLogger;
    private readonly TasksController _controller;
    private const string TestUserId = "auth0|test-user-123";

    public TasksControllerTests()
    {
        _mockTaskService = new Mock<ITaskService>();
        _mockLogger = new Mock<ILogger<TasksController>>();
        _controller = new TasksController(_mockTaskService.Object, _mockLogger.Object);

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

    #region CreateTask Tests

    [Fact]
    public async Task CreateTask_WithValidName_Returns201Created()
    {
        // Arrange
        var createDto = new CreateTaskDto { Name = "Test Task" };
        var createdTask = new TaskDocument
        {
            Id = "task-123",
            UserId = TestUserId,
            Name = "Test Task",
            State = TaskState.OnDeck,
            CreatedAt = DateTime.UtcNow
        };

        _mockTaskService
            .Setup(s => s.CreateTaskWithOptionalReminderAsync("Test Task", TestUserId, null))
            .ReturnsAsync(createdTask);

        // Act
        var result = await _controller.CreateTask(createDto);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.StatusCode.Should().Be(StatusCodes.Status201Created);
        var taskDto = createdResult.Value.Should().BeOfType<TaskDto>().Subject;
        taskDto.Id.Should().Be("task-123");
        taskDto.Name.Should().Be("Test Task");
        taskDto.State.Should().Be(TaskState.OnDeck);
    }

    [Fact]
    public async Task CreateTask_SetsStateToOnDeck()
    {
        // Arrange
        var createDto = new CreateTaskDto { Name = "New Task" };
        var createdTask = new TaskDocument
        {
            Id = "task-456",
            UserId = TestUserId,
            Name = "New Task",
            State = TaskState.OnDeck,
            CreatedAt = DateTime.UtcNow
        };

        _mockTaskService
            .Setup(s => s.CreateTaskWithOptionalReminderAsync("New Task", TestUserId, null))
            .ReturnsAsync(createdTask);

        // Act
        var result = await _controller.CreateTask(createDto);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var taskDto = createdResult.Value.Should().BeOfType<TaskDto>().Subject;
        taskDto.State.Should().Be(TaskState.OnDeck);
    }

    #endregion

    #region GetTasks Tests

    [Fact]
    public async Task GetTasks_WithNoFilter_ReturnsAllUserTasks()
    {
        // Arrange
        var tasks = new List<TaskDocument>
        {
            new TaskDocument { Id = "task-1", UserId = TestUserId, Name = "Task 1", State = TaskState.OnDeck },
            new TaskDocument { Id = "task-2", UserId = TestUserId, Name = "Task 2", State = TaskState.InProgress }
        };

        var pagedResult = new PagedResult<TaskDocument>
        {
            Items = tasks,
            TotalCount = 2,
            HasMore = false
        };

        _mockTaskService
            .Setup(s => s.GetTasksPagedAsync(TestUserId, null, "createdAt", "asc", 0, 50))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetTasks();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var pagedDto = okResult.Value.Should().BeOfType<PagedTasksDto>().Subject;
        pagedDto.Items.Should().HaveCount(2);
        pagedDto.TotalCount.Should().Be(2);
        pagedDto.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task GetTasks_WithStateFilter_ReturnsFilteredTasks()
    {
        // Arrange
        var tasks = new List<TaskDocument>
        {
            new TaskDocument { Id = "task-1", UserId = TestUserId, Name = "Task 1", State = TaskState.OnDeck }
        };

        var pagedResult = new PagedResult<TaskDocument>
        {
            Items = tasks,
            TotalCount = 1,
            HasMore = false
        };

        _mockTaskService
            .Setup(s => s.GetTasksPagedAsync(TestUserId, TaskState.OnDeck, "createdAt", "asc", 0, 50))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetTasks(TaskState.OnDeck);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var pagedDto = okResult.Value.Should().BeOfType<PagedTasksDto>().Subject;
        pagedDto.Items.Should().HaveCount(1);
        pagedDto.Items.First().State.Should().Be(TaskState.OnDeck);
    }

    [Fact]
    public async Task GetTasks_WithInvalidState_Returns400BadRequest()
    {
        // Arrange
        _mockTaskService
            .Setup(s => s.GetTasksPagedAsync(TestUserId, "InvalidState", "createdAt", "asc", 0, 50))
            .ThrowsAsync(new ArgumentException("Invalid task state: InvalidState"));

        // Act
        var result = await _controller.GetTasks("InvalidState");

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task GetTasks_WithPagination_ReturnsPagedResults()
    {
        // Arrange
        var tasks = new List<TaskDocument>
        {
            new TaskDocument { Id = "task-51", UserId = TestUserId, Name = "Task 51", State = TaskState.OnDeck }
        };

        var pagedResult = new PagedResult<TaskDocument>
        {
            Items = tasks,
            TotalCount = 60,
            HasMore = false
        };

        _mockTaskService
            .Setup(s => s.GetTasksPagedAsync(TestUserId, null, "createdAt", "asc", 50, 50))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetTasks(null, "createdAt", "asc", 50, 50);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var pagedDto = okResult.Value.Should().BeOfType<PagedTasksDto>().Subject;
        pagedDto.Items.Should().HaveCount(1);
        pagedDto.TotalCount.Should().Be(60);
        pagedDto.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task GetTasks_WithSorting_CallsServiceWithCorrectParams()
    {
        // Arrange
        var tasks = new List<TaskDocument>
        {
            new TaskDocument { Id = "task-1", UserId = TestUserId, Name = "Task 1", State = TaskState.Completed, CompletedAt = DateTime.UtcNow }
        };

        var pagedResult = new PagedResult<TaskDocument>
        {
            Items = tasks,
            TotalCount = 1,
            HasMore = false
        };

        _mockTaskService
            .Setup(s => s.GetTasksPagedAsync(TestUserId, TaskState.Completed, "completedAt", "desc", 0, 50))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetTasks(TaskState.Completed, "completedAt", "desc");

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var pagedDto = okResult.Value.Should().BeOfType<PagedTasksDto>().Subject;
        pagedDto.Items.Should().HaveCount(1);
        _mockTaskService.Verify(s => s.GetTasksPagedAsync(TestUserId, TaskState.Completed, "completedAt", "desc", 0, 50), Times.Once);
    }

    #endregion

    #region GetTask Tests

    [Fact]
    public async Task GetTask_WithValidId_ReturnsTask()
    {
        // Arrange
        var task = new TaskDocument
        {
            Id = "task-123",
            UserId = TestUserId,
            Name = "Test Task",
            State = TaskState.OnDeck
        };

        _mockTaskService
            .Setup(s => s.GetTaskByIdAsync("task-123", TestUserId))
            .ReturnsAsync(task);

        // Act
        var result = await _controller.GetTask("task-123");

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var taskDto = okResult.Value.Should().BeOfType<TaskDto>().Subject;
        taskDto.Id.Should().Be("task-123");
    }

    [Fact]
    public async Task GetTask_WithInvalidId_Returns404NotFound()
    {
        // Arrange
        _mockTaskService
            .Setup(s => s.GetTaskByIdAsync("nonexistent", TestUserId))
            .ThrowsAsync(new TaskNotFoundException("nonexistent"));

        // Act
        var result = await _controller.GetTask("nonexistent");

        // Assert
        var notFoundResult = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var errorResponse = notFoundResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("TASK_NOT_FOUND");
    }

    #endregion

    #region UpdateTask Tests

    [Fact]
    public async Task UpdateTask_WithValidInput_ReturnsOkWithUpdatedTask()
    {
        // Arrange
        var updateDto = new UpdateTaskDto { State = TaskState.InProgress };
        var updatedTask = new TaskDocument
        {
            Id = "task-123",
            UserId = TestUserId,
            Name = "My Task",
            State = TaskState.InProgress,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        _mockTaskService
            .Setup(s => s.UpdateTaskAsync("task-123", TestUserId, null, TaskState.InProgress))
            .ReturnsAsync(updatedTask);

        // Act
        var result = await _controller.UpdateTask("task-123", updateDto);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var taskDto = okResult.Value.Should().BeOfType<TaskDto>().Subject;
        taskDto.Id.Should().Be("task-123");
        taskDto.State.Should().Be(TaskState.InProgress);
    }

    [Fact]
    public async Task UpdateTask_WithNameOnly_ReturnsOkWithUpdatedTask()
    {
        // Arrange
        var updateDto = new UpdateTaskDto { Name = "Updated Name" };
        var updatedTask = new TaskDocument
        {
            Id = "task-123",
            UserId = TestUserId,
            Name = "Updated Name",
            State = TaskState.OnDeck,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        _mockTaskService
            .Setup(s => s.UpdateTaskAsync("task-123", TestUserId, "Updated Name", null))
            .ReturnsAsync(updatedTask);

        // Act
        var result = await _controller.UpdateTask("task-123", updateDto);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var taskDto = okResult.Value.Should().BeOfType<TaskDto>().Subject;
        taskDto.Name.Should().Be("Updated Name");
    }

    [Fact]
    public async Task UpdateTask_TransitionToCompleted_ReturnsTaskWithCompletedAt()
    {
        // Arrange
        var updateDto = new UpdateTaskDto { State = TaskState.Completed };
        var completedAt = DateTime.UtcNow;
        var updatedTask = new TaskDocument
        {
            Id = "task-123",
            UserId = TestUserId,
            Name = "My Task",
            State = TaskState.Completed,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            CompletedAt = completedAt
        };

        _mockTaskService
            .Setup(s => s.UpdateTaskAsync("task-123", TestUserId, null, TaskState.Completed))
            .ReturnsAsync(updatedTask);

        // Act
        var result = await _controller.UpdateTask("task-123", updateDto);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var taskDto = okResult.Value.Should().BeOfType<TaskDto>().Subject;
        taskDto.State.Should().Be(TaskState.Completed);
        taskDto.CompletedAt.Should().Be(completedAt);
    }

    [Fact]
    public async Task UpdateTask_WithNonExistentId_Returns404NotFound()
    {
        // Arrange
        var updateDto = new UpdateTaskDto { State = TaskState.InProgress };

        _mockTaskService
            .Setup(s => s.UpdateTaskAsync("nonexistent", TestUserId, null, TaskState.InProgress))
            .ThrowsAsync(new TaskNotFoundException("nonexistent"));

        // Act
        var result = await _controller.UpdateTask("nonexistent", updateDto);

        // Assert
        var notFoundResult = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var errorResponse = notFoundResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("TASK_NOT_FOUND");
    }

    [Fact]
    public async Task UpdateTask_WithInvalidState_Returns400BadRequest()
    {
        // Arrange
        var updateDto = new UpdateTaskDto { State = "InvalidState" };

        // Act
        var result = await _controller.UpdateTask("task-123", updateDto);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task UpdateTask_WithEmptyDto_Returns400BadRequest()
    {
        // Arrange - no fields set
        var updateDto = new UpdateTaskDto();

        // Act
        var result = await _controller.UpdateTask("task-123", updateDto);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("VALIDATION_ERROR");
        errorResponse.Error.Message.Should().Contain("At least one field");
    }

    #endregion
}
