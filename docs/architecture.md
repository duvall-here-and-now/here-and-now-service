# Here and Now Service - Architecture

**Date:** 2026-05-01
**Architecture Patterns:** Clean Architecture + Command Pattern + Unity Pattern + Computed Instance Model

---

## Assembly Architecture

The solution contains 5 assemblies in a 3-layer clean architecture:

```
┌──────────────────────────────────────────────────────────┐
│  Web Layer (HereAndNow.Web)                              │
│                                                          │
│  Controllers  Commands  DTOs  Mappers  Middleware        │
│  Validation   Program.cs                                 │
└─────────────────────────┬────────────────────────────────┘
                          │ depends on
        ┌─────────────────┴──────────────────┐
        │                                    │
┌───────▼──────────────┐    ┌────────────────▼──────────────────┐
│  HereAndNow.Task     │    │  HereAndNow.Message               │
│                      │    │                                   │
│  Services            │    │  Auth0 demo assembly              │
│  Repositories        │    │  Static messages only             │
│  Models              │    │                                   │
│  Exceptions          │    └───────────────────────────────────┘
└──────────────────────┘
```

### Test Assemblies

- `HereAndNow.Web.Tests` — unit + integration tests; references all 3 production assemblies
- `HereAndNow.Task.Tests` — unit tests for Task assembly only

---

## Command Pattern Architecture

All mutations flow through a **single endpoint**: `POST /api/v1/commands`.

The `CommandsController` receives a `CommandRequest` with a `command` discriminator string and a `payload` JSON element, then dispatches to the appropriate private handler:

```
POST /api/v1/commands
    { "command": "CreateTask", "payload": { "taskId": "...", "name": "..." } }
         │
         ▼
  CommandsController.ExecuteCommand()
         │
         └── switch(request.Command)
               ├── "CreateTask"                 → HandleCreateTaskAsync
               ├── "CreateTaskAndTaskReminder"  → HandleCreateTaskAndTaskReminderAsync
               ├── "UpdateTaskName"             → HandleUpdateTaskNameAsync
               ├── "UpdateTaskState"            → HandleUpdateTaskStateAsync
               ├── "UpdateTaskReminderScheduledTime" → HandleUpdateTaskReminderScheduledTimeAsync
               ├── "DismissTaskReminder"        → HandleDismissTaskReminderAsync
               ├── "CreateRecurringTaskConfig"  → HandleCreateRecurringTaskConfigAsync
               ├── "UpdateRecurringTaskConfig"  → HandleUpdateRecurringTaskConfigAsync
               ├── "DeleteRecurringTaskConfig"  → HandleDeleteRecurringTaskConfigAsync
               ├── "StartRecurringTask"         → HandleStartRecurringTaskAsync
               ├── "RevertRecurringTaskToOnDeck" → HandleRevertRecurringTaskToOnDeckAsync
               ├── "CompleteRecurringTask"      → HandleCompleteRecurringTaskAsync
               ├── "SkipRecurringTask"          → HandleSkipRecurringTaskAsync
               └── _ → 400 UNKNOWN_COMMAND
```

**Adding a new mutation:** add a command class in `Commands/`, add a handler in `CommandsController`, add a service method in `ITaskService` or `IRecurringTaskService`, wire the switch case.

Read operations use conventional REST endpoints on `TasksController`, `RemindersController`, `RecurringTaskConfigsController`, and `RecurringTasksController`.

---

## Unity Pattern (Atomic Operations)

The Unity Pattern uses Cosmos DB **transactional batches** to update two documents atomically. If either operation fails, both roll back.

### Unity Operations

**CompleteWithUnity / UpdateState to Completed or Deleted:**
```csharp
var batch = _container.CreateTransactionalBatch(new PartitionKey(task.UserId));
batch.ReplaceItem(task.Id, updatedTask);     // Update task state
batch.ReplaceItem(reminder.Id, dismissed);   // Dismiss reminder
await batch.ExecuteAsync();
```

