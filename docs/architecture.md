# Architecture Document

> System architecture and technical design documentation

---

## Executive Summary

The **Here and Now Service** is an ASP.NET Core 8 REST API implementing a reminder management system with Auth0-based authentication and Azure Cosmos DB persistence. The application follows Clean Architecture principles with clear separation between domain logic and infrastructure concerns.

| Aspect | Details |
|--------|---------|
| **Type** | Backend API Service |
| **Framework** | ASP.NET Core 8.0 |
| **Architecture Pattern** | Clean Architecture / Layered |
| **Authentication** | Auth0 JWT Bearer |
| **Deployment** | Azure App Service |
| **Data Storage** | Azure Cosmos DB (Primary) / In-memory (Fallback) |

---

## Technology Stack

| Category | Technology | Version | Purpose |
|----------|-----------|---------|---------|
| **Framework** | ASP.NET Core | 8.0 | Web API hosting |
| **Language** | C# | 12 | Primary language |
| **Authentication** | Auth0 + JWT Bearer | - | Identity & access management |
| **Database** | Azure Cosmos DB | SDK 3.46.0 | NoSQL document storage |
| **API Documentation** | Swashbuckle/Swagger | 6.9.0 | OpenAPI spec generation |
| **Environment Config** | dotenv.net | 3.2.1 | Environment variable loading |
| **Testing** | xUnit | 2.9.2 | Unit/integration testing |
| **Mocking** | Moq | 4.20.72 | Test doubles |
| **Assertions** | FluentAssertions | 6.12.0 | Test assertions |
| **Integration Tests** | WebApplicationFactory | 8.0.11 | HTTP testing |
| **CI/CD** | GitHub Actions | - | Build, test, deploy |
| **Cloud Hosting** | Azure App Service | - | Production hosting |

---

## Architecture Pattern

### Clean Architecture Overview

The solution implements **Clean Architecture** with two main assemblies:

```
┌──────────────────────────────────────────────────────────────┐
│                   External Interface Layer                   │
│  ┌────────────────────────────────────────────────────────┐  │
│  │                    HereAndNow.Web                      │  │
│  │  • Controllers (API endpoints)                         │  │
│  │  • DTOs (Data Transfer Objects with computed State)    │  │
│  │  • Mappers (Domain ↔ DTO conversion)                   │  │
│  │  • Middlewares (Error handling, Security headers)      │  │
│  │  • Configuration (CosmosDbSettings)                    │  │
│  │  • Program.cs (DI configuration, pipeline setup)       │  │
│  └────────────────────────────────────────────────────────┘  │
│                             │                                │
│                   Project Reference                          │
│                             ▼                                │
│  ┌────────────────────────────────────────────────────────┐  │
│  │                 HereAndNow.Reminders                   │  │
│  │                    (Domain Layer)                      │  │
│  │  • Models (ReminderInstance, Message)                  │  │
│  │  • Services (IReminderInstanceService implementations) │  │
│  │  • Persistence (ReminderDocument for Cosmos DB)        │  │
│  │  • Exceptions (ServiceUnavailableException)            │  │
│  │  • No web framework dependencies                       │  │
│  └────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────┘
```

### Key Architectural Decisions

| Decision | Rationale |
|----------|-----------|
| **Separate Domain Assembly** | Business logic can be tested independently and reused |
| **Interface-Based Services** | Enables mocking for tests and swappable implementations |
| **Conditional Persistence** | Cosmos DB in production, in-memory for local dev |
| **DTO Layer** | Computed `State` property, hide internal fields (UserId) |
| **Partition Key = UserId** | Multi-tenant data isolation, efficient queries |
| **Soft Delete Pattern** | Audit trail, data recovery capability |
| **SDK-Level Retry** | 429 throttling handled automatically |

---

## Request Flow

```
┌─────────┐     ┌─────────────┐     ┌────────────────────┐     ┌────────────┐
│  Client │─ ─▶│   Kestrel   │────▶│     Middleware     │────▶│ Controller │
└─────────┘     └─────────────┘     │      Pipeline      │     └────────────┘
                                    │                    │            │
                                    │  1. ErrorHandler   │            │
                                    │  2. SecureHeaders  │            │
                                    │  3. Controllers    │            │
                                    │  4. CORS           │            │
                                    │  5. Authentication │            │
                                    │  6. Authorization  │            │
                                    └────────────────────┘            │
                                                                      ▼
                                    ┌────────────────────────────────────┐
                                    │           Mapper Layer             │
                                    │    ReminderInstanceMapper          │
                                    │    (Domain ↔ DTO conversion)       │
                                    └────────────────────────────────────┘
                                                       │
                                                       ▼
                                    ┌────────────────────────────────────┐
                                    │           Service Layer            │
                                    │    IReminderInstanceService        │
                                    │    IMessageService                 │
                                    └────────────────────────────────────┘
                                                       │
                                    ┌──────────────────┼──────────────────┐
                                    ▼                                     ▼
                   ┌────────────────────────────┐     ┌────────────────────────────┐
                   │   CosmosReminderService    │     │  ReminderInstanceService   │
                   │       (Production)         │     │    (Dev/Fallback)          │
                   │   Uses Azure Cosmos DB     │     │  Uses ConcurrentDictionary │
                   └────────────────────────────┘     └────────────────────────────┘
                                    │
                                    ▼
                   ┌────────────────────────────────────┐
                   │         Azure Cosmos DB            │
                   │   Partition Key: /userId           │
                   │   Database: HereAndNow             │
                   │   Container: Reminders             │
                   └────────────────────────────────────┘
```

