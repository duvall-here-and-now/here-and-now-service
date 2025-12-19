# Source Tree Analysis

> Complete directory structure and file-level documentation

---

## Project Structure Overview

The Here and Now Service follows a **Clean Architecture** pattern with clear separation between business logic and infrastructure concerns. The solution is organized as a multi-project .NET solution with three assemblies.

```
here-and-now-service/
├── .github/                          # GitHub configuration
│   ├── agents/
│   │   └── dotnet-code-reviewer.md   # AI code review agent
│   ├── chatmodes/                    # VSCode/Copilot chat modes
│   │   └── bmad-agent-*.chatmode.md  # BMAD agent configurations
│   ├── workflows/
│   │   └── main_here-and-now-service.yml  # CI/CD pipeline
│   └── copilot-instructions.md       # GitHub Copilot config
│
├── docs/                             # Generated documentation
│   ├── index.md                      # Documentation entry point
│   ├── project-overview.md           # Project summary
│   ├── architecture.md               # System architecture
│   ├── api-contracts.md              # API endpoint documentation
│   ├── data-models.md                # Data model documentation
│   ├── source-tree-analysis.md       # This file
│   ├── development-guide.md          # Local setup instructions
│   ├── deployment-guide.md           # Deployment documentation
│   ├── bmm-workflow-status.yaml      # BMM workflow tracking
│   └── project-scan-report.json      # Scan state for resumability
│
├── Reminders/                        # Domain/Business Logic Layer
│   └── HereAndNow.Reminders/         # Business logic assembly
│       ├── Models/                   # Domain models
│       │   ├── Message.cs            # API message model (12 LOC)
│       │   └── ReminderInstance.cs   # Core reminder entity (48 LOC)
│       ├── Services/                 # Service interfaces & implementations
│       │   ├── IMessageService.cs    # Message service interface (10 LOC)
│       │   ├── IReminderInstanceService.cs  # Reminder service interface (47 LOC)
│       │   ├── MessageService.cs     # Message service implementation (21 LOC)
│       │   ├── ReminderInstanceService.cs   # In-memory implementation (159 LOC)
│       │   └── CosmosReminderInstanceService.cs  # Cosmos DB implementation (248 LOC)
│       ├── Persistence/              # Data access layer
│       │   └── ReminderDocument.cs   # Cosmos DB document mapping (86 LOC)
│       ├── Exceptions/               # Custom exceptions
│       │   └── ServiceUnavailableException.cs  # Service failure exception (35 LOC)
│       └── HereAndNow.Reminders.csproj
│
├── Web/                              # Infrastructure/Presentation Layer
│   ├── HereAndNow.Web/               # ASP.NET Core Web API
│   │   ├── Controllers/              # API controllers
│   │   │   ├── ErrorController.cs    # Global error handling (25 LOC)
│   │   │   ├── MessagesController.cs # Message endpoints (63 LOC)
│   │   │   └── ReminderInstancesController.cs  # CRUD endpoints (172 LOC)
│   │   ├── DTOs/                     # Data transfer objects
│   │   │   ├── CreateReminderRequest.cs  # Create DTO with validation (33 LOC)
│   │   │   ├── UpdateReminderRequest.cs  # Partial update DTO (32 LOC)
│   │   │   ├── ReminderInstanceDto.cs    # Response DTO with computed State (68 LOC)
│   │   │   └── ReminderState.cs          # State enum (27 LOC)
│   │   ├── Mappers/                  # DTO mapping
│   │   │   └── ReminderInstanceMapper.cs  # Domain ↔ DTO mapper (58 LOC)
│   │   ├── Middlewares/              # Custom middleware
│   │   │   ├── ErrorHandlerMiddleware.cs   # Error response handling (73 LOC)
│   │   │   └── SecureHeadersMiddleware.cs  # Security headers (32 LOC)
│   │   ├── Configuration/            # Settings classes
│   │   │   └── CosmosDbSettings.cs   # Cosmos DB configuration (27 LOC)
│   │   ├── Properties/
│   │   │   └── launchSettings.json   # Development launch profiles
│   │   ├── Program.cs                # Application entry point (199 LOC)
│   │   ├── HereAndNow.Web.csproj     # Web project file
│   │   └── SWAGGER_SETUP.md          # Swagger documentation
│   │
│   └── HereAndNow.Web.Tests/         # Test project
│       ├── Controllers/              # Controller unit tests
│       │   └── ReminderInstancesControllerTests.cs  # 13 unit tests (303 LOC)
│       ├── Helpers/                  # Test utilities
│       │   └── TestWebApplicationFactory.cs  # Integration test factory (25 LOC)
│       ├── Integration/              # Integration tests
│       │   ├── AuthorizationTests.cs # 5 auth integration tests (85 LOC)
│       │   └── CorsTests.cs          # CORS integration tests
│       └── HereAndNow.Web.Tests.csproj
│
├── .bmad/                            # BMad Method workflow system
│   ├── bmm/                          # BMad Method Module
│   │   ├── agents/                   # Agent personas
│   │   ├── config.yaml               # BMM configuration
│   │   └── workflows/                # Workflow definitions
│   └── core/                         # Core framework
│       └── tasks/                    # Task definitions
│
├── HereAndNow.sln                    # Solution file
├── CLAUDE.md                         # Claude Code instructions
├── README.md                         # Project overview
├── .env                              # Environment variables (not in repo)
└── .gitignore                        # Git ignore patterns
```