**UpdateTaskName (with reminder sync):**
```csharp
var batch = _container.CreateTransactionalBatch(new PartitionKey(task.UserId));
batch.ReplaceItem(task.Id, renamedTask);         // Rename task
batch.ReplaceItem(reminder.Id, syncedReminder);  // Sync denormalized taskName
await batch.ExecuteAsync();
```

**CreateTaskWithReminder:**
```csharp
var batch = _container.CreateTransactionalBatch(new PartitionKey(userId));
batch.CreateItem(task);
batch.CreateItem(reminder);
await batch.ExecuteAsync();
```

**Rule:** Never update a task and its reminder in separate Cosmos calls when the operation must be atomic.

---

## Cosmos DB Design

### Container Layout

- **Database:** `HereAndNow` (default, overridable via env)
- **Container:** `Tasks` (default, overridable via env)
- **Partition Key:** `/userId`

All 4 document types co-exist in one container, differentiated by the `type` discriminator field:

| `type` value | Document Class | Partition Key |
|---|---|---|
| `"Task"` | `TaskDocument` | `userId` |
| `"TaskReminder"` | `TaskReminderDocument` | `userId` |
| `"RecurringTaskConfig"` | `RecurringTaskConfigDocument` | `userId` |
| `"RecurringTaskStateOverride"` | `RecurringTaskStateOverrideDocument` | `userId` |

**Benefits:** All a user's data lives in one physical partition. Transactional batches can span all 4 document types. No cross-partition queries.

### Cosmos Client Configuration

```csharp
new CosmosClientOptions {
    SerializerOptions = new CosmosSerializationOptions {
        PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
    }
}
```

Repositories and the CosmosClient are registered as **Singleton**. Services are **Scoped**.

---

## Recurring Task Computation Model

Recurring task **instances are never persisted** — they are computed on-demand from two stored sources:

1. `RecurringTaskConfigDocument` — the RRULE definition
2. `RecurringTaskStateOverrideDocument` — sparse explicit state changes

### Two-Query Pattern (NFR43)

```csharp
// Exactly 2 Cosmos queries per GetComputedInstancesAsync call
var configs = await _repository.GetAllConfigsAsync(userId);
var overrides = await _repository.GetOverridesForConfigsAsync(userId, configIds);
// Then compute in-memory
```

### Computation Pipeline (4 phases)

```
Phase 1: Build override lookup dictionary (O(1) per occurrence)
         key = "{configId}_{recurrenceDateAndTime:yyyy-MM-ddTHH:mm:ssZ}"

Phase 2: For each config (independent scope):
         - Expand RRULE occurrences via Ical.Net for the date range
         - Traverse newest-first with activeCandidate sentinel

Phase 3: State resolution per occurrence (6 branches):
         ┌─ Has override?
         │   ├─ Completed or Skipped → terminal, emit as-is
         │   ├─ InProgress → emit as-is, set activeCandidate = this
         │   └─ Unexpected → pass-through
         └─ No override?
             ├─ Future occurrence → Scheduled
             ├─ Past + no activeCandidate → OnDeck (promote this occurrence)
             └─ Past + activeCandidate exists → Scheduled (already have active)

Phase 4: Aggregate all instances across all configs
```

**One-active-at-a-time invariant:** Only the most recent past instance without a terminal override may be `OnDeck` or `InProgress` for a given config.

See [compute-instances-algorithm.md](./compute-instances-algorithm.md) for the full documented algorithm with visual walkthroughs.

---

## Middleware Pipeline

```
Request
   │
   ▼
UseSwagger / UseSwaggerUI
   │
   ▼
UseErrorHandler         ← catches unhandled exceptions, maps to ErrorResponseDto
   │
   ▼
UseSecureHeaders        ← adds X-Content-Type-Options, X-Frame-Options, HSTS, etc.
   │
   ▼
MapControllers
   │
   ▼
UseCors
   │
   ▼
UseAuthentication       ← validates Auth0 JWT Bearer
   │
   ▼
UseAuthorization
   │
   ▼
Controller Action
```

### ErrorHandlerMiddleware

Maps domain exceptions to HTTP status codes and the standard `ErrorResponseDto` envelope. Controllers also handle some exceptions directly (see `CommandsController`).

