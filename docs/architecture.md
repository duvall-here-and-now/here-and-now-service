# Here and Now Service - Architecture

**Date:** 2026-01-15
**Version:** 3.0
**Architecture Pattern:** Clean Architecture with Service-Repository Pattern

## Executive Summary

The Here and Now Service is a RESTful API built on ASP.NET Core 8.0 that provides task management with reminder functionality. It features Auth0 JWT authentication, Azure Cosmos DB persistence, and implements the "Unity" pattern for atomic Task-Reminder operations.

The architecture follows Clean Architecture principles with three assemblies:
- **Message** - Demo business logic (Auth0 sample)
- **Task** - Core business logic with CosmosDB (Tasks + Reminders)
- **Web** - API layer (Controllers, DTOs, Mappers, Validation)

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
┌─────────────────────────────────────────────────────────────────────────┐
│                              Web Layer                                    │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │                         Controllers                                │  │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────────┐   │  │
│  │  │  Tasks      │  │  Reminders  │  │  Messages    │  Error   │   │  │
│  │  │  Controller │  │  Controller │  │  Controller  │  Ctrl    │   │  │
│  │  └──────┬──────┘  └──────┬──────┘  └──────┬───────┴──────────┘   │  │
│  └─────────┼────────────────┼────────────────┼───────────────────────┘  │
│            │                │                │                           │
│  ┌─────────┴────────────────┴────────────────┴───────────────────────┐  │
│  │  DTOs + Mappers + Validation (FutureTimeValidationAttribute)      │  │
│  └─────────┬────────────────┬────────────────────────────────────────┘  │
│            │                │                                            │
│  ┌─────────┴────────────────┴────────────────────────────────────────┐  │
│  │  Middlewares: ErrorHandler, SecureHeaders                          │  │
│  └───────────────────────────────────────────────────────────────────┘  │
└────────────────────────────────┬─────────────────────────────────────────┘
                                 │
                                 │ Dependency Injection
                                 ▼
┌───────────────────────────────────────────────┬──────────────────────────┐
│                Task Layer                      │     Message Layer        │
│  ┌─────────────────────────────────────────┐  │  ┌────────────────────┐  │
│  │               Services                   │  │  │  IMessageService   │  │
│  │  ┌────────────────┐ ┌─────────────────┐ │  │  │    ↓               │  │
│  │  │ ITaskService   │ │ ITaskReminder   │ │  │  │  MessageService    │  │
│  │  │    ↓           │ │   Service ↓     │ │  │  └────────────────────┘  │
│  │  │ TaskService    │ │ TaskReminder    │ │  │           │              │
│  │  │                │ │   Service       │ │  │           ▼              │
│  │  └───────┬────────┘ └────────┬────────┘ │  │  ┌────────────────────┐  │
│  └──────────┼───────────────────┼──────────┘  │  │    Message         │  │
│             │                   │              │  │  (static data)     │  │
│  ┌──────────┴───────────────────┴──────────┐  │  └────────────────────┘  │
│  │              Repositories               │  │                          │
│  │  ┌─────────────────┐ ┌────────────────┐ │  │                          │
│  │  │ ITaskRepository │ │ ITaskReminder  │ │  │                          │
│  │  │    ↓            │ │   Repository ↓ │ │  │                          │
│  │  │ TaskRepository  │ │ TaskReminder   │ │  │                          │
│  │  │                 │ │   Repository   │ │  │                          │
│  │  └────────┬────────┘ └───────┬────────┘ │  │                          │
│  └───────────┼──────────────────┼──────────┘  │                          │
│              │                  │              │                          │
│              ▼                  ▼              │                          │
│  ┌─────────────────────────────────────────┐  │                          │
│  │           Azure Cosmos DB               │  │                          │
│  │  ┌─────────────┐  ┌──────────────────┐  │  │                          │
│  │  │ Task        │  │ TaskReminder     │  │  │                          │
│  │  │ Documents   │  │ Documents        │  │  │                          │
│  │  └─────────────┘  └──────────────────┘  │  │                          │
│  │        Partition Key: /userId           │  │                          │
│  └─────────────────────────────────────────┘  │                          │
└───────────────────────────────────────────────┴──────────────────────────┘
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
│   │       ├── ReminderNotFoundException.cs
│   │       ├── ReminderAlreadyExistsException.cs
│   │       ├── ReminderAlreadyDismissedException.cs
│   │       ├── InvalidScheduledTimeException.cs
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
│   │   ├── MessagesController.cs
│   │   ├── TasksController.cs
│   │   ├── RemindersController.cs
│   │   └── ErrorController.cs
│   ├── DTOs/
│   │   ├── CreateTaskDto.cs
│   │   ├── UpdateTaskDto.cs
│   │   ├── TaskDto.cs
│   │   ├── PagedTasksDto.cs
│   │   ├── CreateReminderDto.cs
│   │   ├── SnoozeReminderDto.cs
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
    │   ├── TasksControllerTests.cs
    │   └── RemindersControllerTests.cs
    ├── Services/
    │   ├── TaskServiceTests.cs
    │   └── TaskReminderServiceTests.cs
    ├── Integration/
    │   ├── AuthorizationTests.cs
    │   ├── CorsTests.cs
    │   ├── TasksApiTests.cs
    │   └── RemindersApiTests.cs
    └── Helpers/
        ├── TestWebApplicationFactory.cs
        └── TestAuthHandler.cs
