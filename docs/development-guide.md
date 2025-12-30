# Here and Now Service - Development Guide

**Date:** 2025-12-29

## Prerequisites

### Required Software

| Software | Version | Purpose |
|----------|---------|---------|
| .NET SDK | 8.0+ | Build and run the application |
| Git | 2.0+ | Version control |
| IDE | VS 2022 / Rider / VS Code | Development environment |

### Optional Tools

| Tool | Purpose |
|------|---------|
| Postman / Insomnia | API testing |
| Azure CLI | Deployment operations |

### Auth0 Account

You need an Auth0 account to test authenticated endpoints:
1. Create an account at [auth0.com](https://auth0.com)
2. Create an API in Auth0 Dashboard
3. Note your domain and audience values

## Environment Setup

### 1. Clone the Repository

```bash
git clone <repository-url>
cd here-and-now-service
```

### 2. Configure Environment Variables

Create a `.env` file in the project root:

```env
PORT=6060
CLIENT_ORIGIN_URL=http://localhost:3000
AUTH0_DOMAIN=your-tenant.auth0.com
AUTH0_AUDIENCE=https://your-api-identifier
```

**Variable Descriptions:**

| Variable | Example | Description |
|----------|---------|-------------|
| PORT | 6060 | Port the API listens on |
| CLIENT_ORIGIN_URL | http://localhost:3000 | Allowed CORS origin(s), comma-separated |
| AUTH0_DOMAIN | dev-abc123.auth0.com | Your Auth0 tenant domain |
| AUTH0_AUDIENCE | https://api.example.com | Your Auth0 API identifier |

### 3. Restore Dependencies

```bash
dotnet restore
```

## Building the Application

### Build Entire Solution

```bash
dotnet build HereAndNow.sln
```

### Build Release Configuration

```bash
dotnet build HereAndNow.sln --configuration Release
```

### Build Specific Project

```bash
dotnet build Web/HereAndNow.Web/HereAndNow.Web.csproj
```

## Running the Application

### Development Mode

```bash
dotnet run --project Web/HereAndNow.Web/HereAndNow.Web.csproj
```

The API will be available at: `http://localhost:{PORT}`

### Watch Mode (Auto-Reload)

```bash
dotnet watch run --project Web/HereAndNow.Web/HereAndNow.Web.csproj
```

### Accessing Swagger UI

Once running, open your browser to:
```
http://localhost:{PORT}/swagger
```

## Testing

### Run All Tests

```bash
dotnet test
```

### Run Tests with Detailed Output

```bash
dotnet test --logger "console;verbosity=detailed"
```

### Run Specific Test by Name

```bash
dotnet test --filter "FullyQualifiedName~GetAll_ShouldReturnOkWithReminders"
```

### Run Tests with Coverage

```bash
dotnet test --collect:"XPlat Code Coverage"
```

Coverage reports are generated in `TestResults/` directories.

### Watch Mode for Testing

```bash
dotnet watch test --project Web/HereAndNow.Web.Tests/HereAndNow.Web.Tests.csproj
```

### Test Categories

| Category | Location | Description |
|----------|----------|-------------|
| Unit Tests | `Controllers/` | Test controllers with mocked services |
| Integration Tests | `Integration/` | Test full HTTP pipeline |

## Project Structure

```
here-and-now-service/
├── Reminders/HereAndNow.Reminders/    # Business logic (edit here for domain changes)
│   ├── Models/                        # Domain models
│   └── Services/                      # Service interfaces + implementations
├── Web/HereAndNow.Web/                # API layer (edit here for API changes)
│   ├── Controllers/                   # REST endpoints
│   ├── DTOs/                          # API contracts
│   ├── Mappers/                       # Domain ↔ DTO conversion
│   └── Middlewares/                   # Custom middleware
└── Web/HereAndNow.Web.Tests/          # Tests
```

## Common Development Tasks

### Adding a New Endpoint

1. **Add/update domain model** (if needed):
   ```
   Reminders/HereAndNow.Reminders/Models/NewModel.cs
   ```

2. **Add/update service interface**:
   ```
   Reminders/HereAndNow.Reminders/Services/INewService.cs
   ```

3. **Add service implementation**:
   ```
   Reminders/HereAndNow.Reminders/Services/NewService.cs
   ```

4. **Add DTO** (if different from domain):
   ```
   Web/HereAndNow.Web/DTOs/NewModelDto.cs
   ```

5. **Add mapper** (if using DTO):
   ```
   Web/HereAndNow.Web/Mappers/NewModelMapper.cs
   ```

6. **Add controller**:
   ```
   Web/HereAndNow.Web/Controllers/NewController.cs
   ```

7. **Register service in DI** (Program.cs):
   ```csharp
   builder.Services.AddScoped<INewService, NewService>();
   ```

8. **Add tests**:
   ```
   Web/HereAndNow.Web.Tests/Controllers/NewControllerTests.cs
   ```

### Adding a New Middleware

1. Create middleware class:
   ```csharp
   // Web/HereAndNow.Web/Middlewares/NewMiddleware.cs
   class NewMiddleware
   {
       private readonly RequestDelegate _next;

       public NewMiddleware(RequestDelegate next) => _next = next;

       public async Task InvokeAsync(HttpContext context)
       {
           // Before
           await _next(context);
           // After
       }
   }

   public static class NewMiddlewareExtensions
   {
       public static IApplicationBuilder UseNew(this IApplicationBuilder builder)
           => builder.UseMiddleware<NewMiddleware>();
   }
   ```

2. Register in pipeline (Program.cs):
   ```csharp
   app.UseNew();
   ```

### Modifying Authentication

Authentication is configured in `Program.cs`:

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = $"https://{auth0Domain}/";
        options.Audience = auth0Audience;
        // Modify validation parameters here
    });
