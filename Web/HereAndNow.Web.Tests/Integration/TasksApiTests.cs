using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using HereAndNow.Web.Tests.Helpers;
using HereAndNowService.DTOs;
using HereAndNowService.Models;
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
}
