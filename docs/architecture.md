# Architecture Document

## Executive Summary

The **Here and Now Service** is an ASP.NET Core 8 REST API implementing a reminder management system with Auth0-based authentication. The application follows Clean Architecture principles with clear separation between domain logic and infrastructure concerns.

| Aspect | Details |
|--------|---------|
| **Type** | Backend API Service |
| **Framework** | ASP.NET Core 8.0 |
| **Architecture Pattern** | Clean Architecture / Layered |
| **Authentication** | Auth0 JWT Bearer |
| **Deployment** | Azure App Service |
| **Data Storage** | In-memory (ConcurrentDictionary) |

---

## Technology Stack

| Category | Technology | Version | Purpose |
|----------|-----------|---------|---------|
| **Framework** | ASP.NET Core | 8.0 | Web API hosting |
| **Language** | C# | 12 | Primary language |
| **Authentication** | Auth0 + JWT Bearer | - | Identity & access management |
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

The solution implements **Clean Architecture** with two assemblies:

```
┌──────────────────────────────────────────────────────────────┐
│                   External Interface Layer                   │
│  ┌────────────────────────────────────────────────────────┐  │
│  │                    HereAndNow.Web                      │  │
│  │  • Controllers (API endpoints)                         │  │
│  │  • Middlewares (Error handling, Security headers)      │  │
│  │  • Program.cs (DI configuration, pipeline setup)       │  │
│  │  • Authentication (Auth0 JWT validation)               │  │
│  └────────────────────────────────────────────────────────┘  │
│                             │                                │
│                   Project Reference                          │
│                             ▼                                │
│  ┌────────────────────────────────────────────────────────┐  │
│  │                 HereAndNow.Reminders                   │  │
│  │                    (Domain Layer)                      │  │
│  │  • Models (ReminderInstance, Message, ReminderStatus)  │  │
│  │  • Service Interfaces (IReminderInstanceService)       │  │
│  │  • Service Implementations (ReminderInstanceService)   │  │
│  │  • No web framework dependencies                       │  │
│  └────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────┘
```

### Key Architectural Decisions

| Decision | Rationale |
|----------|-----------|
| **Separate Domain Assembly** | Business logic can be tested independently and reused |
| **Interface-Based Services** | Enables mocking for tests and future implementations |
| **Singleton ReminderService** | In-memory storage requires single instance for consistency |
| **Scoped MessageService** | Stateless service, new instance per request is fine |
| **Middleware for Cross-Cutting** | Security headers and error handling applied globally |

---

## Request Flow

