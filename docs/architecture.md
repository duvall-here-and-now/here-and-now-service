# Here and Now Service - Architecture

**Date:** 2025-12-29
**Version:** 1.0
**Architecture Pattern:** Clean Architecture with DTO Pattern

## Executive Summary

The Here and Now Service is a RESTful API built on ASP.NET Core 8.0 that provides reminder management capabilities. It follows Clean Architecture principles with clear separation between the business logic layer (Reminders assembly) and the web API layer (Web assembly). Authentication is handled via Auth0 JWT tokens.

## Technology Stack

| Category | Technology | Version | Justification |
|----------|------------|---------|---------------|
| **Runtime** | .NET | 8.0 | LTS release, modern C# features, excellent performance |
| **Framework** | ASP.NET Core | 8.0 | Industry standard for .NET APIs |
| **Language** | C# | 12 | Modern features (required, pattern matching) |
| **Auth** | Auth0 + JWT Bearer | 8.0.11 | Managed identity, secure token validation |
| **API Docs** | Swashbuckle | 6.9.0 | Swagger/OpenAPI generation |
| **Env Config** | dotenv.net | 3.2.1 | 12-factor app configuration |
| **Testing** | xUnit | 2.9.2 | Popular .NET testing framework |
| **Mocking** | Moq | 4.20.72 | Flexible mocking for unit tests |
| **Assertions** | FluentAssertions | 6.12.0 | Readable test assertions |
| **Integration Testing** | Mvc.Testing | 8.0.11 | In-memory test server |

## Architecture Pattern

### Clean Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                          Web Layer                          │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐  │
│  │ Controllers │  │ Middlewares │  │ DTOs + Mappers      │  │
│  │             │  │             │  │                     │  │
│  │ • Messages  │  │ • ErrorHdlr │  │ • ReminderInstDto   │  │
│  │ • Reminders │  │ • SecureHdr │  │ • ReminderInstMappr │  │
│  └──────┬──────┘  └─────────────┘  └─────────────────────┘  │
│         │                                                   │
│         │ Depends On                                        │
└─────────┼───────────────────────────────────────────────────┘
          │
          ▼
┌─────────────────────────────────────────────────────────────┐
│                    Business Logic Layer                     │
│  ┌─────────────────────┐  ┌───────────────────────────────┐ │
│  │       Models        │  │           Services            │ │
│  │                     │  │                               │ │
│  │ • ReminderInstance  │  │ • IReminderInstanceService    │ │
│  │ • Message           │  │ • ReminderInstanceService     │ │
│  │                     │  │ • IMessageService             │ │
│  │                     │  │ • MessageService              │ │
│  └─────────────────────┘  └───────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

### Key Principles

1. **Dependency Inversion**: Web layer depends on abstractions (interfaces) defined in business layer
2. **Separation of Concerns**: Business logic has no web dependencies
3. **DTO Pattern**: API contracts (DTOs) are separate from domain models
4. **Testability**: Each layer can be tested independently

## Component Architecture

### Assembly Structure

```
HereAndNow.sln
├── HereAndNow.Reminders     (Business Logic)
│   ├── Models/
│   └── Services/
├── HereAndNow.Web           (Web API)
│   ├── Controllers/
│   ├── DTOs/
│   ├── Mappers/
│   └── Middlewares/
└── HereAndNow.Web.Tests     (Tests)
    ├── Controllers/
    ├── Integration/
    └── Helpers/
```

### Dependency Graph

```
HereAndNow.Web.Tests
    │
    ├──► HereAndNow.Web
    │        │
    │        └──► HereAndNow.Reminders
    │
    └──► HereAndNow.Reminders
```

## Request Flow

```
HTTP Request
     │
     ▼
┌─────────────────┐
│  Kestrel Server │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Error Handler   │ ◄── Catches exceptions, formats error responses
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
│   Controller    │ ◄── Handles request, calls service
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│    Service      │ ◄── Business logic, data operations
└────────┬────────┘
         │
         ▼
HTTP Response
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
                               └──────────────────┘
```

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