---

## Middleware Pipeline

The middleware is configured in `Program.cs` in this specific order:

```csharp
app.UseErrorHandler();      // 1. Global error handling
app.UseSecureHeaders();     // 2. Security headers injection
app.MapControllers();       // 3. Endpoint routing
app.UseCors();              // 4. CORS policy application
app.UseAuthentication();    // 5. JWT token validation
app.UseAuthorization();     // 6. Authorization policy check
```

### ErrorHandlerMiddleware

- Catches unhandled exceptions
- Handles `ServiceUnavailableException` → 503 response
- Differentiates between "Requires authentication" and "Bad credentials" for 401s
- Returns consistent JSON error responses

### SecureHeadersMiddleware

Adds security headers to all responses:
- `X-XSS-Protection: 0`
- `Strict-Transport-Security` (HSTS)
- `X-Frame-Options: deny`
- `X-Content-Type-Options: nosniff`
- `Content-Security-Policy`
- Cache-Control headers

---

## Data Architecture

### Production Implementation (Azure Cosmos DB)

The service uses **Azure Cosmos DB** (Serverless) for persistent storage:

| Aspect | Details |
|--------|---------|
| **Database** | HereAndNow |
| **Container** | Reminders |
| **Partition Key** | `/userId` - co-locates all reminders for a user |
| **Authentication** | Primary Key via environment variables |
| **SDK** | Microsoft.Azure.Cosmos 3.46.0 |
| **Retry Policy** | 9 attempts, 30s max wait for 429 |

**Implementation Pattern:**
```
IReminderInstanceService (Interface)
        │
        ├── CosmosReminderInstanceService (Production)
        │   └── Uses CosmosClient → Azure Cosmos DB
        │
        └── ReminderInstanceService (Dev/Fallback)
            └── Uses ConcurrentDictionary (in-memory)
```

**Key Implementation Details:**
- `CosmosClient` registered as Singleton (SDK best practice)
- `CosmosReminderInstanceService` registered as Scoped
- All queries include partition key for optimal RU consumption
- Soft-delete pattern (`IsDeleted` flag) preserves audit trail
- Service throws `ServiceUnavailableException` on transient Cosmos errors
- **SDK Retry Policy**: Automatic retry on 429 throttling (9 attempts, 30s max wait)

### Fallback Implementation (In-Memory)

When Cosmos environment variables are not configured:

```csharp
private readonly ConcurrentDictionary<Guid, ReminderInstance> _reminders = new();
```

**Characteristics:**
- Thread-safe concurrent access
- Data persists only for application lifetime
- Suitable for development/demo purposes

### Domain Model

```
ReminderInstance
├── Id: Guid (auto-generated)
├── UserId: string (partition key, from JWT 'sub' claim)
├── Text: string (required)
├── ScheduledDateAndTime: DateTime
├── IsCompleted: bool
├── IsDeleted: bool (soft-delete flag)
├── ShouldPlaySound: bool
└── ShouldDoVibration: bool
```

### DTO Layer

```
ReminderInstanceDto
├── Id: Guid
├── Text: string
├── ScheduledDateAndTime: DateTime
├── IsCompleted: bool
├── IsDeleted: bool
├── ShouldPlaySound: bool
├── ShouldDoVibration: bool
└── State: ReminderState (COMPUTED)
    └── Priority: Deleted > Completed > Active > Scheduled
```

**Note:** `UserId` is intentionally excluded from DTO to prevent exposure in API responses.

### Cosmos Document Model

The `ReminderDocument` class handles Cosmos-specific serialization:
- `id` (string) - Document identifier (Guid.ToString())
- `userId` (string) - Partition key
- Properties use camelCase naming (CosmosSerializationOptions)

---

## API Design

### REST Conventions

| HTTP Method | Usage |
|-------------|-------|
| `GET` | Retrieve resources |
| `POST` | Create new resource |
| `PUT` | Full update of resource |
| `DELETE` | Soft-delete resource |

### Endpoint Summary

