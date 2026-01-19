# Here and Now Service - Architecture

**Date:** 2026-01-18
**Version:** 4.0
**Architecture Patterns:** Clean Architecture + Command Pattern + Unity Pattern

## Executive Summary

The Here and Now Service is a RESTful API built on ASP.NET Core 8.0 that provides task management with reminder functionality. It features Auth0 JWT authentication, Azure Cosmos DB persistence, a **Command Pattern** for mutations, and the "Unity" pattern for atomic Task-Reminder operations.

> **v1.3.0 Evolution:** The API has evolved from traditional REST CRUD to a **Command Pattern** architecture, providing explicit intent, client-generated IDs for optimistic UI, and a single mutation endpoint.

The architecture follows Clean Architecture principles with three assemblies:
- **Message** - Demo business logic (Auth0 sample)
- **Task** - Core business logic with CosmosDB (Tasks + Reminders)
- **Web** - API layer (Controllers, Commands, DTOs, Mappers, Validation)

## Technology Stack

| Category | Technology | Version | Justification |
|----------|------------|---------|---------------|
| **Runtime** | .NET | 8.0 | LTS release, modern C# features, excellent performance |
| **Framework** | ASP.NET Core | 8.0 | Industry standard for .NET APIs |
| **Language** | C# | 12 | Modern features (file-scoped namespaces, nullable) |
| **Database** | Azure Cosmos DB | 3.46.1 | NoSQL document store with transactional batches |
| **Auth** | Auth0 + JWT Bearer | 8.0.11 | Managed identity, secure token validation |
| **API Docs** | Swashbuckle | 6.9.0 | Swagger/OpenAPI generation |
| **Env Config** | dotenv.net | 3.2.1 | 12-factor app configuration |
| **Testing** | xUnit | 2.9.2 | Popular .NET testing framework |
| **Mocking** | Moq | 4.20.72 | Flexible mocking for unit tests |
| **Assertions** | FluentAssertions | 6.12.0 | Readable test assertions |
| **Integration Testing** | Mvc.Testing | 8.0.11 | In-memory test server |

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              Web Layer                                        │
│  ┌─────────────────────────────────────────────────────────────────────────┐ │
│  │                         Controllers                                      │ │
│  │  ┌────────────────┐ ┌─────────────┐ ┌─────────────┐ ┌──────────────────┐│ │
│  │  │   Commands     │ │   Tasks     │ │  Reminders  │ │Messages │ Error  ││ │
│  │  │   Controller   │ │  Controller │ │  Controller │ │Ctrl     │ Ctrl   ││ │
│  │  │ (mutations)    │ │ (queries)   │ │ (queries)   │ │(demo)   │        ││ │
│  │  └───────┬────────┘ └──────┬──────┘ └──────┬──────┘ └─────────┴────────┘│ │
│  └──────────┼─────────────────┼───────────────┼────────────────────────────┘ │
│             │                 │               │                               │
│  ┌──────────┴─────────────────┴───────────────┴────────────────────────────┐ │
│  │  Commands (6) + DTOs + Mappers + Validation (FutureTimeValidation)      │ │
│  └──────────┬──────────────────────────────────────────────────────────────┘ │
│             │                                                                 │
│  ┌──────────┴──────────────────────────────────────────────────────────────┐ │
│  │  Middlewares: ErrorHandler, SecureHeaders                                │ │
│  └──────────────────────────────────────────────────────────────────────────┘ │
└────────────────────────────────┬────────────────────────────────────────────┘
                                 │
                                 │ Dependency Injection
                                 ▼