## Data Architecture

### Current: In-Memory Storage

```csharp
private readonly ConcurrentDictionary<Guid, ReminderInstance> _reminders = new();
```

- Thread-safe concurrent access
- Data lost on restart
- Suitable for development/demo

### Future: Cosmos DB (Indicated)

Domain model comments indicate planned Cosmos DB integration:
```csharp
/// This model maps directly to the Cosmos DB storage schema.
```

## API Design

### RESTful Conventions

| Operation | HTTP Method | Endpoint | Response |
|-----------|-------------|----------|----------|
| List | GET | /api/reminder-instances | 200 + array |
| Get | GET | /api/reminder-instances/{id} | 200 / 404 |
| Create | POST | /api/reminder-instances | 201 + Location |
| Update | PUT | /api/reminder-instances/{id} | 200 / 404 |
| Delete | DELETE | /api/reminder-instances/{id} | 204 / 404 |

### Soft Delete Pattern

Reminders use soft delete - the `IsDeleted` flag is set to true rather than removing records:

```csharp
public bool Delete(Guid id)
{
    // Sets IsDeleted = true, doesn't remove from storage
    var updatedReminder = new ReminderInstance { ..., IsDeleted = true };
    _reminders.TryUpdate(id, updatedReminder, existingReminder);
}
```

## Dependency Injection

### Service Registration (Program.cs)

```csharp
// Scoped - new instance per request
builder.Services.AddScoped<IMessageService, MessageService>();

// Singleton - single instance for app lifetime
builder.Services.AddSingleton<IReminderInstanceService, ReminderInstanceService>();
```

### Lifetime Rationale

| Service | Lifetime | Reason |
|---------|----------|--------|
| IMessageService | Scoped | Stateless, per-request is fine |
| IReminderInstanceService | Singleton | Holds in-memory data store |

## Testing Strategy

### Test Pyramid

```
         ┌─────────┐
         │   E2E   │  (Manual via Swagger)
         ├─────────┤
         │ Integr. │  CorsTests, AuthorizationTests
         ├─────────┤
         │  Unit   │  ReminderInstancesControllerTests
         └─────────┘
```

### Test Infrastructure

- **TestWebApplicationFactory**: Custom factory with test configuration
- **Moq**: Mock service dependencies in controller tests
- **FluentAssertions**: Readable test assertions

## Deployment Architecture

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│   GitHub        │────►│  GitHub Actions │────►│   Azure Web     │
│   Repository    │     │  Build + Test   │     │   App Service   │
└─────────────────┘     └─────────────────┘     └─────────────────┘
                                                        │
                                                        ▼
                                               ┌─────────────────┐
                                               │    Auth0        │
                                               │    (Identity)   │
                                               └─────────────────┘
```

### CI/CD Pipeline

1. **Trigger**: Push to `main` branch
2. **Build**: `dotnet build --configuration Release`
3. **Test**: `dotnet test` with code coverage
4. **Publish**: `dotnet publish`
5. **Deploy**: Azure Web Apps Deploy action

## Configuration

### Environment Variables

| Variable | Required | Description |
|----------|----------|-------------|
| PORT | Yes | Server port number |
| CLIENT_ORIGIN_URL | Yes | Allowed CORS origins (comma-separated) |
| AUTH0_DOMAIN | Yes | Auth0 tenant domain |
| AUTH0_AUDIENCE | Yes | Auth0 API identifier |

### Loading Mechanism

```csharp
DotEnv.Load();
builder.Configuration.AddEnvironmentVariables();
```

## Future Considerations

1. **Database Integration**: Cosmos DB for persistent storage
2. **Caching**: Redis for performance optimization
3. **Background Jobs**: Hangfire for reminder notifications
4. **Observability**: Application Insights for monitoring
5. **Rate Limiting**: Protect API from abuse

---

_Generated using BMAD Method `document-project` workflow_
