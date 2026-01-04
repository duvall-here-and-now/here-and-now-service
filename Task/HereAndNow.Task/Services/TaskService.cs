using HereAndNowService.Models;
using HereAndNowService.Models.Exceptions;
using HereAndNowService.Repositories;
using Microsoft.Extensions.Logging;

namespace HereAndNowService.Services;

/// <summary>
/// Service implementation for Task business logic
/// </summary>
public class TaskService : ITaskService
{
    private readonly ITaskRepository _taskRepository;
    private readonly ILogger<TaskService> _logger;

    /// <summary>
    /// Creates a new TaskService instance
    /// </summary>
    public TaskService(ITaskRepository taskRepository, ILogger<TaskService> logger)
    {
        _taskRepository = taskRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TaskDocument> CreateTaskAsync(string name, string userId)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Task name cannot be empty", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID cannot be empty", nameof(userId));
        }

        _logger.LogDebug("Creating task '{Name}' for user {UserId}", name, userId);

        var task = new TaskDocument
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            Name = name.Trim(),
            State = TaskState.OnDeck,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = null,
            ReminderId = null
        };

        var createdTask = await _taskRepository.CreateAsync(task);

        _logger.LogInformation("Created task {TaskId} '{Name}' for user {UserId}",
            createdTask.Id, createdTask.Name, userId);

        return createdTask;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TaskDocument>> GetTasksAsync(string userId, string? state = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID cannot be empty", nameof(userId));
        }

        // Validate state if provided
        if (state is not null && !TaskState.IsValid(state))
        {
            throw new ArgumentException($"Invalid task state: {state}", nameof(state));
        }

        _logger.LogDebug("Getting tasks for user {UserId} with state filter {State}",
            userId, state ?? "all");

        return await _taskRepository.GetByUserIdAsync(userId, state);
    }

    /// <inheritdoc />
    public async Task<TaskDocument> GetTaskByIdAsync(string taskId, string userId)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            throw new ArgumentException("Task ID cannot be empty", nameof(taskId));
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID cannot be empty", nameof(userId));
        }

        _logger.LogDebug("Getting task {TaskId} for user {UserId}", taskId, userId);

        var task = await _taskRepository.GetByIdAsync(taskId, userId);

        if (task is null)
        {
            throw new TaskNotFoundException(taskId);
        }

        return task;
    }

    /// <inheritdoc />
    public async Task<TaskDocument> UpdateTaskAsync(string taskId, string userId, string? name, string? state)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            throw new ArgumentException("Task ID cannot be empty", nameof(taskId));
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID cannot be empty", nameof(userId));
        }

        _logger.LogDebug("Updating task {TaskId} for user {UserId}", taskId, userId);

        // Fetch the existing task
        var task = await _taskRepository.GetByIdAsync(taskId, userId);

        if (task is null)
        {
            throw new TaskNotFoundException(taskId);
        }

        // Update name if provided
        if (!string.IsNullOrWhiteSpace(name))
        {
            task.Name = name.Trim();
        }

        // Handle state transitions with completedAt logic
        if (!string.IsNullOrWhiteSpace(state))
        {
            var isTransitioningToCompleted = state == TaskState.Completed && task.State != TaskState.Completed;
            var isTransitioningFromCompleted = state != TaskState.Completed && task.State == TaskState.Completed;

            task.State = state;

            if (isTransitioningToCompleted)
            {
                task.CompletedAt = DateTime.UtcNow;
                _logger.LogDebug("Task {TaskId} marked completed, setting completedAt", taskId);
            }
            else if (isTransitioningFromCompleted)
            {
                task.CompletedAt = null;
                _logger.LogDebug("Task {TaskId} transitioned from Completed, clearing completedAt", taskId);
            }
            // Otherwise: preserve existing completedAt
        }

        var updatedTask = await _taskRepository.UpdateAsync(task);

        _logger.LogInformation("Updated task {TaskId} for user {UserId}", taskId, userId);

        return updatedTask;
    }
}