┌───────────────────────────────────────────────┬──────────────────────────────┐
│                Task Layer                      │       Message Layer          │
│  ┌─────────────────────────────────────────┐  │  ┌────────────────────────┐  │
│  │               Services                   │  │  │  IMessageService       │  │
│  │  ┌────────────────┐ ┌─────────────────┐ │  │  │    ↓                   │  │
│  │  │ ITaskService   │ │ ITaskReminder   │ │  │  │  MessageService        │  │
│  │  │    ↓           │ │   Service ↓     │ │  │  └────────────────────────┘  │
│  │  │ TaskService    │ │ TaskReminder    │ │  │           │                  │
│  │  │ • State Machine│ │   Service       │ │  │           ▼                  │
│  │  │ • Unity Ops    │ │                 │ │  │  ┌────────────────────────┐  │
│  │  └───────┬────────┘ └────────┬────────┘ │  │  │    Message             │  │
│  └──────────┼───────────────────┼──────────┘  │  │  (static data)         │  │
│             │                   │              │  └────────────────────────┘  │
│  ┌──────────┴───────────────────┴──────────┐  │                              │
│  │              Repositories               │  │                              │
│  │  ┌─────────────────┐ ┌────────────────┐ │  │                              │
│  │  │ ITaskRepository │ │ ITaskReminder  │ │  │                              │
│  │  │    ↓            │ │   Repository ↓ │ │  │                              │
│  │  │ TaskRepository  │ │ TaskReminder   │ │  │                              │
│  │  │ • Unity Batches │ │   Repository   │ │  │                              │
│  │  └────────┬────────┘ └───────┬────────┘ │  │                              │
│  └───────────┼──────────────────┼──────────┘  │                              │
│              │                  │              │                              │
│              ▼                  ▼              │                              │
│  ┌─────────────────────────────────────────┐  │                              │
│  │           Azure Cosmos DB               │  │                              │
│  │  ┌─────────────┐  ┌──────────────────┐  │  │                              │
│  │  │ Task        │  │ TaskReminder     │  │  │                              │
│  │  │ Documents   │  │ Documents        │  │  │                              │
│  │  └─────────────┘  └──────────────────┘  │  │                              │
│  │        Partition Key: /userId           │  │                              │
│  └─────────────────────────────────────────┘  │                              │
└───────────────────────────────────────────────┴──────────────────────────────┘
```

## Assembly Structure

```
HereAndNow.sln
├── HereAndNow.Message          (Business Logic - Demo)
│   ├── Models/
│   │   └── Message.cs
│   └── Services/
│       ├── IMessageService.cs
│       └── MessageService.cs
│
├── HereAndNow.Task             (Business Logic - Core)
│   ├── Models/
│   │   ├── TaskDocument.cs
│   │   ├── TaskReminderDocument.cs
│   │   ├── TaskState.cs
│   │   ├── PagedResult.cs
│   │   └── Exceptions/
│   │       ├── TaskNotFoundException.cs
│   │       ├── TaskAlreadyExistsException.cs       ← NEW
│   │       ├── ReminderNotFoundException.cs
│   │       ├── TaskReminderAlreadyExistsException.cs ← NEW
│   │       ├── ReminderAlreadyExistsException.cs
│   │       ├── ReminderAlreadyDismissedException.cs
│   │       ├── InvalidScheduledTimeException.cs
│   │       ├── InvalidStateTransitionException.cs   ← NEW
│   │       └── UnityTransactionFailedException.cs
│   ├── Repositories/
│   │   ├── ITaskRepository.cs
│   │   ├── TaskRepository.cs
│   │   ├── ITaskReminderRepository.cs
│   │   ├── TaskReminderRepository.cs
│   │   └── CosmosDbSettings.cs
│   └── Services/
│       ├── ITaskService.cs
│       ├── TaskService.cs
│       ├── ITaskReminderService.cs
│       └── TaskReminderService.cs
│
├── HereAndNow.Web              (Web API Layer)
│   ├── Controllers/
│   │   ├── CommandsController.cs   ← NEW (primary mutations)
│   │   ├── MessagesController.cs
│   │   ├── TasksController.cs      (queries + legacy complete)
│   │   ├── RemindersController.cs  (queries)
│   │   └── ErrorController.cs
│   ├── Commands/                   ← NEW (Command Pattern)
│   │   ├── CommandRequest.cs
│   │   ├── CommandResponse.cs
│   │   ├── CreateTaskCommand.cs
│   │   ├── CreateTaskAndTaskReminderCommand.cs
│   │   ├── UpdateTaskNameCommand.cs
│   │   ├── UpdateTaskStateCommand.cs
│   │   ├── UpdateTaskReminderScheduledTimeCommand.cs
│   │   └── DismissTaskReminderCommand.cs
│   ├── DTOs/
│   │   ├── CreateTaskDto.cs
│   │   ├── TaskDto.cs
│   │   ├── PagedTasksDto.cs
│   │   ├── TaskAndReminderDto.cs   ← NEW
│   │   ├── CreateReminderDto.cs
│   │   ├── TaskReminderDto.cs
│   │   └── ErrorResponseDto.cs
│   ├── Mappers/
│   │   ├── TaskMapper.cs
│   │   └── ReminderMapper.cs
│   ├── Validation/
│   │   └── FutureTimeValidationAttribute.cs
│   └── Middlewares/
│       ├── ErrorHandlerMiddleware.cs
│       └── SecureHeadersMiddleware.cs
│
└── HereAndNow.Web.Tests        (Test Project)
    ├── Controllers/
    │   ├── CommandsControllerTests.cs  ← NEW
    │   ├── TasksControllerTests.cs
    │   └── RemindersControllerTests.cs
    ├── Services/
    │   ├── TaskServiceTests.cs
    │   └── TaskReminderServiceTests.cs
    ├── Integration/
    │   ├── AuthorizationTests.cs
    │   ├── CorsTests.cs
    │   ├── CommandsApiTests.cs  ← NEW
    │   ├── TasksApiTests.cs
    │   └── RemindersApiTests.cs
    └── Helpers/
        ├── TestWebApplicationFactory.cs
        └── TestAuthHandler.cs