### SecureHeadersMiddleware

Adds security headers to all responses. Header list is hardcoded in the middleware.

---

## Authentication & Authorization

- **Provider:** Auth0
- **Scheme:** JWT Bearer
- **Authority:** `https://{AUTH0_DOMAIN}/`
- **Audience:** `{AUTH0_AUDIENCE}`
- All task/reminder/recurring endpoints: `[Authorize]` (require valid JWT)
- Public message endpoint: no authorization
- `userId` is always `User.FindFirst(ClaimTypes.NameIdentifier)?.Value`

---

## Dependency Injection Registration

Registered in `Program.cs`:

| Interface | Implementation | Lifetime |
|-----------|----------------|----------|
| `IMessageService` | `MessageService` | Scoped |
| `CosmosDbSettings` | — (POCO) | Singleton |
| `CosmosClient` | — (SDK) | Singleton |
| `ITaskRepository` | `TaskRepository` | Singleton |
| `ITaskReminderRepository` | `TaskReminderRepository` | Singleton |
| `IRecurringTaskRepository` | `RecurringTaskRepository` | Singleton |
| `ITaskService` | `TaskService` | Scoped |
| `ITaskReminderService` | `TaskReminderService` | Scoped |
| `IRecurringTaskService` | `RecurringTaskService` | Scoped |

**Cosmos services are only registered when `COSMOS_CONNECTION_STRING` is configured.** Without it, only the Message endpoints work.

---

## Validation Architecture

Two layers of validation:

1. **Model validation** (ASP.NET Core) — `[Required]`, `[MinLength]`, `[FutureTime]` attributes on DTO/command classes. Handled by `ApiBehaviorOptions.InvalidModelStateResponseFactory`, which returns the project-standard `ErrorResponseDto` (not ProblemDetails).

2. **Controller-level validation** — explicit GUID format checks, state value checks, UTC enforcement. Done in each `CommandsController` handler before calling the service.

**`ScheduledTime` validation failures** use the `INVALID_SCHEDULED_TIME` error code (not `VALIDATION_ERROR`) to allow clients to handle reminder time errors distinctly.

---

## Configuration Architecture

Environment variables loaded via `dotenv.net` from `.env` at startup:

| Variable | Required | Default | Purpose |
|----------|----------|---------|---------|
| `PORT` | Yes | — | Kestrel listen port |
| `CLIENT_ORIGIN_URL` | Yes | — | CORS allowed origins (comma-separated) |
| `AUTH0_DOMAIN` | Yes | — | JWT authority domain |
| `AUTH0_AUDIENCE` | Yes | — | JWT audience |
| `COSMOS_CONNECTION_STRING` | No | — | Enables Cosmos features |
| `COSMOS_DATABASE_NAME` | No | `HereAndNow` | Cosmos DB name |
| `COSMOS_CONTAINER_NAME` | No | `Tasks` | Cosmos container name |

Cosmos DB database and container are **auto-created on startup** if they don't exist.

---

## Testing Architecture

### Unit Tests

- `HereAndNow.Task.Tests` — pure unit tests for services and models; mock repositories with Moq
- `HereAndNow.Web.Tests/Controllers/` — controller unit tests; mock services with Moq; set `HttpContext.User` manually

### Integration Tests

- `HereAndNow.Web.Tests/Integration/` — full HTTP stack via `WebApplicationFactory`
- Uses `TestWebApplicationFactory` with mock Cosmos services (no real Cosmos connection)
- Uses `TestAuthHandler` to simulate Auth0 JWT; supports `X-Test-UserId` header for user isolation tests

### Test Conventions

```csharp
// Standard test user
const string TestUserId = "auth0|test-user-123";

// Controller user setup
controller.ControllerContext = new ControllerContext
{
    HttpContext = new DefaultHttpContext
    {
        User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, TestUserId)
        }))
    }
};

// Naming: MethodName_Condition_ExpectedResult
[Fact] public async Task CreateTask_WithValidPayload_Returns201WithTaskDto() { ... }
```
