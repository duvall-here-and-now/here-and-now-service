# Here and Now Service - Source Tree Analysis

**Date:** 2026-01-18

## Overview

This document provides an annotated directory structure of the Here and Now Service codebase, highlighting critical directories, entry points, and key file locations.

## Complete Directory Structure

```
here-and-now-service/
├── .claude/                        # Claude Code configuration
│   └── commands/                   # Custom slash commands
├── .env                            # Environment variables (not in git)
├── .github/                        # GitHub configuration
│   ├── agents/                     # AI agent definitions
│   │   └── dotnet-code-reviewer.md
│   ├── copilot-instructions.md
│   └── workflows/                  # CI/CD pipelines
│       └── main_here-and-now-service.yml
├── .gitignore
├── CLAUDE.md                       # Claude Code instructions
├── HereAndNow.sln                  # ★ Solution file (entry point)
├── README.md                       # Project readme
│
├── Message/                        # ★ Business Logic Assembly (Demo)
│   └── HereAndNow.Message/
│       ├── HereAndNow.Message.csproj
│       ├── Models/
│       │   └── Message.cs          # Simple message model
│       └── Services/
│           ├── IMessageService.cs
│           └── MessageService.cs   # Static message provider
│
├── Task/                           # ★ Business Logic Assembly (Core)
│   └── HereAndNow.Task/
│       ├── HereAndNow.Task.csproj
│       ├── Models/
│       │   ├── TaskDocument.cs         # Task entity for CosmosDB
│       │   ├── TaskReminderDocument.cs # Reminder entity for CosmosDB
│       │   ├── TaskState.cs            # State constants
│       │   ├── PagedResult.cs          # Pagination wrapper
│       │   └── Exceptions/             # Business exceptions
│       │       ├── TaskNotFoundException.cs
│       │       ├── TaskAlreadyExistsException.cs      # NEW
│       │       ├── ReminderNotFoundException.cs
│       │       ├── TaskReminderAlreadyExistsException.cs  # NEW
│       │       ├── ReminderAlreadyExistsException.cs
│       │       ├── ReminderAlreadyDismissedException.cs
│       │       ├── InvalidScheduledTimeException.cs
│       │       ├── InvalidStateTransitionException.cs     # NEW
│       │       └── UnityTransactionFailedException.cs
│       ├── Repositories/
│       │   ├── ITaskRepository.cs
│       │   ├── TaskRepository.cs       # CosmosDB operations for tasks
│       │   ├── ITaskReminderRepository.cs
│       │   ├── TaskReminderRepository.cs # CosmosDB operations for reminders
│       │   └── CosmosDbSettings.cs     # Configuration class
│       └── Services/
│           ├── ITaskService.cs
│           ├── TaskService.cs          # Task business logic + state machine
│           ├── ITaskReminderService.cs
│           └── TaskReminderService.cs  # Reminder business logic
│
├── Web/                            # ★ Web Layer Assemblies
│   ├── HereAndNow.Web/             # API Project
│   │   ├── HereAndNow.Web.csproj
│   │   ├── Program.cs              # ★ Application entry point
│   │   ├── Controllers/
│   │   │   ├── ErrorController.cs
│   │   │   ├── MessagesController.cs
│   │   │   ├── CommandsController.cs     # ★ NEW - Command Pattern API
│   │   │   ├── TasksController.cs        # Queries + legacy complete
│   │   │   └── RemindersController.cs    # Queries + legacy endpoints
│   │   ├── Commands/                     # ★ NEW - Command Pattern
│   │   │   ├── CommandRequest.cs
│   │   │   ├── CommandResponse.cs
│   │   │   ├── CreateTaskCommand.cs
│   │   │   ├── CreateTaskAndTaskReminderCommand.cs
│   │   │   ├── UpdateTaskNameCommand.cs
│   │   │   ├── UpdateTaskStateCommand.cs
│   │   │   ├── UpdateTaskReminderScheduledTimeCommand.cs
│   │   │   └── DismissTaskReminderCommand.cs
│   │   ├── DTOs/
│   │   │   ├── CreateTaskDto.cs
│   │   │   ├── TaskDto.cs
│   │   │   ├── PagedTasksDto.cs
│   │   │   ├── TaskAndReminderDto.cs     # NEW - combined response
│   │   │   ├── CreateReminderDto.cs
│   │   │   ├── TaskReminderDto.cs
│   │   │   └── ErrorResponseDto.cs
│   │   ├── Mappers/
│   │   │   ├── TaskMapper.cs
│   │   │   └── ReminderMapper.cs
│   │   ├── Validation/
│   │   │   └── FutureTimeValidationAttribute.cs
│   │   ├── Middlewares/
│   │   │   ├── ErrorHandlerMiddleware.cs
│   │   │   └── SecureHeadersMiddleware.cs
│   │   └── SWAGGER_SETUP.md
│   │
│   └── HereAndNow.Web.Tests/       # Test Project
│       ├── HereAndNow.Web.Tests.csproj
│       ├── Controllers/
│       │   ├── CommandsControllerTests.cs  # NEW
│       │   ├── TasksControllerTests.cs
│       │   └── RemindersControllerTests.cs
│       ├── Services/
│       │   ├── TaskServiceTests.cs
│       │   └── TaskReminderServiceTests.cs
│       ├── Integration/
│       │   ├── AuthorizationTests.cs
│       │   ├── CorsTests.cs
│       │   ├── CommandsApiTests.cs     # NEW
│       │   ├── TasksApiTests.cs
│       │   └── RemindersApiTests.cs
│       └── Helpers/
│           ├── TestWebApplicationFactory.cs
│           └── TestAuthHandler.cs
│
├── Reminders/                      # ⚠️ DEPRECATED (empty folder)
│
├── docs/                           # Generated documentation
│   ├── index.md                    # Master index
│   ├── project-overview.md
│   ├── architecture.md
│   ├── api-contracts.md
│   ├── data-models.md
│   ├── source-tree-analysis.md
│   ├── development-guide.md
│   ├── deployment-guide.md
│   └── project-scan-report.json    # Workflow state
│
└── _bmad/                          # BMAD workflow tooling
```