```

## Dependency Graph

```
HereAndNow.Web.Tests
    │
    ├──► HereAndNow.Web
    │        │
    │        ├──► HereAndNow.Task
    │        │
    │        └──► HereAndNow.Message
    │
    ├──► HereAndNow.Task
    │
    └──► HereAndNow.Message
```

## Key Architectural Patterns

### 1. Service-Repository Pattern

The Task layer implements the Service-Repository pattern:
- **Services** contain business logic and orchestrate operations
- **Repositories** handle data access (CosmosDB operations)
- Clear separation enables unit testing with mock repositories

### 2. Unity Pattern

The "Unity" pattern ensures Task and Reminder are updated atomically using CosmosDB transactional batches:

```csharp
// Example: Completing a task with reminder
var batch = _container.CreateTransactionalBatch(new PartitionKey(task.UserId));
batch.ReplaceItem(task.Id, task);       // Task → Completed
batch.ReplaceItem(reminder.Id, reminder); // Reminder → Dismissed
await batch.ExecuteAsync();
```

**Unity Operations:**
| Operation | Task Change | Reminder Change |
|-----------|-------------|-----------------|
| Complete Task | state → Completed | isDismissed → true |
| Delete Task | state → Deleted | isDismissed → true |
| Update Task Name | name updated | taskName synced |

### 3. Denormalization

`TaskReminderDocument.TaskName` is denormalized from the parent Task to enable:
- Reminder lists without joining to tasks
- Efficient mobile/SPA display
- When task name changes, it's synced atomically via Unity

## Request Flow

### Protected Endpoint Flow (Task/Reminder Operations)

```
HTTP Request (with JWT)
     │
     ▼
┌─────────────────┐
│  Kestrel Server │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Error Handler   │ ◄── Catches exceptions, formats ErrorResponseDto
│ Middleware      │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Secure Headers  │ ◄── Adds security headers (HSTS, CSP, etc.)
│ Middleware      │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│     CORS        │ ◄── Validates origin, handles preflight
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Authentication  │ ◄── Validates JWT token with Auth0
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Authorization   │ ◄── Enforces [Authorize] attributes
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│   Controller    │ ◄── TasksController / RemindersController
│   + Mapper      │     Maps DTOs ↔ Documents
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│    Service      │ ◄── TaskService / TaskReminderService
│                 │     Business logic, validation
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│   Repository    │ ◄── TaskRepository / TaskReminderRepository
│                 │     CosmosDB operations, Unity batches
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│   Cosmos DB     │ ◄── Document storage, partition key: /userId
└────────┬────────┘
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

## API Endpoints Summary

| Controller | Endpoint | Method | Auth |
|------------|----------|--------|------|
| Messages | /api/messages/public | GET | None |
| Messages | /api/messages/protected | GET | JWT |
| Messages | /api/messages/admin | GET | JWT |
| Tasks | /api/v1/tasks | POST, GET | JWT |
| Tasks | /api/v1/tasks/{id} | GET, PUT, DELETE | JWT |
| Tasks | /api/v1/tasks/{id}/complete | PUT | JWT |
| Reminders | /api/v1/reminders | POST, GET | JWT |
| Reminders | /api/v1/reminders/{id} | GET, PUT | JWT |
| Reminders | /api/v1/reminders/{id}/dismiss | PUT | JWT |

## Dependency Injection

### Service Registration (Program.cs)

```csharp
// Message services
builder.Services.AddScoped<IMessageService, MessageService>();

// Cosmos DB (if configured)
if (!string.IsNullOrEmpty(cosmosConnectionString))
{
    builder.Services.AddSingleton(cosmosDbSettings);
    builder.Services.AddSingleton<CosmosClient>(...);
    builder.Services.AddSingleton<ITaskRepository, TaskRepository>();
    builder.Services.AddSingleton<ITaskReminderRepository, TaskReminderRepository>();
    builder.Services.AddScoped<ITaskService, TaskService>();
    builder.Services.AddScoped<ITaskReminderService, TaskReminderService>();
}
```

### Lifetime Rationale

| Service | Lifetime | Reason |
|---------|----------|--------|
| CosmosClient | Singleton | Thread-safe, connection pooling |
| Repositories | Singleton | Stateless, uses singleton CosmosClient |
| Services | Scoped | Per-request business operations |
| IMessageService | Scoped | Per-request (legacy pattern) |

## Testing Architecture

### Test Pyramid

```
           ┌─────────────┐
           │   Manual    │  Swagger UI
           ├─────────────┤
           │ Integration │  TasksApiTests, RemindersApiTests,
           │             │  AuthorizationTests, CorsTests
           ├─────────────┤
           │    Unit     │  TaskServiceTests, TaskReminderServiceTests,
           │             │  TasksControllerTests, RemindersControllerTests
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
_Last Updated: 2026-01-15_