| Endpoint | Method | Auth | Description |
|----------|--------|------|-------------|
| `/api/messages/public` | GET | No | Public message |
| `/api/messages/protected` | GET | Yes | Protected message |
| `/api/messages/admin` | GET | Yes | Admin message |
| `/api/reminder-instances` | GET | Yes | List user's reminders |
| `/api/reminder-instances/{id}` | GET | Yes | Get single reminder |
| `/api/reminder-instances` | POST | Yes | Create reminder |
| `/api/reminder-instances/{id}` | PUT | Yes | Update reminder |
| `/api/reminder-instances/{id}` | DELETE | Yes | Soft-delete reminder |

### Response Format

All responses use JSON with consistent error format:
```json
{
  "message": "Error description"
}
```

---

## Authentication & Authorization

### Auth0 Integration

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = $"https://{auth0Domain}/";
        options.Audience = auth0Audience;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = true,
            ValidateIssuer = true,
            ValidIssuer = $"https://{auth0Domain}/",
            ValidateIssuerSigningKey = true
        };
    });
```

### User Identification Flow

1. Client obtains JWT from Auth0
2. JWT included in `Authorization: Bearer {token}` header
3. Middleware validates token against Auth0 issuer
4. Controller extracts `sub` claim as `userId`
5. `userId` used as Cosmos partition key for data isolation

---

## Testing Architecture

### Test Project Structure

```
HereAndNow.Web.Tests/
├── Controllers/
│   └── ReminderInstancesControllerTests.cs  # 13 unit tests
├── Helpers/
│   └── TestWebApplicationFactory.cs         # Test host factory
└── Integration/
    ├── AuthorizationTests.cs                # 5 auth tests
    └── CorsTests.cs                         # CORS tests
```

### Testing Strategies

| Test Type | Purpose | Tools |
|-----------|---------|-------|
| **Unit Tests** | Test controller logic in isolation | xUnit, Moq |
| **Integration Tests** | Test full HTTP pipeline | WebApplicationFactory |

### Test Coverage Areas

- CRUD operations (GetAll, GetById, Create, Update, Delete)
- State computation (Scheduled, Active, Completed, Deleted)
- Authorization enforcement (401 without token)
- Error handling (404, 400)

---

## Deployment Architecture

```
┌─────────────────┐     ┌─────────────────┐     ┌───────────────────┐
│   GitHub Repo   │────▶│  GitHub Actions │────▶│ Azure App Service │
│                 │     │      CI/CD      │     │   (Production)    │
└─────────────────┘     └─────────────────┘     └───────────────────┘
        │                       │                         │
        │                       ├── Build (.NET 8)        │
        │                       ├── Test (xUnit)          ▼
        │                       ├── Coverage Upload   ┌───────────────────┐
        │                       ├── Publish           │  Azure Cosmos DB  │
        │                       └── Deploy            │   (Serverless)    │
        │                                             └───────────────────┘
        └── Trigger: Push to main branch
```

### Infrastructure

| Component | Service |
|-----------|---------|
| **Hosting** | Azure App Service |
| **Database** | Azure Cosmos DB (Serverless) |
| **CI/CD** | GitHub Actions |
| **Identity** | Auth0 |
| **Secrets** | Azure App Settings + GitHub Secrets |

---

## Security Considerations

### Implemented Security

- [x] JWT Bearer authentication via Auth0
- [x] Multi-tenant data isolation via partition key
- [x] CORS policy with configurable origins
- [x] Security headers (HSTS, CSP, X-Frame-Options)
- [x] No server header exposed
- [x] SDK-level retry for Cosmos DB throttling
- [x] Soft delete pattern (audit trail)

### Security Recommendations

- [ ] Add API-level rate limiting (per-user request throttling)
- [ ] Add request logging with PII masking
- [ ] Configure Application Insights for monitoring
- [ ] Implement refresh token rotation
- [ ] Add input validation middleware

---

## Scalability Considerations

### Current Architecture Supports

- **Horizontal Scaling**: Stateless services scale with App Service instances
- **Cosmos DB Scaling**: Serverless auto-scales with demand
- **Partition Strategy**: Efficient single-partition queries per user

### Future Scaling Path

1. **Azure Functions** - For event-driven reminder triggers
2. **Azure Service Bus** - For notification queueing
3. **Redis Cache** - For session/cache if needed
4. **CDN** - For static assets if frontend is co-hosted

---

## Related Documentation

- [API Contracts](./api-contracts.md) - Detailed API specification
- [Data Models](./data-models.md) - Domain model documentation
- [Source Tree](./source-tree-analysis.md) - Project structure
- [Development Guide](./development-guide.md) - Setup instructions
- [Deployment Guide](./deployment-guide.md) - CI/CD and Azure deployment

---

## Documentation Metadata

| Field | Value |
|-------|-------|
| **Generated** | 2025-12-17 |
| **Scan Level** | Exhaustive |
