# Here and Now Service - Source Tree Analysis

**Date:** 2025-12-29

## Overview

This document provides an annotated directory structure of the Here and Now Service codebase, highlighting critical directories, entry points, and key file locations.

## Complete Directory Structure

```
here-and-now-service/
├── .claude/                    # Claude Code configuration
│   └── commands/               # Custom slash commands
├── .env                        # Environment variables (not in git)
├── .github/                    # GitHub configuration
│   ├── agents/                 # AI agent definitions
│   │   └── dotnet-code-reviewer.md
│   ├── copilot-instructions.md
│   └── workflows/              # CI/CD pipelines
│       └── main_here-and-now-service.yml
├── .gitignore
├── .vs/                        # Visual Studio settings
├── .vscode/                    # VS Code settings
├── CLAUDE.md                   # Claude Code instructions
├── HereAndNow.sln              # ★ Solution file (entry point)
├── README.md                   # Project readme
├── Reminders/                  # ★ Business Logic Assembly
│   └── HereAndNow.Reminders/
│       ├── HereAndNow.Reminders.csproj
│       ├── Models/             # Domain models
│       │   ├── Message.cs
│       │   └── ReminderInstance.cs
│       └── Services/           # Service interfaces & implementations
│           ├── IMessageService.cs
│           ├── IReminderInstanceService.cs
│           ├── MessageService.cs
│           └── ReminderInstanceService.cs
├── Web/                        # ★ Web Layer Assemblies
│   ├── HereAndNow.Web/         # API Project
│   │   ├── Controllers/        # API endpoints
│   │   │   ├── ErrorController.cs
│   │   │   ├── MessagesController.cs
│   │   │   └── ReminderInstancesController.cs
│   │   ├── DTOs/               # Data Transfer Objects
│   │   │   ├── ReminderInstanceDto.cs
│   │   │   └── ReminderState.cs
│   │   ├── HereAndNow.Web.csproj
│   │   ├── Mappers/            # Domain ↔ DTO mappers
│   │   │   └── ReminderInstanceMapper.cs
│   │   ├── Middlewares/        # Custom middleware
│   │   │   ├── ErrorHandlerMiddleware.cs
│   │   │   └── SecureHeadersMiddleware.cs
│   │   ├── Program.cs          # ★ Application entry point
│   │   └── SWAGGER_SETUP.md
│   └── HereAndNow.Web.Tests/   # Test Project
│       ├── Controllers/        # Controller unit tests
│       │   └── ReminderInstancesControllerTests.cs
│       ├── HereAndNow.Web.Tests.csproj
│       ├── Helpers/            # Test infrastructure
│       │   └── TestWebApplicationFactory.cs
│       └── Integration/        # Integration tests
│           ├── AuthorizationTests.cs
│           └── CorsTests.cs
└── docs/                       # Generated documentation
```

## Critical Directories

### `Reminders/HereAndNow.Reminders/`

**Purpose:** Business logic assembly containing domain models and service interfaces.

**Contains:**
- Domain models (`Message`, `ReminderInstance`)
- Service interfaces (`IMessageService`, `IReminderInstanceService`)
- Service implementations with in-memory storage

**Key Design Decision:** This assembly has no web dependencies, allowing business logic to be tested and reused independently.

### `Web/HereAndNow.Web/`

**Purpose:** ASP.NET Core Web API project handling HTTP requests, authentication, and API documentation.

**Contains:**
- REST API controllers
- DTOs for API contracts (separate from domain models)
- Mappers for domain ↔ DTO conversion
- Custom middleware for error handling and security headers

**Entry Point:** `Program.cs` - Application bootstrap and configuration

### `Web/HereAndNow.Web.Tests/`

**Purpose:** Test project containing unit and integration tests.

**Contains:**
- Controller unit tests with mocked dependencies
- Integration tests for authorization and CORS
- Test infrastructure (`TestWebApplicationFactory`)

### `.github/workflows/`

**Purpose:** CI/CD pipeline configuration for GitHub Actions.

**Contains:**
- Build, test, and deploy workflow to Azure Web Apps

## Entry Points

### Main Entry Point

- **File:** `Web/HereAndNow.Web/Program.cs`
- **Description:** Application bootstrap, service registration, middleware configuration, and Kestrel server startup.

### Test Entry Point

- **File:** `Web/HereAndNow.Web.Tests/Helpers/TestWebApplicationFactory.cs`
- **Description:** Custom `WebApplicationFactory` for integration testing with test configuration overrides.

## File Organization Patterns

### Controllers Pattern
- **Location:** `Web/HereAndNow.Web/Controllers/`
- **Pattern:** `{Feature}Controller.cs`
- **Purpose:** REST API endpoints grouped by feature
- **Examples:** `MessagesController.cs`, `ReminderInstancesController.cs`

### Services Pattern
- **Location:** `Reminders/HereAndNow.Reminders/Services/`
- **Pattern:** `I{Feature}Service.cs` (interface), `{Feature}Service.cs` (implementation)
- **Purpose:** Business logic abstraction and implementation
- **Examples:** `IReminderInstanceService.cs`, `ReminderInstanceService.cs`

### DTOs Pattern
- **Location:** `Web/HereAndNow.Web/DTOs/`
- **Pattern:** `{Model}Dto.cs`
- **Purpose:** API contract objects separated from domain models
- **Examples:** `ReminderInstanceDto.cs`, `ReminderState.cs`

### Tests Pattern
- **Location:** `Web/HereAndNow.Web.Tests/`
- **Patterns:**
  - `Controllers/{Controller}Tests.cs` - Unit tests
  - `Integration/{Feature}Tests.cs` - Integration tests
- **Purpose:** Comprehensive test coverage
- **Examples:** `ReminderInstancesControllerTests.cs`, `AuthorizationTests.cs`

## Configuration Files

| File | Description |
|------|-------------|
| `HereAndNow.sln` | Visual Studio solution file |
| `*.csproj` | Project files with dependencies |
| `.env` | Environment variables (PORT, AUTH0_*, etc.) |
| `.github/workflows/*.yml` | CI/CD pipeline definitions |
| `.gitignore` | Git ignore patterns |

## Notes for Development

1. **Solution Structure:** Open `HereAndNow.sln` in Visual Studio or Rider for full IDE support.

2. **Layer Dependencies:** Web → Reminders (one-way dependency; Reminders has no knowledge of Web)

3. **Adding New Features:**
   - Add domain model to `Reminders/Models/`
   - Add service interface to `Reminders/Services/`
   - Add DTO to `Web/DTOs/`
   - Add mapper to `Web/Mappers/`
   - Add controller to `Web/Controllers/`
   - Add tests to `Web.Tests/`

4. **Configuration:** All configuration is via environment variables loaded by `dotenv.net`.

---

_Generated using BMAD Method `document-project` workflow_
