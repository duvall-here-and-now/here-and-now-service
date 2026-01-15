# Here and Now Service - Data Models

**Date:** 2026-01-15

## Overview

This document describes the domain models and data structures used in the Here and Now Service. The project follows a clean architecture pattern with three layers:

| Layer | Assembly | Purpose |
|-------|----------|---------|
| **Message** | HereAndNow.Message | Demo business logic (static messages) |
| **Task** | HereAndNow.Task | Core business logic (Tasks, Reminders, CosmosDB) |
| **Web** | HereAndNow.Web | API layer (Controllers, DTOs, Mappers) |

## Storage

- **Message Layer:** In-memory (static messages for Auth0 demo)
- **Task Layer:** **Azure Cosmos DB** (NoSQL document store)
  - Database: `HereAndNow`
  - Container: `Tasks`
  - Partition Key: `/userId`
  - Document Types: `Task`, `TaskReminder` (type discriminator pattern)

---

## Task Module Domain Models

Located in: `Task/HereAndNow.Task/Models/`

### TaskDocument

**File:** `TaskDocument.cs`
**Namespace:** `HereAndNowService.Models`
**Purpose:** Core task entity stored in CosmosDB.

```csharp
public class TaskDocument
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "Task";

    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = TaskState.OnDeck;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; set; }

    [JsonPropertyName("reminderId")]
    public string? ReminderId { get; set; }

    [JsonPropertyName("lastModifiedAt")]
    public DateTime LastModifiedAt { get; set; }
}
```

**Field Descriptions:**

| Field | Type | Description |
|-------|------|-------------|
| `Id` | string | Unique identifier (GUID) |
| `Type` | string | Document type discriminator (`"Task"`) |
| `UserId` | string | Partition key - user who owns the task |
| `Name` | string | Task name/title (1-500 chars) |
| `State` | string | Current state (OnDeck, InProgress, Completed, Deleted) |
| `CreatedAt` | DateTime | UTC creation timestamp |
| `CompletedAt` | DateTime? | UTC completion timestamp (null if not completed) |
| `ReminderId` | string? | Associated reminder ID (null if none) |
| `LastModifiedAt` | DateTime | UTC last modification timestamp |

---

### TaskReminderDocument

**File:** `TaskReminderDocument.cs`
**Namespace:** `HereAndNowService.Models`
**Purpose:** Reminder entity stored in same CosmosDB container as tasks.

```csharp
public class TaskReminderDocument
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("type")]
    public string Type { get; set; } = "TaskReminder";

    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("taskId")]
    public string TaskId { get; set; } = string.Empty;

    [JsonPropertyName("taskName")]
    public string TaskName { get; set; } = string.Empty;

    [JsonPropertyName("scheduledTime")]
    public DateTime ScheduledTime { get; set; }

    [JsonPropertyName("isDismissed")]
    public bool IsDismissed { get; set; } = false;

    [JsonPropertyName("dismissedAt")]
    public DateTime? DismissedAt { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("lastModifiedAt")]
    public DateTime LastModifiedAt { get; set; }
}
```

**Field Descriptions:**

| Field | Type | Description |
|-------|------|-------------|
| `Id` | string | Unique identifier (GUID) |
| `Type` | string | Document type discriminator (`"TaskReminder"`) |
| `UserId` | string | Partition key - user who owns the reminder |
| `TaskId` | string | Associated task ID |
| `TaskName` | string | Denormalized task name (for display without join) |
| `ScheduledTime` | DateTime | UTC time when reminder triggers |
| `IsDismissed` | bool | Whether reminder has been dismissed |
| `DismissedAt` | DateTime? | UTC dismissal timestamp |
| `CreatedAt` | DateTime | UTC creation timestamp |
| `LastModifiedAt` | DateTime | UTC last modification timestamp |

**Design Notes:**
- `TaskName` is denormalized to avoid joins when displaying reminders
- When task name is updated, `TaskName` is synced atomically using transactional batch

---

### TaskState

