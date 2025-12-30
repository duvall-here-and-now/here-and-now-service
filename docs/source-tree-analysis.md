# Here and Now Service - Source Tree Analysis

**Date:** 2025-12-30

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
├── README.md                   # Project readme (Auth0 sample)
├── Message/                    # ★ Business Logic Assembly
│   └── HereAndNow.Message/
│       ├── HereAndNow.Message.csproj
│       ├── Models/             # Domain models
│       │   └── Message.cs      # Simple message model
│       └── Services/           # Service interfaces & implementations
│           ├── IMessageService.cs
│           └── MessageService.cs
├── Web/                        # ★ Web Layer Assemblies
│   ├── HereAndNow.Web/         # API Project
│   │   ├── Controllers/        # API endpoints
│   │   │   ├── ErrorController.cs
│   │   │   └── MessagesController.cs
│   │   ├── DTOs/               # (Empty - for future expansion)
│   │   ├── HereAndNow.Web.csproj
│   │   ├── Mappers/            # (Empty - for future expansion)
│   │   ├── Middlewares/        # Custom middleware
│   │   │   ├── ErrorHandlerMiddleware.cs
│   │   │   └── SecureHeadersMiddleware.cs
│   │   ├── Program.cs          # ★ Application entry point
│   │   └── SWAGGER_SETUP.md
│   └── HereAndNow.Web.Tests/   # Test Project
│       ├── Controllers/        # (Empty - for controller tests)
│       ├── HereAndNow.Web.Tests.csproj
│       ├── Helpers/            # Test infrastructure
│       │   └── TestWebApplicationFactory.cs
│       └── Integration/        # Integration tests
│           ├── AuthorizationTests.cs
│           └── CorsTests.cs
├── docs/                       # Generated documentation
└── _bmad/                      # BMAD workflow tooling
```

## Critical Directories

### `Message/HereAndNow.Message/`

**Purpose:** Business logic assembly containing domain models and service interfaces.

**Contains:**
- Domain model (`Message`)
- Service interface (`IMessageService`)
- Service implementation (`MessageService`) with static messages

**Key Design Decision:** This assembly has no web dependencies, allowing business logic to be tested and reused independently.

### `Web/HereAndNow.Web/`

**Purpose:** ASP.NET Core Web API project handling HTTP requests, authentication, and API documentation.

**Contains:**
- REST API controller (`MessagesController`)
- Error handling controller (`ErrorController`)
- Custom middleware for error handling and security headers
- Empty DTOs and Mappers folders for future expansion

**Entry Point:** `Program.cs` - Application bootstrap and configuration

### `Web/HereAndNow.Web.Tests/`

**Purpose:** Test project containing unit and integration tests.

**Contains:**
- Integration test classes (currently empty method bodies)
- Test infrastructure (`TestWebApplicationFactory`)
- Empty Controllers folder for unit tests

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
- **Current:** `MessagesController.cs`, `ErrorController.cs`

### Services Pattern
- **Location:** `Message/HereAndNow.Message/Services/`
- **Pattern:** `I{Feature}Service.cs` (interface), `{Feature}Service.cs` (implementation)
- **Purpose:** Business logic abstraction and implementation
- **Current:** `IMessageService.cs`, `MessageService.cs`

### Models Pattern
- **Location:** `Message/HereAndNow.Message/Models/`
- **Pattern:** `{ModelName}.cs`
- **Purpose:** Domain models
- **Current:** `Message.cs`

### Tests Pattern
- **Location:** `Web/HereAndNow.Web.Tests/`
- **Patterns:**
  - `Controllers/{Controller}Tests.cs` - Unit tests
  - `Integration/{Feature}Tests.cs` - Integration tests
- **Current:** `AuthorizationTests.cs`, `CorsTests.cs`

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

2. **Layer Dependencies:** Web → Message (one-way dependency; Message has no knowledge of Web)

3. **Adding New Features:**
   - Add domain model to `Message/HereAndNow.Message/Models/`
   - Add service interface to `Message/HereAndNow.Message/Services/`
   - Add service implementation to `Message/HereAndNow.Message/Services/`
   - Register service in DI (`Program.cs`)
   - Add controller to `Web/HereAndNow.Web/Controllers/`
   - Add tests to `Web/HereAndNow.Web.Tests/`

4. **Configuration:** All configuration is via environment variables loaded by `dotenv.net`.

---

_Generated using BMAD Method `document-project` workflow_
_Last Updated: 2025-12-30_