```

## Key Architectural Patterns

### 1. Command Pattern (NEW in v1.3.0)

The Command Pattern provides explicit intent-based operations through a single endpoint:

```
POST /api/v1/commands
{
  "command": "CreateTask",
  "payload": { "taskId": "...", "name": "..." }
}
```

**Why Command Pattern?**

| Benefit | Explanation |
|---------|-------------|
| **Explicit Intent** | Commands express what to do, not just HTTP verbs |
| **Client-Generated IDs** | Enables optimistic UI - UI updates before server responds |
| **Single Endpoint** | All mutations through `POST /api/v1/commands` |
| **Idempotency** | Client can retry safely (if ID exists → success) |
| **Atomic Operations** | Natural fit for Unity pattern |

**Command Flow:**

```
┌─────────────────────┐
│ POST /api/v1/commands │
│ { command, payload }  │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ CommandsController  │
│ ExecuteCommand()    │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Switch on command   │
│ type, deserialize   │
│ payload to Command  │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Call appropriate    │
│ Service method      │
│ (TaskService, etc.) │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Return result       │
│ (TaskDto, etc.)     │
└─────────────────────┘
```

**Available Commands:**

| Command | Service Method | Response |
|---------|---------------|----------|
| `CreateTask` | `CreateTaskWithIdAsync` | `TaskDto` |
| `CreateTaskAndTaskReminder` | `CreateTaskWithReminderAsync` | `TaskAndReminderDto` |
| `UpdateTaskName` | `UpdateTaskNameAsync` | `TaskDto` |
| `UpdateTaskState` | `UpdateStateAsync` | `TaskDto` |
| `UpdateTaskReminderScheduledTime` | `SnoozeAsync` | `TaskReminderDto` |
| `DismissTaskReminder` | `DismissAsync` | 204 No Content |

### 2. Service-Repository Pattern

The Task layer implements the Service-Repository pattern:
- **Services** contain business logic, state machine, and orchestrate operations
- **Repositories** handle data access (CosmosDB operations)
- Clear separation enables unit testing with mock repositories

### 3. Unity Pattern

The "Unity" pattern ensures Task and Reminder are updated atomically using CosmosDB transactional batches:

```csharp
// Example: Completing a task with reminder (UpdateTaskState → Completed)
var batch = _container.CreateTransactionalBatch(new PartitionKey(task.UserId));
batch.ReplaceItem(task.Id, task);         // Task → Completed
batch.ReplaceItem(reminder.Id, reminder); // Reminder → Dismissed
await batch.ExecuteAsync();
```

**Unity Operations:**

| Operation | Task Change | Reminder Change |
|-----------|-------------|-----------------|
| CreateTaskAndTaskReminder | Created with reminderId | Created with taskId |
| UpdateTaskState → Completed | state → Completed | isDismissed → true |
| UpdateTaskState → Deleted | state → Deleted | isDismissed → true |
| UpdateTaskName (with reminder) | name updated | taskName synced |

### 4. Task State Machine

The TaskService implements a state machine for task lifecycle:

```
┌─────────────────────────────────────────────────────────┐
│                                                           │
│  ┌──────────┐   ┌────────────┐   ┌───────────────────┐  │
│  │  OnDeck  │ ↔ │ InProgress │ ↔ │    Completed      │  │
│  └──────────┘   └────────────┘   └───────────────────┘  │
│       │              │                                   │
│       │              │         ┌───────────────────────┐│
│       └──────────────┴───────→ │       Deleted         ││
│                                │  (terminal - no exit) ││
│                                └───────────────────────┘│
└─────────────────────────────────────────────────────────┘
```

**State Machine Rules:**
- **Idempotent:** Same state = no-op success
- **Terminal:** Deleted cannot transition to other states
- **CompletedAt:** Set on → Completed, cleared on ← Completed
- **Unity:** → Completed/Deleted with reminder triggers atomic dismiss

### 5. Denormalization

`TaskReminderDocument.TaskName` is denormalized from the parent Task to enable:
- Reminder lists without joining to tasks
- Efficient mobile/SPA display
- When task name changes, it's synced atomically via Unity

## Request Flow

### Command Request Flow (Mutations)

```
HTTP Request (with JWT)
     │
     ▼
