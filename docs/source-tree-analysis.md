# Source Tree Analysis

## Project Structure Overview

The Here and Now Service follows a **Clean Architecture** pattern with clear separation between business logic and infrastructure concerns. The solution is organized as a multi-project .NET solution with three assemblies.

```
here-and-now-service/
├── .github/                          # GitHub configuration
│   ├── agents/
│   │   └── dotnet-code-reviewer.md   # AI code review agent
│   ├── workflows/
│   │   └── main_here-and-now-service.yml  # CI/CD pipeline
│   └── copilot-instructions.md       # GitHub Copilot config
│
├── docs/                             # Generated documentation
│   ├── index.md                      # Documentation entry point
│   ├── api-contracts.md              # API endpoint documentation
│   ├── data-models.md                # Data model documentation
│   └── ...                           # Other documentation files
│
├── Reminders/                        # Domain/Business Logic Layer
│   └── HereAndNow.Reminders/         # Business logic assembly
│       ├── Models/                   # Domain models
│       │   ├── Message.cs            # API message model
│       │   ├── ReminderInstance.cs   # Core reminder entity
│       │   └── ReminderStatus.cs     # Status enumeration
│       ├── Services/                 # Service interfaces & implementations
│       │   ├── IMessageService.cs    # Message service interface
│       │   ├── IReminderInstanceService.cs  # Reminder service interface
│       │   ├── MessageService.cs     # Message service implementation
│       │   └── ReminderInstanceService.cs   # Reminder service (in-memory)
│       └── HereAndNow.Reminders.csproj
│
├── Web/                              # Infrastructure/Presentation Layer
│   ├── HereAndNow.Web/               # ASP.NET Core Web API
│   │   ├── Controllers/              # API controllers
│   │   │   ├── ErrorController.cs    # Global error handling
│   │   │   ├── MessagesController.cs # Message endpoints
│   │   │   └── ReminderInstancesController.cs  # CRUD endpoints
│   │   ├── Middlewares/              # Custom middleware
│   │   │   ├── ErrorHandlerMiddleware.cs     # Error response handling
│   │   │   └── SecureHeadersMiddleware.cs    # Security headers
│   │   ├── Properties/
│   │   │   └── launchSettings.json   # Development launch profiles
│   │   ├── appsettings.json          # Production configuration
│   │   ├── appsettings.Development.json  # Development overrides
│   │   ├── Program.cs                # Application entry point & DI setup
│   │   ├── HereAndNow.Web.csproj     # Web project file
│   │   └── SWAGGER_SETUP.md          # Swagger documentation
│   │
│   └── HereAndNow.Web.Tests/         # Test project
│       ├── Controllers/              # Controller unit tests
│       │   └── ReminderInstancesControllerTests.cs
│       ├── Helpers/                  # Test utilities
│       │   └── TestWebApplicationFactory.cs  # Integration test factory
│       ├── Integration/              # Integration tests
│       │   ├── AuthorizationTests.cs # Auth integration tests
│       │   └── CorsTests.cs          # CORS integration tests
│       └── HereAndNow.Web.Tests.csproj
│
├── HereAndNow.sln                    # Solution file
├── CLAUDE.md                         # Claude Code instructions
├── README.md                         # Project overview
└── .gitignore                        # Git ignore patterns
```

---

## Critical Folders

### `/Reminders/HereAndNow.Reminders/`

**Purpose:** Domain layer containing pure business logic with no web framework dependencies.

**Key Characteristics:**
- Only dependency is `Microsoft.Extensions.Logging.Abstractions`
- Defines interfaces that the Web layer implements/uses
- Contains domain models (`ReminderInstance`, `Message`, `ReminderStatus`)
- Houses service implementations for business operations

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

**Entry Point:** `Program.cs` (minimal hosting model)

### `/Web/HereAndNow.Web.Tests/`

**Purpose:** Automated testing for the API layer.

**Test Types:**
- **Unit Tests:** Controller behavior testing with mocked services
- **Integration Tests:** Full HTTP request/response testing with `WebApplicationFactory`

---

## Entry Points

### Application Entry Point

**File:** `Web/HereAndNow.Web/Program.cs`

This file:
1. Loads environment variables via `dotenv.net`
2. Configures DI container (services, authentication)
3. Sets up middleware pipeline
4. Configures Swagger/OpenAPI
5. Starts the Kestrel web server

```csharp
// Key startup order in Program.cs
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddSingleton<IReminderInstanceService, ReminderInstanceService>();
// ... authentication, CORS, Swagger setup
app.UseErrorHandler();
app.UseSecureHeaders();
app.MapControllers();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
```

### Test Entry Points

**File:** `Web/HereAndNow.Web.Tests/Helpers/TestWebApplicationFactory.cs`

Custom `WebApplicationFactory<Program>` for integration tests.

---

## Key File Locations

| Category | File | Description |
|----------|------|-------------|
| **Configuration** | `Web/HereAndNow.Web/appsettings.json` | App settings |
| **Configuration** | `.env` (not in repo) | Environment secrets |
| **CI/CD** | `.github/workflows/main_here-and-now-service.yml` | GitHub Actions |
| **API Docs** | `/swagger` endpoint | OpenAPI documentation |
| **Solution** | `HereAndNow.sln` | Visual Studio solution |

---

## Namespace Structure

| Namespace | Assembly | Purpose |
|-----------|----------|---------|
| `HereAndNowService.Models` | HereAndNow.Reminders | Domain entities |
| `HereAndNowService.Services` | HereAndNow.Reminders | Business logic |
| `HereAndNowService.Controllers` | HereAndNow.Web | API endpoints |
| `HereAndNowService.Middlewares` | HereAndNow.Web | Request pipeline |

---

## Dependency Flow

```
┌───────────────────────────────────────────────────────────┐
│                     HereAndNow.Web                        │
│  ┌──────────────┐  ┌──────────────┐  ┌─────────────────┐  │
│  │ Controllers  │─▶│  Program.cs  │◀─│   Middlewares   │  │
│  └──────────────┘  │  (DI Setup)  │  └─────────────────┘  │
│         │          └──────────────┘                       │
│         │                │                                │
│         ▼                ▼                                │
│  ┌─────────────────────────────────────────────────────┐  │
│  │              Service Interfaces                     │  │
│  │       IMessageService, IReminderService             │  │
│  └─────────────────────────────────────────────────────┘  │
└───────────────────────────┬───────────────────────────────┘
                            │ Project Reference
                            ▼
┌───────────────────────────────────────────────────────────┐
│                  HereAndNow.Reminders                     │
│  ┌──────────────┐  ┌───────────────────────────────────┐  │
│  │    Models    │  │            Services               │  │
│  │  - Message   │  │  - MessageService                 │  │
│  │  - Reminder  │  │  - ReminderInstanceService        │  │
│  │  - Status    │  │    (ConcurrentDictionary storage) │  │
│  └──────────────┘  └───────────────────────────────────┘  │
└───────────────────────────────────────────────────────────┘
```

---

## Files Excluded from Source Tree

- `/obj/` - Build artifacts
- `/bin/` - Output binaries
- `/.bmad/` - BMad workflow system (meta-tooling)
- `/node_modules/` - N/A (not a Node.js project)

---

## Related Documentation

- [Architecture](./architecture.md) - System architecture details
- [API Contracts](./api-contracts.md) - API endpoint documentation
- [Data Models](./data-models.md) - Domain model documentation
- [Development Guide](./development-guide.md) - Setup and development workflow
