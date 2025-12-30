# Here and Now Service - Architecture

**Date:** 2025-12-30
**Version:** 2.0
**Architecture Pattern:** Clean Architecture (2-Layer)

## Executive Summary

The Here and Now Service is a RESTful API built on ASP.NET Core 8.0 that demonstrates Auth0 authentication with different access levels (public, protected, admin). It follows Clean Architecture principles with clear separation between the business logic layer (Message assembly) and the web API layer (Web assembly).

## Technology Stack

| Category | Technology | Version | Justification |
|----------|------------|---------|---------------|
| **Runtime** | .NET | 8.0 | LTS release, modern C# features, excellent performance |
| **Framework** | ASP.NET Core | 8.0 | Industry standard for .NET APIs |
| **Language** | C# | 12 | Modern features (file-scoped namespaces, nullable) |
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
│  │             │  │             │  │ (empty - future)    │  │
│  │ • Messages  │  │ • ErrorHdlr │  │                     │  │
│  │ • Error     │  │ • SecureHdr │  │                     │  │
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
│  │ • Message           │  │ • IMessageService             │ │
│  │                     │  │ • MessageService              │ │
│  └─────────────────────┘  └───────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

### Key Principles

1. **Dependency Inversion**: Web layer depends on abstractions (interfaces) defined in business layer
2. **Separation of Concerns**: Business logic has no web dependencies
3. **Testability**: Each layer can be tested independently
4. **Simplicity**: Minimal complexity for demo/sample API purposes

## Component Architecture

### Assembly Structure

```
HereAndNow.sln
├── HereAndNow.Message       (Business Logic)
│   ├── Models/
│   │   └── Message.cs
│   └── Services/
│       ├── IMessageService.cs
│       └── MessageService.cs
├── HereAndNow.Web           (Web API)
│   ├── Controllers/
│   │   ├── MessagesController.cs
│   │   └── ErrorController.cs
│   ├── DTOs/                (empty)
│   ├── Mappers/             (empty)
│   └── Middlewares/
│       ├── ErrorHandlerMiddleware.cs
│       └── SecureHeadersMiddleware.cs
└── HereAndNow.Web.Tests     (Tests)
    ├── Controllers/         (empty)
    ├── Integration/
    │   ├── AuthorizationTests.cs
    │   └── CorsTests.cs
    └── Helpers/
        └── TestWebApplicationFactory.cs
```

### Dependency Graph

```
HereAndNow.Web.Tests
    │
    ├──► HereAndNow.Web
    │        │
    │        └──► HereAndNow.Message
    │
    └──► HereAndNow.Message
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
│   Controller    │ ◄── MessagesController
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│    Service      │ ◄── MessageService (static messages)
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

## API Design

### Endpoints

| Endpoint | Auth | Description |
|----------|------|-------------|
| GET /api/messages/public | None | Public message (demo) |
| GET /api/messages/protected | JWT | Protected message (demo) |
| GET /api/messages/admin | JWT | Admin message (demo) |

### Response Format

All endpoints return the same schema:
```json
{
  "text": "Message content"
}
```

## Dependency Injection

### Service Registration (Program.cs)

```csharp
// Scoped - new instance per request
builder.Services.AddScoped<IMessageService, MessageService>();
```

### Lifetime Rationale

| Service | Lifetime | Reason |
|---------|----------|--------|
| IMessageService | Scoped | Stateless service, per-request is appropriate |

## Testing Strategy

### Test Pyramid

```
         ┌─────────┐
         │   E2E   │  (Manual via Swagger)
         ├─────────┤
         │ Integr. │  CorsTests, AuthorizationTests
         ├─────────┤
         │  Unit   │  (Empty - to be added)
         └─────────┘
```

### Test Infrastructure

- **TestWebApplicationFactory**: Custom factory with test configuration
- **Moq**: Available for mocking service dependencies
- **FluentAssertions**: Available for readable assertions

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

1. **Database Integration**: Add persistent storage (Cosmos DB, SQL Server)
2. **Real Business Logic**: Replace static messages with actual functionality
3. **Role-Based Auth**: Add Auth0 roles/permissions for admin endpoint
4. **Caching**: Redis for performance optimization
5. **Observability**: Application Insights for monitoring
6. **Rate Limiting**: Protect API from abuse

---

_Generated using BMAD Method `document-project` workflow_
_Last Updated: 2025-12-30_