┌─────────────────────┐
│  Kestrel Server     │
└────────┬────────────┘
         │
         ▼
┌─────────────────────┐
│ Error Handler       │ ◄── Catches exceptions, formats ErrorResponseDto
│ Middleware          │
└────────┬────────────┘
         │
         ▼
┌─────────────────────┐
│ Secure Headers      │ ◄── Adds security headers (HSTS, CSP, etc.)
│ Middleware          │
└────────┬────────────┘
         │
         ▼
┌─────────────────────┐
│     CORS            │ ◄── Validates origin, handles preflight
└────────┬────────────┘
         │
         ▼
┌─────────────────────┐
│ Authentication      │ ◄── Validates JWT token with Auth0
└────────┬────────────┘
         │
         ▼
┌─────────────────────┐
│ Authorization       │ ◄── Enforces [Authorize] attributes
└────────┬────────────┘
         │
         ▼
┌─────────────────────┐
│ CommandsController  │ ◄── POST /api/v1/commands
│ ExecuteCommand()    │     Dispatch to handler
└────────┬────────────┘
         │
         ▼
┌─────────────────────┐
│    Service          │ ◄── TaskService / TaskReminderService
│ • State Machine     │     Business logic, validation
│ • Unity Ops         │
└────────┬────────────┘
         │
         ▼
┌─────────────────────┐
│   Repository        │ ◄── TaskRepository / TaskReminderRepository
│ • Transactional     │     CosmosDB operations, Unity batches
│   Batches           │
└────────┬────────────┘
         │
         ▼
┌─────────────────────┐
│   Cosmos DB         │ ◄── Document storage, partition key: /userId
└────────┬────────────┘
         │
         ▼
HTTP Response (TaskDto / TaskReminderDto / ErrorResponseDto)
```

## Cosmos DB Design

### Container Structure

- **Database:** HereAndNow
- **Container:** Tasks
- **Partition Key:** `/userId`
- **Document Types:** Task, TaskReminder (type discriminator pattern)

### Why Single Container?

1. **Transactional Batches** - CosmosDB batches require same partition
2. **Unity Pattern** - Atomic Task+Reminder updates need same container
3. **Query Efficiency** - Both document types queried by userId

### Partition Strategy

```
Container: Tasks
├── Partition: user-123
│   ├── { type: "Task", id: "task-1", ... }
│   ├── { type: "Task", id: "task-2", ... }
│   ├── { type: "TaskReminder", id: "reminder-1", taskId: "task-1", ... }
│   └── { type: "TaskReminder", id: "reminder-2", taskId: "task-2", ... }
├── Partition: user-456
│   └── ...
```

## API Endpoints Summary

### Commands (Primary Mutation API)

| Endpoint | Method | Commands |
|----------|--------|----------|
| /api/v1/commands | POST | CreateTask, CreateTaskAndTaskReminder, UpdateTaskName, UpdateTaskState, UpdateTaskReminderScheduledTime, DismissTaskReminder |

### Tasks (Queries + Legacy)

| Endpoint | Method | Auth |
|----------|--------|------|
| /api/v1/tasks | GET | JWT |
| /api/v1/tasks/{id} | GET | JWT |
| /api/v1/tasks/{id}/complete | PUT | JWT |
| /api/v1/tasks | POST | JWT (deprecated) |

### Reminders (Queries)

| Endpoint | Method | Auth |
|----------|--------|------|
| /api/v1/reminders | GET | JWT |
| /api/v1/reminders/{id} | GET | JWT |
| /api/v1/reminders/{id}/dismiss | PUT | JWT |

### Messages (Demo)

| Endpoint | Method | Auth |
|----------|--------|------|
| /api/messages/public | GET | None |
| /api/messages/protected | GET | JWT |
| /api/messages/admin | GET | JWT |

## Security Architecture

### Authentication Flow

```
┌──────────┐     ┌──────────┐     ┌──────────────┐
│  Client  │────►│  Auth0   │────►│ JWT Token    │
└──────────┘     └──────────┘     └──────┬───────┘
                                         │
                                         ▼
                               ┌──────────────────┐
                               │ API Request with │
                               │ Bearer Token     │
                               └────────┬─────────┘
                                        │
                                        ▼
                               ┌──────────────────┐
                               │ JWT Validation   │
                               │ • Issuer         │
                               │ • Audience       │
                               │ • Signature      │
                               └────────┬─────────┘
                                        │
                                        ▼
                               ┌──────────────────┐
                               │ Extract userId   │
                               │ from ClaimTypes  │
                               │ .NameIdentifier  │
                               └──────────────────┘