## Critical Directories

### `Message/HereAndNow.Message/`

**Purpose:** Demo business logic assembly (Auth0 sample).

**Contains:**
- Domain model (`Message`)
- Service interface (`IMessageService`)
- Service implementation (`MessageService`) with static messages

**Key Design Decision:** No web dependencies; pure business logic for demonstration.

### `Task/HereAndNow.Task/`

**Purpose:** Core business logic assembly with CosmosDB persistence.

**Contains:**
- Domain models (`TaskDocument`, `TaskReminderDocument`, `TaskState`, `PagedResult`)
- Custom exceptions for business rule violations
- Repository interfaces and implementations for CosmosDB
- Service interfaces and implementations for business logic

**Key Design Decisions:**
- Service-Repository pattern for separation of concerns
- Unity pattern for atomic Task-Reminder operations
- Denormalized `taskName` in reminders for efficient display
- State machine logic for task lifecycle

### `Web/HereAndNow.Web/`

**Purpose:** ASP.NET Core Web API project handling HTTP requests, authentication, and API documentation.

**Contains:**
- **Commands** - Command Pattern implementation for all mutations
- REST API controllers (`CommandsController`, `MessagesController`, `TasksController`, `RemindersController`)
- DTOs for request/response shaping
- Mappers for Document↔DTO conversion
- Custom validation attributes
- Middleware for error handling and security headers

**Entry Point:** `Program.cs` - Application bootstrap and configuration

### `Web/HereAndNow.Web/Commands/` (NEW)

**Purpose:** Command Pattern implementation for intent-based API operations.

**Contains:**
- `CommandRequest.cs` - Base request structure with `command` and `payload`
- `CommandResponse.cs` - Base response structure
- Individual command classes with validation attributes

**Key Design Decisions:**
- Client-generated IDs for optimistic UI patterns
- Single endpoint (`POST /api/v1/commands`) for all mutations
- Explicit intent over HTTP verb semantics

### `Web/HereAndNow.Web.Tests/`

**Purpose:** Test project containing unit and integration tests.

**Contains:**
- Unit tests for controllers (including CommandsController) and services
- Integration tests for full HTTP pipeline
- Test infrastructure (mock auth, factory)

### `.github/workflows/`

**Purpose:** CI/CD pipeline configuration for GitHub Actions.

**Contains:**
- Build, test, and deploy workflow to Azure Web Apps
- Test result publishing
- Code coverage collection

## Entry Points

### Main Entry Point

- **File:** `Web/HereAndNow.Web/Program.cs`
- **Description:** Application bootstrap, service registration, middleware configuration, CosmosDB setup, and Kestrel server startup.

### Test Entry Point

- **File:** `Web/HereAndNow.Web.Tests/Helpers/TestWebApplicationFactory.cs`
- **Description:** Custom `WebApplicationFactory` for integration testing with test configuration overrides.

## File Organization Patterns

### Controllers Pattern
- **Location:** `Web/HereAndNow.Web/Controllers/`
- **Pattern:** `{Feature}Controller.cs`
- **Current:** `CommandsController.cs`, `MessagesController.cs`, `TasksController.cs`, `RemindersController.cs`, `ErrorController.cs`