```

## Code Style and Conventions

### File-Scoped Namespaces

Use file-scoped namespaces (C# 10+):

```csharp
namespace HereAndNowService.Controllers;  // Note: semicolon, not braces

public class MyController : ControllerBase
{
    // ...
}
```

### Nullable Reference Types

Nullable reference types are enabled. Use `?` for nullable types:

```csharp
public ReminderInstance? GetById(Guid id)  // Can return null
```

### XML Documentation

Document public APIs with XML comments:

```csharp
/// <summary>
/// Gets all reminder instances.
/// </summary>
/// <returns>A collection of all reminder instances.</returns>
[HttpGet]
public ActionResult<IEnumerable<ReminderInstanceDto>> GetAll()
```

### Logging

Use structured logging with ILogger:

```csharp
_logger.LogInformation("GET /api/reminder-instances/{ReminderId} - Request received", id);
```

## Debugging

### Visual Studio / Rider

1. Set `HereAndNow.Web` as startup project
2. Press F5 to start debugging
3. Breakpoints work as expected

### VS Code

1. Install C# extension
2. Create `.vscode/launch.json`:
   ```json
   {
     "version": "0.2.0",
     "configurations": [
       {
         "name": ".NET Core Launch (web)",
         "type": "coreclr",
         "request": "launch",
         "program": "${workspaceFolder}/Web/HereAndNow.Web/bin/Debug/net8.0/HereAndNow.Web.dll",
         "cwd": "${workspaceFolder}/Web/HereAndNow.Web"
       }
     ]
   }
   ```

### Testing API Endpoints

**Using curl:**
```bash
# Public endpoint
curl http://localhost:6060/api/messages/public

# Authenticated endpoint
curl -H "Authorization: Bearer YOUR_TOKEN" http://localhost:6060/api/reminder-instances
```

**Using Swagger UI:**
1. Navigate to `http://localhost:6060/swagger`
2. Click "Authorize" button
3. Enter your JWT token
4. Execute endpoints

## Troubleshooting

### Common Issues

| Issue | Solution |
|-------|----------|
| "Config variable missing" | Check `.env` file exists with all required variables |
| 401 on all requests | Verify AUTH0_DOMAIN and AUTH0_AUDIENCE |
| CORS errors | Check CLIENT_ORIGIN_URL matches your frontend |
| Port already in use | Change PORT in `.env` |

### Viewing Logs

Application logs are written to console. For more detailed logs, adjust logging level in code or add logging configuration.

---

_Generated using BMAD Method `document-project` workflow_