---

## Critical Folders

### `/Reminders/HereAndNow.Reminders/`

**Purpose:** Domain layer containing pure business logic with no web framework dependencies.

**Key Characteristics:**
- Dependencies: `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Azure.Cosmos`, `Newtonsoft.Json`
- Defines interfaces that the Web layer consumes
- Contains domain models (`ReminderInstance`, `Message`)
- Houses both service implementations (Cosmos + in-memory)
- Contains persistence layer for Cosmos DB document mapping

**Why It Matters:**
- Enables unit testing without HTTP/web concerns
- Can be reused across different hosting models (API, Console, Worker)
- Follows Dependency Inversion Principle

### `/Web/HereAndNow.Web/`

**Purpose:** ASP.NET Core Web API hosting layer.

**Key Characteristics:**
- References `HereAndNow.Reminders` for business logic
- Handles HTTP, authentication, CORS, and Swagger
- Contains custom middleware for security and error handling
- Configures dependency injection container
- Contains DTOs for API responses (with computed `State`)
- Houses mapper for Domain ↔ DTO conversion

**Entry Point:** `Program.cs` (minimal hosting model)

### `/Web/HereAndNow.Web.Tests/`

**Purpose:** Automated testing for the API layer.

**Test Types:**
- **Unit Tests:** 13 controller tests with mocked services (ReminderInstancesControllerTests)
- **Integration Tests:** 5 auth tests with `WebApplicationFactory` (AuthorizationTests)

**Test Coverage Areas:**
- CRUD operations (GetAll, GetById, Create, Update, Delete)
- State computation (Scheduled, Active, Completed, Deleted)
- Authorization enforcement (401 without token)
- Error handling (404 for not found)

---

## Entry Points

### Application Entry Point

**File:** `Web/HereAndNow.Web/Program.cs:12`

This file orchestrates:
1. Environment variable loading via `dotenv.net`
2. Conditional Cosmos DB vs in-memory service registration
3. Authentication setup with Auth0 JWT Bearer
4. CORS configuration (multi-origin support)
5. Swagger/OpenAPI configuration with security scheme
6. Middleware pipeline setup
7. Kestrel server startup

**Service Registration Flow:**

```csharp
// Conditional persistence selection (Program.cs:35-62)
var useCosmosDb = !string.IsNullOrEmpty(cosmosSettings.Endpoint);

if (useCosmosDb)
{
    builder.Services.AddSingleton<CosmosClient>(...);
    builder.Services.AddScoped<IReminderInstanceService, CosmosReminderInstanceService>();
}
else
{
    builder.Services.AddSingleton<IReminderInstanceService, ReminderInstanceService>();
}
```

### Test Entry Points

**File:** `Web/HereAndNow.Web.Tests/Helpers/TestWebApplicationFactory.cs:7`

Custom `WebApplicationFactory<Program>` that:
- Overrides configuration with in-memory test values
- Sets environment to "Testing"
- Provides test client for integration tests

---

## Key File Locations

| Category | File | Description |
|----------|------|-------------|
| **Entry Point** | `Web/HereAndNow.Web/Program.cs` | App startup & DI setup |
| **Configuration** | `.env` (not in repo) | Environment secrets |
| **CI/CD** | `.github/workflows/main_here-and-now-service.yml` | GitHub Actions |
| **API Docs** | `/swagger` endpoint | OpenAPI documentation |
| **Solution** | `HereAndNow.sln` | Visual Studio solution |
| **Domain Models** | `Reminders/.../Models/` | Business entities |
| **Services** | `Reminders/.../Services/` | Business logic |
| **Controllers** | `Web/.../Controllers/` | API endpoints |
| **DTOs** | `Web/.../DTOs/` | API response models |
| **Tests** | `Web/.../Tests/` | Unit & integration tests |