### Commands Pattern (NEW)
- **Location:** `Web/HereAndNow.Web/Commands/`
- **Pattern:** `{Action}{Feature}Command.cs`
- **Current:** `CreateTaskCommand`, `CreateTaskAndTaskReminderCommand`, `UpdateTaskNameCommand`, `UpdateTaskStateCommand`, `UpdateTaskReminderScheduledTimeCommand`, `DismissTaskReminderCommand`

### Services Pattern
- **Location:** `Task/HereAndNow.Task/Services/` or `Message/HereAndNow.Message/Services/`
- **Pattern:** `I{Feature}Service.cs` (interface), `{Feature}Service.cs` (implementation)
- **Current:** `ITaskService`, `TaskService`, `ITaskReminderService`, `TaskReminderService`, `IMessageService`, `MessageService`

### Repositories Pattern
- **Location:** `Task/HereAndNow.Task/Repositories/`
- **Pattern:** `I{Feature}Repository.cs` (interface), `{Feature}Repository.cs` (implementation)
- **Current:** `ITaskRepository`, `TaskRepository`, `ITaskReminderRepository`, `TaskReminderRepository`

### Models Pattern
- **Location:** `Task/HereAndNow.Task/Models/` or `Message/HereAndNow.Message/Models/`
- **Pattern:** `{ModelName}.cs` or `{ModelName}Document.cs` (for CosmosDB entities)
- **Current:** `TaskDocument`, `TaskReminderDocument`, `TaskState`, `PagedResult`, `Message`

### DTOs Pattern
- **Location:** `Web/HereAndNow.Web/DTOs/`
- **Pattern:** `{Action}{Feature}Dto.cs` or `{Feature}Dto.cs`
- **Current:** `CreateTaskDto`, `TaskDto`, `PagedTasksDto`, `TaskAndReminderDto`, etc.

### Mappers Pattern
- **Location:** `Web/HereAndNow.Web/Mappers/`
- **Pattern:** `{Feature}Mapper.cs`
- **Current:** `TaskMapper`, `ReminderMapper`

### Tests Pattern
- **Location:** `Web/HereAndNow.Web.Tests/`
- **Patterns:**
  - `Controllers/{Controller}Tests.cs` - Controller unit tests
  - `Services/{Service}Tests.cs` - Service unit tests
  - `Integration/{Feature}ApiTests.cs` - Integration tests

## Configuration Files

| File | Description |
|------|-------------|
| `HereAndNow.sln` | Visual Studio solution file |
| `*.csproj` | Project files with dependencies |
| `.env` | Environment variables (PORT, AUTH0_*, COSMOS_*) |
| `.github/workflows/*.yml` | CI/CD pipeline definitions |
| `.gitignore` | Git ignore patterns |
| `CLAUDE.md` | Claude Code AI assistant instructions |

## Notes for Development

1. **Solution Structure:** Open `HereAndNow.sln` in Visual Studio or Rider for full IDE support.

2. **Layer Dependencies:**
   ```
   Web → Task, Message (one-way)
   Task → (none - independent)
   Message → (none - independent)
   ```

3. **Adding New Commands:**
   - Add command class to `Web/HereAndNow.Web/Commands/`
   - Add handler case to `CommandsController.ExecuteCommand()`
   - Add service method (if needed) to `Task/HereAndNow.Task/Services/`
   - Add repository method (if needed) to `Task/HereAndNow.Task/Repositories/`
   - Add DTO (if needed) to `Web/HereAndNow.Web/DTOs/`
   - Add unit test to `Web/HereAndNow.Web.Tests/Controllers/CommandsControllerTests.cs`
   - Add integration test to `Web/HereAndNow.Web.Tests/Integration/CommandsApiTests.cs`

4. **Adding Legacy Features (deprecated approach):**
   - Add domain model to `Task/HereAndNow.Task/Models/`
   - Add exception (if needed) to `Task/HereAndNow.Task/Models/Exceptions/`
   - Add repository interface + implementation to `Task/HereAndNow.Task/Repositories/`
   - Add service interface + implementation to `Task/HereAndNow.Task/Services/`
   - Add DTO to `Web/HereAndNow.Web/DTOs/`
   - Add mapper to `Web/HereAndNow.Web/Mappers/`
   - Add controller endpoints to `Web/HereAndNow.Web/Controllers/`
   - Register services in DI (`Program.cs`)
   - Add tests to `Web/HereAndNow.Web.Tests/`

5. **Configuration:** All configuration is via environment variables loaded by `dotenv.net`.

6. **Deprecated:** The `Reminders/` folder at root level is empty and no longer used.

---

_Generated using BMAD Method `document-project` workflow_
_Last Updated: 2026-01-18_