```
┌─────────┐     ┌─────────────┐     ┌────────────────────┐     ┌────────────┐
│  Client │─ ─▶│   Kestrel   │────▶│     Middleware     │────▶│ Controller │
└─────────┘     └─────────────┘     │      Pipeline      │     └────────────┘
                                    │                    │            │
                                    │  1. ErrorHandler   │            │
                                    │  2. SecureHeaders  │            │
                                    │  3. CORS           │            │
                                    │  4. Authentication │            │
                                    │  5. Authorization  │            │
                                    └────────────────────┘            │
                                                                      ▼
                                    ┌────────────────────────────────────┐
                                    │           Service Layer            │
                                    │    IReminderInstanceService        │
                                    │    IMessageService                 │
                                    └────────────────────────────────────┘
                                                       │
                                                       ▼
                                    ┌────────────────────────────────────┐
                                    │           Data Storage             │
                                    │    ConcurrentDictionary<Guid,      │
                                    │         ReminderInstance>          │
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
- Returns consistent JSON error responses
- Differentiates between "Requires authentication" and "Bad credentials" for 401s

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

The service uses **Azure Cosmos DB** (Serverless) for persistent storage with the following configuration:

| Aspect | Details |
|--------|---------|
| **Database** | HereAndNow |
| **Container** | Reminders |
| **Partition Key** | `/userId` - co-locates all reminders for a user |
| **Authentication** | Primary Key via environment variables |
| **SDK** | Microsoft.Azure.Cosmos 3.46.0 |

**Architecture Pattern:**
```
┌─────────────────────────────────────────────────────────────┐
│                    IReminderInstanceService                  │
│                     (Interface in Reminders)                 │
├─────────────────────────────────────────────────────────────┤
│  CosmosReminderInstanceService  │  ReminderInstanceService  │
│        (Production)              │     (In-Memory/Dev)       │
│   Uses Azure Cosmos DB           │  Uses ConcurrentDictionary│
└─────────────────────────────────────────────────────────────┘
```

**Key Implementation Details:**
- `CosmosClient` registered as Singleton (SDK best practice)
- Service registered as Scoped (one container reference per request)
- All queries include partition key for optimal RU consumption
- Soft-delete pattern (`IsDeleted` flag) preserves audit trail
- Service returns 503 on Cosmos unavailability
- **SDK Retry Policy**: Automatic retry on 429 throttling (9 attempts, 30s max wait)

### Fallback Implementation (In-Memory)

When Cosmos environment variables are not configured, the service falls back to an in-memory implementation:

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
| `DELETE` | Remove resource |

### Endpoint Summary

| Endpoint | Method | Auth | Description |
|----------|--------|------|-------------|
| `/api/messages/public` | GET | No | Public message |
| `/api/messages/protected` | GET | Yes | Protected message |
| `/api/messages/admin` | GET | Yes | Admin message |
| `/api/reminder-instances` | GET | Yes | List all reminders |
| `/api/reminder-instances/{id}` | GET | Yes | Get single reminder |
| `/api/reminder-instances` | POST | Yes | Create reminder |
| `/api/reminder-instances/{id}` | PUT | Yes | Update reminder |
| `/api/reminder-instances/{id}` | DELETE | Yes | Delete reminder |

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

### Authorization Flow

1. Client obtains JWT from Auth0
2. JWT included in `Authorization: Bearer {token}` header
3. Middleware validates token against Auth0 issuer
4. `[Authorize]` attribute enforces authentication on endpoints

---

## Testing Architecture

### Test Project Structure

```
HereAndNow.Web.Tests/
├── Controllers/
│   └── ReminderInstancesControllerTests.cs  # Unit tests
├── Helpers/
│   └── TestWebApplicationFactory.cs         # Test host factory
└── Integration/
    ├── AuthorizationTests.cs                # Auth integration tests
    └── CorsTests.cs                         # CORS integration tests
```

### Testing Strategies

| Test Type | Purpose | Tools |
|-----------|---------|-------|
| **Unit Tests** | Test controller logic in isolation | xUnit, Moq |
| **Integration Tests** | Test full HTTP pipeline | WebApplicationFactory |

---

## Deployment Architecture

```
┌─────────────────┐     ┌─────────────────┐     ┌───────────────────┐
│   GitHub Repo   │────▶│  GitHub Actions │────▶│ Azure App Service │
│                 │     │      CI/CD      │     │   (Production)    │
└─────────────────┘     └─────────────────┘     └───────────────────┘
        │                       │
        │                       ├── Build (.NET 8)
        │                       ├── Test (xUnit)
        │                       ├── Publish
        │                       └── Deploy
        │
        └── Trigger: Push to main branch
```

### Infrastructure

| Component | Service |
|-----------|---------|
| **Hosting** | Azure App Service |
| **CI/CD** | GitHub Actions |
| **Identity** | Auth0 |
| **Secrets** | Azure App Settings + GitHub Secrets |

---

## Security Considerations

### Implemented Security

- JWT Bearer authentication
- CORS policy with specific origin
- Security headers (HSTS, CSP, X-Frame-Options)
- No server header exposed
- Swagger access restricted by IP in production

### Security Recommendations

- [x] SDK-level retry for Cosmos DB throttling (429 responses)
- [ ] Add API-level rate limiting (per-user request throttling)
- [ ] Add request logging with PII masking
- [ ] Configure Application Insights for monitoring
- [ ] Implement refresh token rotation
- [ ] Add input validation middleware

---

## Scalability Considerations

### Current Limitations

- In-memory storage limits horizontal scaling
- Single instance deployment

### Scaling Path

1. **Add Database** - Replace ConcurrentDictionary with SQL/NoSQL
2. **Stateless Services** - All services should be stateless for multi-instance
3. **Azure Service Bus** - For event-driven features
4. **Redis Cache** - For session/cache if needed

---

## Related Documentation

- [API Contracts](./api-contracts.md) - Detailed API specification
- [Data Models](./data-models.md) - Domain model documentation
- [Source Tree](./source-tree-analysis.md) - Project structure
- [Development Guide](./development-guide.md) - Setup instructions
- [Deployment Guide](./deployment-guide.md) - CI/CD and Azure deployment