```

### Multi-Tenant Isolation

- Each user's data is partitioned by `userId` in CosmosDB
- Services extract `userId` from JWT claims
- All queries include `userId` filter (enforced at repository level)

### Security Headers

The `SecureHeadersMiddleware` adds:

| Header | Value | Purpose |
|--------|-------|---------|
| X-XSS-Protection | 0 | Disable legacy XSS filter |
| Strict-Transport-Security | max-age=31536000; includeSubDomains | Force HTTPS |
| X-Frame-Options | deny | Prevent clickjacking |
| X-Content-Type-Options | nosniff | Prevent MIME sniffing |
| Content-Security-Policy | default-src 'self'; frame-ancestors 'none'; | Restrict resources |
| Cache-Control | no-cache, no-store, max-age=0, must-revalidate | Prevent caching |

## Testing Architecture

### Test Pyramid

```
           ┌─────────────┐
           │   Manual    │  Swagger UI
           ├─────────────┤
           │ Integration │  CommandsApiTests, TasksApiTests,
           │             │  RemindersApiTests, AuthorizationTests
           ├─────────────┤
           │    Unit     │  CommandsControllerTests, TaskServiceTests,
           │             │  TaskReminderServiceTests
           └─────────────┘
```

### Test Infrastructure

- **TestWebApplicationFactory**: Custom factory with mock auth and in-memory config
- **TestAuthHandler**: Mock JWT authentication for integration tests
- **Moq**: Mocking service/repository dependencies
- **FluentAssertions**: Readable test assertions

## Deployment Architecture

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│   GitHub        │────►│  GitHub Actions │────►│   Azure Web     │
│   Repository    │     │  Build + Test   │     │   App Service   │
└─────────────────┘     └─────────────────┘     └─────────────────┘
                                                        │
                                                        ▼
                        ┌───────────────────────────────────────────┐
                        │                                           │
                        ▼                                           ▼
               ┌─────────────────┐                       ┌─────────────────┐
               │    Auth0        │                       │  Azure Cosmos   │
               │    (Identity)   │                       │      DB         │
               └─────────────────┘                       └─────────────────┘
```

### CI/CD Pipeline

1. **Trigger**: Push to `main` branch
2. **Build**: `dotnet build --configuration Release`
3. **Test**: `dotnet test` with TRX reporting + code coverage
4. **Publish**: `dotnet publish`
5. **Deploy**: Azure Web Apps Deploy action with `clean: true`

## Configuration

### Environment Variables

| Variable | Required | Default | Description |
|----------|----------|---------|-------------|
| PORT | Yes | - | Server port number |
| CLIENT_ORIGIN_URL | Yes | - | Allowed CORS origins (comma-separated) |
| AUTH0_DOMAIN | Yes | - | Auth0 tenant domain |
| AUTH0_AUDIENCE | Yes | - | Auth0 API identifier |
| COSMOS_CONNECTION_STRING | No | - | CosmosDB connection (enables Task features) |
| COSMOS_DATABASE_NAME | No | HereAndNow | CosmosDB database name |
| COSMOS_CONTAINER_NAME | No | Tasks | CosmosDB container name |

### Conditional Features

- If `COSMOS_CONNECTION_STRING` is not set, Task/Reminder endpoints won't be available
- Message endpoints work without CosmosDB (static data)

---

_Generated using BMAD Method `document-project` workflow_
_Last Updated: 2026-01-18_
