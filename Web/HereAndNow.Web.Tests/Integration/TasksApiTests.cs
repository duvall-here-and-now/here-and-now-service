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
            .Setup(s => s.CreateTaskWithOptionalReminderAsync("My Task", TestAuthHandler.TestUserId, null))
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
            .Setup(s => s.CreateTaskWithOptionalReminderAsync("New Task", TestAuthHandler.TestUserId, null))
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
            .Setup(s => s.CreateTaskWithOptionalReminderAsync("Test Task", TestAuthHandler.TestUserId, null))
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

        var pagedResult = new PagedResult<TaskDocument>
        {
            Items = tasks,
            TotalCount = 2,
            HasMore = false
        };

        _factory.MockTaskService
            .Setup(s => s.GetTasksPagedAsync(TestAuthHandler.TestUserId, null, "createdAt", "asc", 0, 50))
            .ReturnsAsync(pagedResult);

        // Act
        var response = await _client.GetAsync("/api/v1/tasks");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var pagedDto = await response.Content.ReadFromJsonAsync<PagedTasksDto>();
        pagedDto!.Items.Should().HaveCount(2);
        pagedDto.TotalCount.Should().Be(2);
        pagedDto.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task GetTasks_WithStateFilter_ReturnsFilteredTasks()
    {
        // Arrange
        var tasks = new List<TaskDocument>
        {
            new TaskDocument { Id = "task-1", UserId = TestAuthHandler.TestUserId, Name = "Task 1", State = TaskState.OnDeck }
        };

        var pagedResult = new PagedResult<TaskDocument>
        {
            Items = tasks,
            TotalCount = 1,
            HasMore = false
        };

        _factory.MockTaskService
            .Setup(s => s.GetTasksPagedAsync(TestAuthHandler.TestUserId, TaskState.OnDeck, "createdAt", "asc", 0, 50))
            .ReturnsAsync(pagedResult);

        // Act
        var response = await _client.GetAsync("/api/v1/tasks?state=OnDeck");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var pagedDto = await response.Content.ReadFromJsonAsync<PagedTasksDto>();
        pagedDto!.Items.Should().HaveCount(1);
        pagedDto.Items.First().State.Should().Be(TaskState.OnDeck);
    }

    [Fact]
    public async Task GetTasks_WithSortingParams_PassesCorrectParamsToService()
    {
        // Arrange (Story 2-5, AC: 1, 2 - sorting)
        var tasks = new List<TaskDocument>
        {
            new TaskDocument { Id = "task-1", UserId = TestAuthHandler.TestUserId, Name = "Task 1", State = TaskState.Completed, CompletedAt = DateTime.UtcNow }
        };

        var pagedResult = new PagedResult<TaskDocument>
        {
            Items = tasks,
            TotalCount = 1,
            HasMore = false
        };

        _factory.MockTaskService
            .Setup(s => s.GetTasksPagedAsync(TestAuthHandler.TestUserId, TaskState.Completed, "completedAt", "desc", 0, 50))
            .ReturnsAsync(pagedResult);

        // Act
        var response = await _client.GetAsync("/api/v1/tasks?state=Completed&orderBy=completedAt&direction=desc");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.MockTaskService.Verify(
            s => s.GetTasksPagedAsync(TestAuthHandler.TestUserId, TaskState.Completed, "completedAt", "desc", 0, 50),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task GetTasks_WithPaginationParams_ReturnsCorrectPage()
    {
        // Arrange (Story 2-5, AC: 3, 4 - pagination)
        var tasks = new List<TaskDocument>
        {
            new TaskDocument { Id = "task-51", UserId = TestAuthHandler.TestUserId, Name = "Task 51", State = TaskState.OnDeck }
        };

        var pagedResult = new PagedResult<TaskDocument>
        {
            Items = tasks,
            TotalCount = 60,
            HasMore = false
        };

        _factory.MockTaskService
            .Setup(s => s.GetTasksPagedAsync(TestAuthHandler.TestUserId, null, "createdAt", "asc", 50, 50))
            .ReturnsAsync(pagedResult);

        // Act
        var response = await _client.GetAsync("/api/v1/tasks?skip=50&take=50");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var pagedDto = await response.Content.ReadFromJsonAsync<PagedTasksDto>();
        pagedDto!.Items.Should().HaveCount(1);
        pagedDto.TotalCount.Should().Be(60);
        pagedDto.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task GetTasks_WithHasMoreTrue_IndicatesMoreDataAvailable()
    {
        // Arrange (Story 2-5, AC: 3 - hasMore flag)
        var tasks = Enumerable.Range(1, 50).Select(i => new TaskDocument
        {
            Id = $"task-{i}",
            UserId = TestAuthHandler.TestUserId,
            Name = $"Task {i}",
            State = TaskState.OnDeck
        }).ToList();

        var pagedResult = new PagedResult<TaskDocument>
        {
            Items = tasks,
            TotalCount = 75,
            HasMore = true
        };

        _factory.MockTaskService
            .Setup(s => s.GetTasksPagedAsync(TestAuthHandler.TestUserId, null, "createdAt", "asc", 0, 50))
            .ReturnsAsync(pagedResult);

        // Act
        var response = await _client.GetAsync("/api/v1/tasks");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var pagedDto = await response.Content.ReadFromJsonAsync<PagedTasksDto>();
        pagedDto!.Items.Should().HaveCount(50);
        pagedDto.TotalCount.Should().Be(75);
        pagedDto.HasMore.Should().BeTrue();
    }

    [Fact]
    public async Task GetTasks_WithCreatedAtAscending_ForOnDeckColumn()
    {
        // Arrange (Story 2-5, AC: 1 - OnDeck sorted by createdAt ASC)
        var tasks = new List<TaskDocument>
        {
            new TaskDocument { Id = "task-old", UserId = TestAuthHandler.TestUserId, Name = "Old Task", State = TaskState.OnDeck, CreatedAt = DateTime.UtcNow.AddDays(-2) },
            new TaskDocument { Id = "task-new", UserId = TestAuthHandler.TestUserId, Name = "New Task", State = TaskState.OnDeck, CreatedAt = DateTime.UtcNow }
        };

        var pagedResult = new PagedResult<TaskDocument>
        {
            Items = tasks,
            TotalCount = 2,
            HasMore = false
        };

        _factory.MockTaskService
            .Setup(s => s.GetTasksPagedAsync(TestAuthHandler.TestUserId, TaskState.OnDeck, "createdAt", "asc", 0, 50))
            .ReturnsAsync(pagedResult);

        // Act
        var response = await _client.GetAsync("/api/v1/tasks?state=OnDeck&orderBy=createdAt&direction=asc");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.MockTaskService.Verify(
            s => s.GetTasksPagedAsync(TestAuthHandler.TestUserId, TaskState.OnDeck, "createdAt", "asc", 0, 50),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task GetTasks_WithCompletedAtDescending_ForCompletedColumn()
    {
        // Arrange (Story 2-5, AC: 2 - Completed sorted by completedAt DESC)
        var tasks = new List<TaskDocument>
        {
            new TaskDocument { Id = "task-recent", UserId = TestAuthHandler.TestUserId, Name = "Recent Task", State = TaskState.Completed, CompletedAt = DateTime.UtcNow },
            new TaskDocument { Id = "task-older", UserId = TestAuthHandler.TestUserId, Name = "Older Task", State = TaskState.Completed, CompletedAt = DateTime.UtcNow.AddDays(-1) }
        };

        var pagedResult = new PagedResult<TaskDocument>
        {
            Items = tasks,
            TotalCount = 2,
            HasMore = false
        };

        _factory.MockTaskService
            .Setup(s => s.GetTasksPagedAsync(TestAuthHandler.TestUserId, TaskState.Completed, "completedAt", "desc", 0, 50))
            .ReturnsAsync(pagedResult);

        // Act
        var response = await _client.GetAsync("/api/v1/tasks?state=Completed&orderBy=completedAt&direction=desc");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.MockTaskService.Verify(
            s => s.GetTasksPagedAsync(TestAuthHandler.TestUserId, TaskState.Completed, "completedAt", "desc", 0, 50),
            Times.AtLeastOnce);
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

        var pagedResult = new PagedResult<TaskDocument>
        {
            Items = currentUserTasks,
            TotalCount = 1,
            HasMore = false
        };

        _factory.MockTaskService
            .Setup(s => s.GetTasksPagedAsync(TestAuthHandler.TestUserId, null, "createdAt", "asc", 0, 50))
            .ReturnsAsync(pagedResult);

        // Act
        var response = await _client.GetAsync("/api/v1/tasks");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify the service was called with the correct user ID (at least once due to shared factory)
        _factory.MockTaskService.Verify(
            s => s.GetTasksPagedAsync(TestAuthHandler.TestUserId, null, "createdAt", "asc", 0, 50),
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
            .Setup(s => s.CreateTaskWithOptionalReminderAsync("User's Task", TestAuthHandler.TestUserId, null))
            .ReturnsAsync(createdTask);

        // Act
        await _client.PostAsJsonAsync("/api/v1/tasks", createDto);

        // Assert - verify service was called with correct user ID from auth
        _factory.MockTaskService.Verify(
            s => s.CreateTaskWithOptionalReminderAsync("User's Task", TestAuthHandler.TestUserId, null),
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

    #region Soft Delete Tests (Story 2-4: Task Deletion)

    [Fact]
    public async Task UpdateTask_TransitionToDeleted_Returns200()
    {
        // Arrange (Story 2-4, AC: 2 - soft delete via state transition)
        var updateDto = new UpdateTaskDto { State = TaskState.Deleted };
        var deletedTask = new TaskDocument
        {
            Id = "task-to-delete",
            UserId = TestAuthHandler.TestUserId,
            Name = "Task to Delete",
            State = TaskState.Deleted,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        _factory.MockTaskService
            .Setup(s => s.UpdateTaskAsync("task-to-delete", TestAuthHandler.TestUserId, null, TaskState.Deleted))
            .ReturnsAsync(deletedTask);

        // Act
        var response = await _client.PutAsJsonAsync("/api/v1/tasks/task-to-delete", updateDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var taskDto = await response.Content.ReadFromJsonAsync<TaskDto>();
        taskDto!.State.Should().Be(TaskState.Deleted);
    }

    [Fact]
    public async Task GetTasks_ExcludesDeletedTasksByDefault()
    {
        // Arrange (Story 2-4, AC: 4 - deleted tasks excluded from results by default)
        // When GetTasksPagedAsync is called without a state filter, it should only return non-deleted tasks
        var activeTasks = new List<TaskDocument>
        {
            new TaskDocument { Id = "active-task-1", UserId = TestAuthHandler.TestUserId, Name = "Active Task 1", State = TaskState.OnDeck },
            new TaskDocument { Id = "active-task-2", UserId = TestAuthHandler.TestUserId, Name = "Active Task 2", State = TaskState.InProgress }
        };

        var pagedResult = new PagedResult<TaskDocument>
        {
            Items = activeTasks,
            TotalCount = 2,
            HasMore = false
        };

        // The service should NOT return deleted tasks when no state filter is provided
        _factory.MockTaskService
            .Setup(s => s.GetTasksPagedAsync(TestAuthHandler.TestUserId, null, "createdAt", "asc", 0, 50))
            .ReturnsAsync(pagedResult);

        // Act
        var response = await _client.GetAsync("/api/v1/tasks");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var pagedDto = await response.Content.ReadFromJsonAsync<PagedTasksDto>();
        pagedDto!.Items.Should().HaveCount(2);
        pagedDto.Items.Should().NotContain(t => t.State == TaskState.Deleted);
    }

    [Fact]
    public async Task GetTasks_WithDeletedStateFilter_ReturnsDeletedTasks()
    {
        // Arrange (Story 2-4 - explicit filter for deleted tasks should work)
        var deletedTasks = new List<TaskDocument>
        {
            new TaskDocument { Id = "deleted-task", UserId = TestAuthHandler.TestUserId, Name = "Deleted Task", State = TaskState.Deleted }
        };

        var pagedResult = new PagedResult<TaskDocument>
        {
            Items = deletedTasks,
            TotalCount = 1,
            HasMore = false
        };

        _factory.MockTaskService
            .Setup(s => s.GetTasksPagedAsync(TestAuthHandler.TestUserId, TaskState.Deleted, "createdAt", "asc", 0, 50))
            .ReturnsAsync(pagedResult);

        // Act
        var response = await _client.GetAsync("/api/v1/tasks?state=Deleted");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var pagedDto = await response.Content.ReadFromJsonAsync<PagedTasksDto>();
        pagedDto!.Items.Should().HaveCount(1);
        pagedDto.Items.First().State.Should().Be(TaskState.Deleted);
    }

    [Fact]
    public async Task UpdateTask_DeleteOtherUsersTask_Returns404()
    {
        // Arrange (Story 2-4, AC related - user isolation for delete operations)
        var updateDto = new UpdateTaskDto { State = TaskState.Deleted };

        // Simulate that the task belongs to a different user by having service throw TaskNotFoundException
        _factory.MockTaskService
            .Setup(s => s.UpdateTaskAsync("other-users-task", TestAuthHandler.TestUserId, null, TaskState.Deleted))
            .ThrowsAsync(new TaskNotFoundException("other-users-task"));

        // Act
        var response = await _client.PutAsJsonAsync("/api/v1/tasks/other-users-task", updateDto);

        // Assert - 404 not 403, to avoid information leakage
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        errorResponse!.Error.Code.Should().Be("TASK_NOT_FOUND");
    }

    #endregion

    #region Complete Task with Unity Tests (Story 3-4: Task-Reminder Unity on Complete)

    [Fact]
    public async Task CompleteTask_WithReminder_Returns200AndDismissesReminder()
    {
        // Arrange (Story 3-4, AC: 1, 3 - task moves to Completed and reminder is dismissed atomically)
        var taskId = "task-with-reminder";
        var completedTask = new TaskDocument
        {
            Id = taskId,
            UserId = TestAuthHandler.TestUserId,
            Name = "Task with Reminder",
            State = TaskState.Completed,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            CompletedAt = DateTime.UtcNow,
            ReminderId = null // Cleared after Unity
        };

        _factory.MockTaskService
            .Setup(s => s.CompleteTaskWithUnityAsync(TestAuthHandler.TestUserId, taskId))
            .ReturnsAsync(completedTask);

        // Act
        var response = await _client.PutAsync($"/api/v1/tasks/{taskId}/complete", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var taskDto = await response.Content.ReadFromJsonAsync<TaskDto>();
        taskDto.Should().NotBeNull();
        taskDto!.State.Should().Be(TaskState.Completed);
        taskDto.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CompleteTask_WithoutReminder_Returns200()
    {
        // Arrange (Story 3-4, AC: relates to handling tasks without reminders)
        var taskId = "task-without-reminder";
        var completedTask = new TaskDocument
        {
            Id = taskId,
            UserId = TestAuthHandler.TestUserId,
            Name = "Task without Reminder",
            State = TaskState.Completed,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            CompletedAt = DateTime.UtcNow,
            ReminderId = null
        };

        _factory.MockTaskService
            .Setup(s => s.CompleteTaskWithUnityAsync(TestAuthHandler.TestUserId, taskId))
            .ReturnsAsync(completedTask);

        // Act
        var response = await _client.PutAsync($"/api/v1/tasks/{taskId}/complete", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var taskDto = await response.Content.ReadFromJsonAsync<TaskDto>();
        taskDto.Should().NotBeNull();
        taskDto!.State.Should().Be(TaskState.Completed);
    }

    [Fact]
    public async Task CompleteTask_NonExistentTask_Returns404()
    {
        // Arrange (Story 3-4 - task not found handling)
        var invalidTaskId = "invalid-task-id";

        _factory.MockTaskService
            .Setup(s => s.CompleteTaskWithUnityAsync(TestAuthHandler.TestUserId, invalidTaskId))
            .ThrowsAsync(new TaskNotFoundException(invalidTaskId));

        // Act
        var response = await _client.PutAsync($"/api/v1/tasks/{invalidTaskId}/complete", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        errorResponse.Should().NotBeNull();
        errorResponse!.Error.Code.Should().Be("TASK_NOT_FOUND");
    }

    [Fact]
    public async Task CompleteTask_TransactionFails_Returns500()
    {
        // Arrange (Story 3-4, AC: 4 - transaction failure handling)
        var taskId = "task-unity-fail";

        _factory.MockTaskService
            .Setup(s => s.CompleteTaskWithUnityAsync(TestAuthHandler.TestUserId, taskId))
            .ThrowsAsync(new UnityTransactionFailedException("Transaction failed", taskId));

        // Act
        var response = await _client.PutAsync($"/api/v1/tasks/{taskId}/complete", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        errorResponse.Should().NotBeNull();
        errorResponse!.Error.Code.Should().Be("UNITY_TRANSACTION_FAILED");
        errorResponse.Error.Message.Should().Contain("Please try again");
    }

    [Fact]
    public async Task CompleteTask_WithoutAuthentication_Returns401()
    {
        // Arrange (Story 3-4 - authentication required)
        _client.DefaultRequestHeaders.Add("X-Test-Unauthenticated", "true");

        // Act
        var response = await _client.PutAsync("/api/v1/tasks/some-task/complete", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CompleteTask_SetsCompletedAtTimestamp()
    {
        // Arrange (Story 3-4, AC: 3 - completedAt is set correctly)
        var taskId = "task-timestamp-test";
        var completedAt = DateTime.UtcNow;
        var completedTask = new TaskDocument
        {
            Id = taskId,
            UserId = TestAuthHandler.TestUserId,
            Name = "Timestamp Test Task",
            State = TaskState.Completed,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            CompletedAt = completedAt,
            ReminderId = null
        };

        _factory.MockTaskService
            .Setup(s => s.CompleteTaskWithUnityAsync(TestAuthHandler.TestUserId, taskId))
            .ReturnsAsync(completedTask);

        // Act
        var response = await _client.PutAsync($"/api/v1/tasks/{taskId}/complete", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var taskDto = await response.Content.ReadFromJsonAsync<TaskDto>();
        taskDto!.CompletedAt.Should().BeCloseTo(completedAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task CompleteTask_CallsServiceWithCorrectUserId()
    {
        // Arrange (Story 3-4 - user isolation)
        var taskId = "task-user-isolation";
        var completedTask = new TaskDocument
        {
            Id = taskId,
            UserId = TestAuthHandler.TestUserId,
            Name = "User Isolation Task",
            State = TaskState.Completed,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            CompletedAt = DateTime.UtcNow,
            ReminderId = null
        };

        _factory.MockTaskService
            .Setup(s => s.CompleteTaskWithUnityAsync(TestAuthHandler.TestUserId, taskId))
            .ReturnsAsync(completedTask);

        // Act
        await _client.PutAsync($"/api/v1/tasks/{taskId}/complete", null);

        // Assert - verify service was called with correct user ID from auth
        _factory.MockTaskService.Verify(
            s => s.CompleteTaskWithUnityAsync(TestAuthHandler.TestUserId, taskId),
            Times.Once);
    }

    #endregion

    #region Delete Task with Unity Tests (Story 3-5: Unity on Delete & Reminder Status Display)

    [Fact]
    public async Task DeleteTask_WithReminder_Returns204AndDismissesReminder()
    {
        // Arrange (Story 3-5, AC: 1, 2 - task is deleted and reminder is dismissed atomically)
        var taskId = "task-with-reminder-delete";

        _factory.MockTaskService
            .Setup(s => s.DeleteTaskWithUnityAsync(TestAuthHandler.TestUserId, taskId))
            .Returns(Task.CompletedTask);

        // Act
        var response = await _client.DeleteAsync($"/api/v1/tasks/{taskId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        _factory.MockTaskService.Verify(
            s => s.DeleteTaskWithUnityAsync(TestAuthHandler.TestUserId, taskId),
            Times.Once);
    }

    [Fact]
    public async Task DeleteTask_WithoutReminder_Returns204()
    {
        // Arrange (Story 3-5, Task 1.3 - delete works for tasks without reminders)
        var taskId = "task-without-reminder-delete";

        _factory.MockTaskService
            .Setup(s => s.DeleteTaskWithUnityAsync(TestAuthHandler.TestUserId, taskId))
            .Returns(Task.CompletedTask);

        // Act
        var response = await _client.DeleteAsync($"/api/v1/tasks/{taskId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteTask_NonExistentTask_Returns404()
    {
        // Arrange (Story 3-5, Task 2.5 - not found handling)
        var invalidTaskId = "invalid-task-id";

        _factory.MockTaskService
            .Setup(s => s.DeleteTaskWithUnityAsync(TestAuthHandler.TestUserId, invalidTaskId))
            .ThrowsAsync(new TaskNotFoundException(invalidTaskId));

        // Act
        var response = await _client.DeleteAsync($"/api/v1/tasks/{invalidTaskId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        errorResponse.Should().NotBeNull();
        errorResponse!.Error.Code.Should().Be("TASK_NOT_FOUND");
    }

    [Fact]
    public async Task DeleteTask_TransactionFails_Returns500()
    {
        // Arrange (Story 3-5, AC: 2 - transaction failure handling)
        var taskId = "task-unity-delete-fail";

        _factory.MockTaskService
            .Setup(s => s.DeleteTaskWithUnityAsync(TestAuthHandler.TestUserId, taskId))
            .ThrowsAsync(new UnityTransactionFailedException("Transaction failed", taskId));

        // Act
        var response = await _client.DeleteAsync($"/api/v1/tasks/{taskId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        errorResponse.Should().NotBeNull();
        errorResponse!.Error.Code.Should().Be("UNITY_TRANSACTION_FAILED");
        errorResponse.Error.Message.Should().Contain("Please try again");
    }

    [Fact]
    public async Task DeleteTask_WithoutAuthentication_Returns401()
    {
        // Arrange (Story 3-5 - authentication required)
        _client.DefaultRequestHeaders.Add("X-Test-Unauthenticated", "true");

        // Act
        var response = await _client.DeleteAsync("/api/v1/tasks/some-task");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteTask_CallsServiceWithCorrectUserId()
    {
        // Arrange (Story 3-5 - user isolation)
        var taskId = "task-delete-user-isolation";

        _factory.MockTaskService
            .Setup(s => s.DeleteTaskWithUnityAsync(TestAuthHandler.TestUserId, taskId))
            .Returns(Task.CompletedTask);

        // Act
        await _client.DeleteAsync($"/api/v1/tasks/{taskId}");

        // Assert - verify service was called with correct user ID from auth
        _factory.MockTaskService.Verify(
            s => s.DeleteTaskWithUnityAsync(TestAuthHandler.TestUserId, taskId),
            Times.Once);
    }

    [Fact]
    public async Task DeleteTask_OtherUsersTask_Returns404()
    {
        // Arrange (Story 3-5 - user isolation for delete operations)
        var otherUsersTaskId = "other-users-task-delete";

        // Simulate that the task belongs to a different user by having service throw TaskNotFoundException
        _factory.MockTaskService
            .Setup(s => s.DeleteTaskWithUnityAsync(TestAuthHandler.TestUserId, otherUsersTaskId))
            .ThrowsAsync(new TaskNotFoundException(otherUsersTaskId));

        // Act
        var response = await _client.DeleteAsync($"/api/v1/tasks/{otherUsersTaskId}");

        // Assert - 404 not 403, to avoid information leakage
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        errorResponse!.Error.Code.Should().Be("TASK_NOT_FOUND");
    }

    #endregion

    #region Combined Task+Reminder Creation Tests (Story 3-2: Create Task with Reminder)

    [Fact]
    public async Task CreateTask_WithScheduledTime_CreatesTaskWithReminder()
    {
        // Arrange (Story 3-2, AC: 3 - creates both Task and TaskReminder together)
        var scheduledTime = DateTime.UtcNow.AddHours(2);
        var createDto = new CreateTaskDto
        {
            Name = "Task with Reminder",
            ScheduledTime = scheduledTime
        };

        var createdTask = new TaskDocument
        {
            Id = Guid.NewGuid().ToString(),
            UserId = TestAuthHandler.TestUserId,
            Name = "Task with Reminder",
            State = TaskState.OnDeck,
            CreatedAt = DateTime.UtcNow,
            ReminderId = "reminder-123"
        };

        _factory.MockTaskService
            .Setup(s => s.CreateTaskWithOptionalReminderAsync("Task with Reminder", TestAuthHandler.TestUserId, scheduledTime))
            .ReturnsAsync(createdTask);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/tasks", createDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var taskDto = await response.Content.ReadFromJsonAsync<TaskDto>();
        taskDto.Should().NotBeNull();
        taskDto!.Name.Should().Be("Task with Reminder");
        taskDto.ReminderId.Should().Be("reminder-123");

        // Verify the new combined method was called
        _factory.MockTaskService.Verify(
            s => s.CreateTaskWithOptionalReminderAsync("Task with Reminder", TestAuthHandler.TestUserId, scheduledTime),
            Times.Once);
    }

    [Fact]
    public async Task CreateTask_WithoutScheduledTime_CreatesOnlyTask()
    {
        // Arrange (Story 3-2, AC: 4 - only Task created when no scheduledTime)
        var createDto = new CreateTaskDto { Name = "Task without Reminder" };

        var createdTask = new TaskDocument
        {
            Id = Guid.NewGuid().ToString(),
            UserId = TestAuthHandler.TestUserId,
            Name = "Task without Reminder",
            State = TaskState.OnDeck,
            CreatedAt = DateTime.UtcNow,
            ReminderId = null
        };

        _factory.MockTaskService
            .Setup(s => s.CreateTaskWithOptionalReminderAsync("Task without Reminder", TestAuthHandler.TestUserId, null))
            .ReturnsAsync(createdTask);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/tasks", createDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var taskDto = await response.Content.ReadFromJsonAsync<TaskDto>();
        taskDto.Should().NotBeNull();
        taskDto!.ReminderId.Should().BeNull();

        // Verify the combined method was called with null scheduledTime
        _factory.MockTaskService.Verify(
            s => s.CreateTaskWithOptionalReminderAsync("Task without Reminder", TestAuthHandler.TestUserId, null),
            Times.Once);
    }

    [Fact]
    public async Task CreateTask_WithPastScheduledTime_Returns400InvalidScheduledTime()
    {
        // Arrange (Story 3-2, AC: 5 - validation prevents past times)
        var pastTime = DateTime.UtcNow.AddHours(-1);
        var createDto = new CreateTaskDto
        {
            Name = "Task with Past Reminder",
            ScheduledTime = pastTime
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/tasks", createDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        errorResponse.Should().NotBeNull();
        errorResponse!.Error.Code.Should().Be("INVALID_SCHEDULED_TIME");
        errorResponse.Error.Message.Should().Contain("future");
    }

    [Fact]
    public async Task CreateTask_WithScheduledTime_TaskHasReminderId()
    {
        // Arrange (Story 3-2, AC: 3 - task should have reminderId populated)
        var scheduledTime = DateTime.UtcNow.AddDays(1);
        var createDto = new CreateTaskDto
        {
            Name = "Task to check reminderId",
            ScheduledTime = scheduledTime
        };

        var createdTask = new TaskDocument
        {
            Id = "task-abc",
            UserId = TestAuthHandler.TestUserId,
            Name = "Task to check reminderId",
            State = TaskState.OnDeck,
            CreatedAt = DateTime.UtcNow,
            ReminderId = "reminder-xyz"
        };

        _factory.MockTaskService
            .Setup(s => s.CreateTaskWithOptionalReminderAsync("Task to check reminderId", TestAuthHandler.TestUserId, scheduledTime))
            .ReturnsAsync(createdTask);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/tasks", createDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var taskDto = await response.Content.ReadFromJsonAsync<TaskDto>();
        taskDto!.Id.Should().Be("task-abc");
        taskDto.ReminderId.Should().Be("reminder-xyz");
    }

    [Fact]
    public async Task CreateTask_WithScheduledTimeInFuture_ReturnsCreated()
    {
        // Arrange (Story 3-2 - valid future time should work)
        var futureTime = DateTime.UtcNow.AddMinutes(30);
        var createDto = new CreateTaskDto
        {
            Name = "Future Reminder Task",
            ScheduledTime = futureTime
        };

        var createdTask = new TaskDocument
        {
            Id = "task-future",
            UserId = TestAuthHandler.TestUserId,
            Name = "Future Reminder Task",
            State = TaskState.OnDeck,
            CreatedAt = DateTime.UtcNow,
            ReminderId = "reminder-future"
        };

        _factory.MockTaskService
            .Setup(s => s.CreateTaskWithOptionalReminderAsync("Future Reminder Task", TestAuthHandler.TestUserId, futureTime))
            .ReturnsAsync(createdTask);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/tasks", createDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    #endregion
}
