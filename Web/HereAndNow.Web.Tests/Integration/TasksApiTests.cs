using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using HereAndNow.Web.Tests.Helpers;
using HereAndNowService.DTOs;
using HereAndNowService.Models;
using HereAndNowService.Models.Exceptions;
using Moq;

namespace HereAndNow.Web.Tests.Integration;

public class TasksApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public TasksApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    #region Authentication Tests (AC: #3)

    [Fact]
    public async Task GetTasks_WithoutAuthentication_Returns401Unauthorized()
    {
        // Arrange
        _client.DefaultRequestHeaders.Add("X-Test-Unauthenticated", "true");

        // Act
        var response = await _client.GetAsync("/api/v1/tasks");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateTask_WithoutAuthentication_Returns401Unauthorized()
    {
        // Arrange
        _client.DefaultRequestHeaders.Add("X-Test-Unauthenticated", "true");
        var createDto = new CreateTaskDto { Name = "Test Task" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/tasks", createDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Validation Error Format Tests

    [Fact]
    public async Task CreateTask_WithEmptyName_ReturnsStandardErrorFormat()
    {
        // Arrange - send request with empty name that triggers DataAnnotations validation
        var createDto = new CreateTaskDto { Name = "" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/tasks", createDto);

        // Assert - should return project-standard error format, NOT ProblemDetails
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        errorResponse.Should().NotBeNull();
        errorResponse!.Error.Should().NotBeNull();
        errorResponse.Error.Code.Should().Be("VALIDATION_ERROR");
        errorResponse.Error.Message.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Create Task Tests (AC: #1)

    [Fact]
    public async Task CreateTask_WithValidRequest_Returns201Created()
    {
        // Arrange
        var createDto = new CreateTaskDto { Name = "My Task" };
        var createdTask = new TaskDocument
        {
            Id = Guid.NewGuid().ToString(),
            UserId = TestAuthHandler.TestUserId,
            Name = "My Task",
            State = TaskState.OnDeck,
            CreatedAt = DateTime.UtcNow
        };

        _factory.MockTaskService
            .Setup(s => s.CreateTaskAsync("My Task", TestAuthHandler.TestUserId))
            .ReturnsAsync(createdTask);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/tasks", createDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var taskDto = await response.Content.ReadFromJsonAsync<TaskDto>();
        taskDto.Should().NotBeNull();
        taskDto!.Name.Should().Be("My Task");
        taskDto.State.Should().Be(TaskState.OnDeck);
    }

    [Fact]
    public async Task CreateTask_SetsStateToOnDeck()
    {
        // Arrange
        var createDto = new CreateTaskDto { Name = "New Task" };
        var createdTask = new TaskDocument
        {
            Id = Guid.NewGuid().ToString(),
            UserId = TestAuthHandler.TestUserId,
            Name = "New Task",
            State = TaskState.OnDeck,
            CreatedAt = DateTime.UtcNow
        };

        _factory.MockTaskService
            .Setup(s => s.CreateTaskAsync("New Task", TestAuthHandler.TestUserId))
            .ReturnsAsync(createdTask);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/tasks", createDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var taskDto = await response.Content.ReadFromJsonAsync<TaskDto>();
        taskDto!.State.Should().Be(TaskState.OnDeck);
    }

    [Fact]
    public async Task CreateTask_ReturnsTaskWithIdAndCreatedAt()
    {
        // Arrange
        var createDto = new CreateTaskDto { Name = "Test Task" };
        var createdTask = new TaskDocument
        {
            Id = "abc123-def456",
            UserId = TestAuthHandler.TestUserId,
            Name = "Test Task",
            State = TaskState.OnDeck,
            CreatedAt = new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc)
        };

        _factory.MockTaskService
            .Setup(s => s.CreateTaskAsync("Test Task", TestAuthHandler.TestUserId))
            .ReturnsAsync(createdTask);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/tasks", createDto);

        // Assert
        var taskDto = await response.Content.ReadFromJsonAsync<TaskDto>();
        taskDto!.Id.Should().Be("abc123-def456");
        taskDto.CreatedAt.Should().Be(new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc));
    }

    #endregion

    #region Get Tasks Tests (AC: #2)

    [Fact]
    public async Task GetTasks_WithAuthentication_Returns200Ok()
    {
        // Arrange
        var tasks = new List<TaskDocument>
        {
            new TaskDocument { Id = "task-1", UserId = TestAuthHandler.TestUserId, Name = "Task 1", State = TaskState.OnDeck },
            new TaskDocument { Id = "task-2", UserId = TestAuthHandler.TestUserId, Name = "Task 2", State = TaskState.InProgress }
        };

        _factory.MockTaskService
            .Setup(s => s.GetTasksAsync(TestAuthHandler.TestUserId, null))
            .ReturnsAsync(tasks);

        // Act
        var response = await _client.GetAsync("/api/v1/tasks");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var taskDtos = await response.Content.ReadFromJsonAsync<List<TaskDto>>();
        taskDtos.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetTasks_WithStateFilter_ReturnsFilteredTasks()
    {
        // Arrange
        var tasks = new List<TaskDocument>
        {
            new TaskDocument { Id = "task-1", UserId = TestAuthHandler.TestUserId, Name = "Task 1", State = TaskState.OnDeck }
        };

        _factory.MockTaskService
            .Setup(s => s.GetTasksAsync(TestAuthHandler.TestUserId, TaskState.OnDeck))
            .ReturnsAsync(tasks);

        // Act
        var response = await _client.GetAsync("/api/v1/tasks?state=OnDeck");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var taskDtos = await response.Content.ReadFromJsonAsync<List<TaskDto>>();
        taskDtos.Should().HaveCount(1);
        taskDtos!.First().State.Should().Be(TaskState.OnDeck);
    }

    #endregion

    #region User Isolation Tests (AC: #2)

    [Fact]
    public async Task GetTasks_ReturnsOnlyCurrentUsersTasks()
    {
        // This test verifies that the controller passes the correct user ID to the service
        // The service is responsible for filtering by user ID

        // Arrange
        var currentUserTasks = new List<TaskDocument>
        {
            new TaskDocument { Id = "task-isolation-test", UserId = TestAuthHandler.TestUserId, Name = "Isolated Task" }
        };

        _factory.MockTaskService
            .Setup(s => s.GetTasksAsync(TestAuthHandler.TestUserId, null))
            .ReturnsAsync(currentUserTasks);

        // Act
        var response = await _client.GetAsync("/api/v1/tasks");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify the service was called with the correct user ID (at least once due to shared factory)
        _factory.MockTaskService.Verify(
            s => s.GetTasksAsync(TestAuthHandler.TestUserId, null),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task CreateTask_UsesAuthenticatedUserId()
    {
        // Arrange
        var createDto = new CreateTaskDto { Name = "User's Task" };
        var createdTask = new TaskDocument
        {
            Id = "new-task",
            UserId = TestAuthHandler.TestUserId,
            Name = "User's Task",
            State = TaskState.OnDeck,
            CreatedAt = DateTime.UtcNow
        };

        _factory.MockTaskService
            .Setup(s => s.CreateTaskAsync("User's Task", TestAuthHandler.TestUserId))
            .ReturnsAsync(createdTask);

        // Act
        await _client.PostAsJsonAsync("/api/v1/tasks", createDto);

        // Assert - verify service was called with correct user ID from auth
        _factory.MockTaskService.Verify(
            s => s.CreateTaskAsync("User's Task", TestAuthHandler.TestUserId),
            Times.Once);
    }

    #endregion

    #region Update Task Tests (AC: 1-5)

    [Fact]
    public async Task UpdateTask_WithValidState_Returns200Ok()
    {
        // Arrange
        var updateDto = new UpdateTaskDto { State = TaskState.InProgress };
        var updatedTask = new TaskDocument
        {
            Id = "task-123",
            UserId = TestAuthHandler.TestUserId,
            Name = "My Task",
            State = TaskState.InProgress,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        _factory.MockTaskService
            .Setup(s => s.UpdateTaskAsync("task-123", TestAuthHandler.TestUserId, null, TaskState.InProgress))
            .ReturnsAsync(updatedTask);

        // Act
        var response = await _client.PutAsJsonAsync("/api/v1/tasks/task-123", updateDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var taskDto = await response.Content.ReadFromJsonAsync<TaskDto>();
        taskDto!.State.Should().Be(TaskState.InProgress);
    }

    [Fact]
    public async Task UpdateTask_TransitionToCompleted_SetsCompletedAt()
    {
        // Arrange (AC: 2)
        var updateDto = new UpdateTaskDto { State = TaskState.Completed };
        var completedAt = DateTime.UtcNow;
        var updatedTask = new TaskDocument
        {
            Id = "task-123",
            UserId = TestAuthHandler.TestUserId,
            Name = "My Task",
            State = TaskState.Completed,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            CompletedAt = completedAt
        };

        _factory.MockTaskService
            .Setup(s => s.UpdateTaskAsync("task-123", TestAuthHandler.TestUserId, null, TaskState.Completed))
            .ReturnsAsync(updatedTask);

        // Act
        var response = await _client.PutAsJsonAsync("/api/v1/tasks/task-123", updateDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var taskDto = await response.Content.ReadFromJsonAsync<TaskDto>();
        taskDto!.State.Should().Be(TaskState.Completed);
        taskDto.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateTask_TransitionFromCompleted_ClearsCompletedAt()
    {
        // Arrange (AC: 3)
        var updateDto = new UpdateTaskDto { State = TaskState.OnDeck };
        var updatedTask = new TaskDocument
        {
            Id = "task-123",
            UserId = TestAuthHandler.TestUserId,
            Name = "My Task",
            State = TaskState.OnDeck,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            CompletedAt = null // cleared
        };

        _factory.MockTaskService
            .Setup(s => s.UpdateTaskAsync("task-123", TestAuthHandler.TestUserId, null, TaskState.OnDeck))
            .ReturnsAsync(updatedTask);

        // Act
        var response = await _client.PutAsJsonAsync("/api/v1/tasks/task-123", updateDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var taskDto = await response.Content.ReadFromJsonAsync<TaskDto>();
        taskDto!.State.Should().Be(TaskState.OnDeck);
        taskDto.CompletedAt.Should().BeNull();
    }

    [Fact]
    public async Task UpdateTask_WithNonExistentId_Returns404()
    {
        // Arrange (AC: 4)
        var updateDto = new UpdateTaskDto { State = TaskState.InProgress };

        _factory.MockTaskService
            .Setup(s => s.UpdateTaskAsync("invalid-id", TestAuthHandler.TestUserId, null, TaskState.InProgress))
            .ThrowsAsync(new TaskNotFoundException("invalid-id"));

        // Act
        var response = await _client.PutAsJsonAsync("/api/v1/tasks/invalid-id", updateDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        errorResponse!.Error.Code.Should().Be("TASK_NOT_FOUND");
    }

    [Fact]
    public async Task UpdateTask_WithInvalidState_Returns400()
    {
        // Arrange
        var updateDto = new UpdateTaskDto { State = "InvalidState" };

        // Act
        var response = await _client.PutAsJsonAsync("/api/v1/tasks/task-123", updateDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        errorResponse!.Error.Code.Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task UpdateTask_WithoutAuthentication_Returns401()
    {
        // Arrange
        _client.DefaultRequestHeaders.Add("X-Test-Unauthenticated", "true");
        var updateDto = new UpdateTaskDto { State = TaskState.InProgress };

        // Act
        var response = await _client.PutAsJsonAsync("/api/v1/tasks/task-123", updateDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateTask_OtherUsersTask_Returns404()
    {
        // Arrange (AC: 5 - user isolation)
        var updateDto = new UpdateTaskDto { State = TaskState.InProgress };

        // Simulate that the task belongs to a different user by having service throw TaskNotFoundException
        _factory.MockTaskService
            .Setup(s => s.UpdateTaskAsync("other-users-task", TestAuthHandler.TestUserId, null, TaskState.InProgress))
            .ThrowsAsync(new TaskNotFoundException("other-users-task"));

        // Act
        var response = await _client.PutAsJsonAsync("/api/v1/tasks/other-users-task", updateDto);

        // Assert - 404 not 403, to avoid information leakage
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateTask_PartialUpdateNameOnly_Returns200()
    {
        // Arrange
        var updateDto = new UpdateTaskDto { Name = "Updated Name" };
        var updatedTask = new TaskDocument
        {
            Id = "task-123",
            UserId = TestAuthHandler.TestUserId,
            Name = "Updated Name",
            State = TaskState.OnDeck,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        _factory.MockTaskService
            .Setup(s => s.UpdateTaskAsync("task-123", TestAuthHandler.TestUserId, "Updated Name", null))
            .ReturnsAsync(updatedTask);

        // Act
        var response = await _client.PutAsJsonAsync("/api/v1/tasks/task-123", updateDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var taskDto = await response.Content.ReadFromJsonAsync<TaskDto>();
        taskDto!.Name.Should().Be("Updated Name");
    }

    [Fact]
    public async Task UpdateTask_FullUpdate_Returns200()
    {
        // Arrange
        var updateDto = new UpdateTaskDto { Name = "New Name", State = TaskState.InProgress };
        var updatedTask = new TaskDocument
        {
            Id = "task-123",
            UserId = TestAuthHandler.TestUserId,
            Name = "New Name",
            State = TaskState.InProgress,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        _factory.MockTaskService
            .Setup(s => s.UpdateTaskAsync("task-123", TestAuthHandler.TestUserId, "New Name", TaskState.InProgress))
            .ReturnsAsync(updatedTask);

        // Act
        var response = await _client.PutAsJsonAsync("/api/v1/tasks/task-123", updateDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var taskDto = await response.Content.ReadFromJsonAsync<TaskDto>();
        taskDto!.Name.Should().Be("New Name");
        taskDto.State.Should().Be(TaskState.InProgress);
    }

    #endregion
}