**File:** `TaskState.cs`
**Namespace:** `HereAndNowService.Models`
**Purpose:** Constants for task state values (not enum for exact JSON match).

```csharp
public static class TaskState
{
    public const string OnDeck = "OnDeck";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string Deleted = "Deleted";

    public static readonly string[] AllStates = { OnDeck, InProgress, Completed, Deleted };

    public static bool IsValid(string? state) =>
        state is OnDeck or InProgress or Completed or Deleted;
}
```

**State Transitions:**

```
┌─────────────────────────────────────────────────────┐
│                                                       │
│  ┌──────────┐   ┌────────────┐   ┌───────────────┐  │
│  │  OnDeck  │ → │ InProgress │ → │   Completed   │  │
│  └──────────┘   └────────────┘   └───────────────┘  │
│       │              │                               │
│       │              │         ┌───────────────────┐│
│       └──────────────┴───────→ │     Deleted       ││
│                                └───────────────────┘│
└─────────────────────────────────────────────────────┘
```

---

### PagedResult<T>

**File:** `PagedResult.cs`
**Namespace:** `HereAndNowService.Models`
**Purpose:** Generic wrapper for paginated query results.

```csharp
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int TotalCount { get; set; }
    public bool HasMore { get; set; }
}
```

---

## DTOs (Data Transfer Objects)

Located in: `Web/HereAndNow.Web/DTOs/`

### Task DTOs

| DTO | Purpose |
|-----|---------|
| `CreateTaskDto` | Request body for creating tasks |
| `UpdateTaskDto` | Request body for updating tasks |
| `TaskDto` | Response body for task data |
| `PagedTasksDto` | Response body for paginated tasks |

### Reminder DTOs

| DTO | Purpose |
|-----|---------|
| `CreateReminderDto` | Request body for creating reminders |
| `SnoozeReminderDto` | Request body for snoozing reminders |
| `TaskReminderDto` | Response body for reminder data |

### Error DTOs

| DTO | Purpose |
|-----|---------|
| `ErrorResponseDto` | Standard error response wrapper |
| `ErrorDetailsDto` | Error code and message details |

---

## Exceptions

Located in: `Task/HereAndNow.Task/Models/Exceptions/`

| Exception | Thrown When |
|-----------|-------------|
| `TaskNotFoundException` | Task with given ID doesn't exist |
| `ReminderNotFoundException` | Reminder with given ID doesn't exist |
| `ReminderAlreadyExistsException` | Task already has a reminder attached |
| `ReminderAlreadyDismissedException` | Attempting to modify a dismissed reminder |
| `InvalidScheduledTimeException` | Scheduled time is in the past |
| `UnityTransactionFailedException` | CosmosDB transactional batch failed |

---

## Service Layer

### Task Services

Located in: `Task/HereAndNow.Task/Services/`

| Interface | Implementation | Purpose |
|-----------|----------------|---------|
| `ITaskService` | `TaskService` | Task CRUD and Unity operations |
| `ITaskReminderService` | `TaskReminderService` | Reminder CRUD, snooze, dismiss |

### Message Services

Located in: `Message/HereAndNow.Message/Services/`

| Interface | Implementation | Purpose |
|-----------|----------------|---------|
| `IMessageService` | `MessageService` | Demo message retrieval |

---

## Repository Layer

Located in: `Task/HereAndNow.Task/Repositories/`

| Interface | Implementation | Purpose |
|-----------|----------------|---------|
| `ITaskRepository` | `TaskRepository` | CosmosDB operations for tasks |
| `ITaskReminderRepository` | `TaskReminderRepository` | CosmosDB operations for reminders |

**Key Repository Methods:**

```csharp
// TaskRepository
Task<TaskDocument> CreateAsync(TaskDocument task);
Task<IEnumerable<TaskDocument>> GetByUserIdAsync(string userId, string? state = null);
Task<PagedResult<TaskDocument>> GetByUserIdPagedAsync(...);
Task<TaskDocument?> GetByIdAsync(string userId, string taskId);
Task<TaskDocument> UpdateAsync(TaskDocument task);
Task<TaskDocument> CompleteWithUnityAsync(TaskDocument task, TaskReminderDocument? reminder);
Task DeleteWithUnityAsync(TaskDocument task, TaskReminderDocument? reminder);
Task<TaskDocument> UpdateWithReminderSyncAsync(TaskDocument task, TaskReminderDocument reminder);
```