---

## Namespace Structure

| Namespace | Assembly | Purpose |
|-----------|----------|---------|
| `HereAndNowService.Models` | HereAndNow.Reminders | Domain entities |
| `HereAndNowService.Services` | HereAndNow.Reminders | Business logic |
| `HereAndNowService.Persistence` | HereAndNow.Reminders | Data access |
| `HereAndNowService.Exceptions` | HereAndNow.Reminders | Custom exceptions |
| `HereAndNowService.Controllers` | HereAndNow.Web | API endpoints |
| `HereAndNowService.DTOs` | HereAndNow.Web | Transfer objects |
| `HereAndNowService.Mappers` | HereAndNow.Web | Object mapping |
| `HereAndNowService.Middlewares` | HereAndNow.Web | Request pipeline |
| `HereAndNowService.Configuration` | HereAndNow.Web | Settings classes |

---

## Dependency Flow

```
┌───────────────────────────────────────────────────────────────────────┐
│                         HereAndNow.Web                                 │
│  ┌──────────────┐  ┌──────────┐  ┌───────────┐  ┌─────────────────┐   │
│  │ Controllers  │─▶│   DTOs   │  │  Mappers  │  │   Middlewares   │   │
│  └──────────────┘  └──────────┘  └───────────┘  └─────────────────┘   │
│         │                                                              │
│         │    ┌──────────────────────────────────────────────────┐     │
│         └───▶│           Program.cs (DI Container)               │     │
│              │  - Conditional Cosmos/In-Memory registration      │     │
│              │  - Auth0 JWT Bearer setup                         │     │
│              │  - CORS multi-origin configuration                │     │
│              └──────────────────────────────────────────────────┘     │
└───────────────────────────┬───────────────────────────────────────────┘
                            │ Project Reference
                            ▼
┌───────────────────────────────────────────────────────────────────────┐
│                       HereAndNow.Reminders                            │
│  ┌──────────────┐  ┌───────────────────────────────────────────────┐  │
│  │    Models    │  │               Services                        │  │
│  │  - Message   │  │  ┌─────────────────────────────────────────┐  │  │
│  │  - Reminder  │  │  │      IReminderInstanceService           │  │  │
│  └──────────────┘  │  └─────────────────────────────────────────┘  │  │
│                    │         ▲                    ▲                 │  │
│  ┌──────────────┐  │         │                    │                 │  │
│  │ Persistence  │  │  ┌──────┴──────┐     ┌──────┴────────────┐    │  │
│  │ - Document   │◀─│──│ CosmosReminder│     │ ReminderInstance │    │  │
│  └──────────────┘  │  │   Service   │     │    Service       │    │  │
│                    │  │ (Production)│     │ (Dev/Test)       │    │  │
│  ┌──────────────┐  │  └─────────────┘     └──────────────────┘    │  │
│  │  Exceptions  │  │        │                     │                │  │
│  │ - Unavailable│  │        ▼                     ▼                │  │
│  └──────────────┘  │  ┌─────────────┐     ┌───────────────────┐    │  │
│                    │  │ Cosmos DB   │     │ ConcurrentDict    │    │  │
│                    │  └─────────────┘     └───────────────────┘    │  │
│                    └───────────────────────────────────────────────┘  │
└───────────────────────────────────────────────────────────────────────┘
```

---

## Files Excluded from Source Tree

- `/obj/` - Build artifacts
- `/bin/` - Output binaries
- `/.bmad/` - BMad workflow system (meta-tooling)
- `.env` - Environment secrets (in .gitignore)

---

## Source Code Statistics

| Metric | Count |
|--------|-------|
| **Total Source Files** | 22 |
| **Lines of Code (approx)** | ~1,900 |
| **Controllers** | 3 |
| **Services** | 4 (2 interfaces, 2 implementations with Cosmos + in-memory) |
| **Models** | 7 (2 domain, 4 DTOs, 1 persistence) |
| **Unit Tests** | 16 |
| **Integration Tests** | 7 |

---

## Related Documentation

- [Architecture](./architecture.md) - System architecture details
- [API Contracts](./api-contracts.md) - API endpoint documentation
- [Data Models](./data-models.md) - Domain model documentation
- [Development Guide](./development-guide.md) - Setup and development workflow

---

## Documentation Metadata

| Field | Value |
|-------|-------|
| **Generated** | 2025-12-19 |
| **Scan Level** | Exhaustive |
| **Workflow** | document-project v1.2.0 |
