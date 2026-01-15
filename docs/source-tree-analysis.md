# Here and Now Service - Source Tree Analysis

**Date:** 2026-01-15

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
│       │       ├── ReminderNotFoundException.cs
│       │       ├── ReminderAlreadyExistsException.cs
│       │       ├── ReminderAlreadyDismissedException.cs
│       │       ├── InvalidScheduledTimeException.cs
│       │       └── UnityTransactionFailedException.cs
│       ├── Repositories/
│       │   ├── ITaskRepository.cs
│       │   ├── TaskRepository.cs       # CosmosDB operations for tasks
│       │   ├── ITaskReminderRepository.cs
│       │   ├── TaskReminderRepository.cs # CosmosDB operations for reminders
│       │   └── CosmosDbSettings.cs     # Configuration class
│       └── Services/
│           ├── ITaskService.cs
│           ├── TaskService.cs          # Task business logic
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
│   │   │   ├── TasksController.cs      # Task CRUD + Unity operations
│   │   │   └── RemindersController.cs  # Reminder CRUD + snooze/dismiss
│   │   ├── DTOs/
│   │   │   ├── CreateTaskDto.cs
│   │   │   ├── UpdateTaskDto.cs
│   │   │   ├── TaskDto.cs
│   │   │   ├── PagedTasksDto.cs
│   │   │   ├── CreateReminderDto.cs
│   │   │   ├── SnoozeReminderDto.cs
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
│       │   ├── TasksControllerTests.cs
│       │   └── RemindersControllerTests.cs
│       ├── Services/
│       │   ├── TaskServiceTests.cs
│       │   └── TaskReminderServiceTests.cs
│       ├── Integration/
│       │   ├── AuthorizationTests.cs
│       │   ├── CorsTests.cs
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

### `Web/HereAndNow.Web/`

**Purpose:** ASP.NET Core Web API project handling HTTP requests, authentication, and API documentation.

**Contains:**
- REST API controllers (`MessagesController`, `TasksController`, `RemindersController`)
- DTOs for request/response shaping
- Mappers for Document↔DTO conversion
- Custom validation attributes
- Middleware for error handling and security headers

**Entry Point:** `Program.cs` - Application bootstrap and configuration

### `Web/HereAndNow.Web.Tests/`

**Purpose:** Test project containing unit and integration tests.

**Contains:**
- Unit tests for controllers and services
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
- **Current:** `MessagesController.cs`, `TasksController.cs`, `RemindersController.cs`, `ErrorController.cs`

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
- **Current:** `CreateTaskDto`, `UpdateTaskDto`, `TaskDto`, `PagedTasksDto`, etc.

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

3. **Adding New Task Features:**
   - Add domain model to `Task/HereAndNow.Task/Models/`
   - Add exception (if needed) to `Task/HereAndNow.Task/Models/Exceptions/`
   - Add repository interface + implementation to `Task/HereAndNow.Task/Repositories/`
   - Add service interface + implementation to `Task/HereAndNow.Task/Services/`
   - Add DTO to `Web/HereAndNow.Web/DTOs/`
   - Add mapper to `Web/HereAndNow.Web/Mappers/`
   - Add controller endpoints to `Web/HereAndNow.Web/Controllers/`
   - Register services in DI (`Program.cs`)
   - Add tests to `Web/HereAndNow.Web.Tests/`

4. **Configuration:** All configuration is via environment variables loaded by `dotenv.net`.

5. **Deprecated:** The `Reminders/` folder at root level is empty and no longer used.

---

_Generated using BMAD Method `document-project` workflow_
_Last Updated: 2026-01-15_