---

## Mappers

Located in: `Web/HereAndNow.Web/Mappers/`

| Mapper | Purpose |
|--------|---------|
| `TaskMapper` | Convert TaskDocument ↔ TaskDto |
| `ReminderMapper` | Convert TaskReminderDocument ↔ TaskReminderDto |

---

## CosmosDB Configuration

**Settings Class:** `CosmosDbSettings`

```csharp
public class CosmosDbSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = "HereAndNow";
    public string ContainerName { get; set; } = "Tasks";
}
```

**Environment Variables:**

| Variable | Purpose |
|----------|---------|
| `COSMOS_CONNECTION_STRING` | CosmosDB connection string |
| `COSMOS_DATABASE_NAME` | Database name (default: HereAndNow) |
| `COSMOS_CONTAINER_NAME` | Container name (default: Tasks) |

---

## Unity Pattern

The "Unity" pattern uses CosmosDB transactional batches to atomically update Task and Reminder documents together:

```csharp
// Example: CompleteWithUnityAsync
var batch = _container.CreateTransactionalBatch(new PartitionKey(task.UserId));
batch.ReplaceItem(task.Id, task);
batch.ReplaceItem(reminder.Id, reminder);
var batchResponse = await batch.ExecuteAsync();
```

**Operations using Unity:**
1. **Complete Task** - Task → Completed, Reminder → Dismissed
2. **Delete Task** - Task → Deleted, Reminder → Dismissed
3. **Update Task Name** - Task name synced to Reminder's denormalized `TaskName`

---

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                         Web Layer                                │
│  ┌───────────────┐  ┌────────────────┐  ┌───────────────────┐  │
│  │ TasksController│  │RemindersController│ │MessagesController│  │
│  └───────┬───────┘  └───────┬────────┘  └─────────┬─────────┘  │
│          │                   │                     │             │
│          ▼                   ▼                     │             │
│  ┌────────────────────────────────────┐           │             │
│  │    DTOs + Mappers + Validation     │           │             │
│  └───────────────┬────────────────────┘           │             │
└──────────────────┼────────────────────────────────┼─────────────┘
                   │                                │
                   ▼                                ▼
┌──────────────────────────────────┐  ┌────────────────────────────┐
│          Task Layer              │  │      Message Layer          │
│  ┌────────────────────────────┐  │  │  ┌──────────────────────┐  │
│  │ ITaskService ← TaskService │  │  │  │IMessageService       │  │
│  │ ITaskReminderService ←     │  │  │  │  ← MessageService    │  │
│  │       TaskReminderService  │  │  │  └──────────────────────┘  │
│  └─────────────┬──────────────┘  │  │            │               │
│                │                  │  │            ▼               │
│                ▼                  │  │  ┌──────────────────────┐  │
│  ┌────────────────────────────┐  │  │  │     Message          │  │
│  │ ITaskRepository ←          │  │  │  │  (static data)       │  │
│  │       TaskRepository       │  │  │  └──────────────────────┘  │
│  │ ITaskReminderRepository ←  │  │  └────────────────────────────┘
│  │    TaskReminderRepository  │  │
│  └─────────────┬──────────────┘  │
│                │                  │
│                ▼                  │
│  ┌────────────────────────────┐  │
│  │    Azure Cosmos DB         │  │
│  │  ┌──────────┬───────────┐  │  │
│  │  │  Task    │TaskReminder│  │  │
│  │  │Documents │ Documents  │  │  │
│  │  └──────────┴───────────┘  │  │
│  └────────────────────────────┘  │
└──────────────────────────────────┘
```

---

_Generated using BMAD Method `document-project` workflow_
_Last Updated: 2026-01-15_
