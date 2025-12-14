# Development Guide

## Prerequisites

Before you begin, ensure you have the following installed:

| Tool | Version | Purpose |
|------|---------|---------|
| .NET SDK | 8.0+ | Build and run the application |
| Git | Latest | Version control |
| Visual Studio 2022 / VS Code | Latest | IDE (recommended) |
| Auth0 Account | - | Authentication provider |

### Verify Installation

```bash
# Check .NET SDK version
dotnet --version
# Expected: 8.x.x

# Check Git
git --version
```

---

## Environment Setup

### 1. Clone the Repository

```bash
git clone <repository-url>
cd here-and-now-service
```

### 2. Create Environment File

Create a `.env` file in the project root (not committed to Git):

```env
PORT=3001
CLIENT_ORIGIN_URL=http://localhost:4040
AUTH0_DOMAIN=your-tenant.auth0.com
AUTH0_AUDIENCE=your-api-identifier
```

| Variable | Description | Example |
|----------|-------------|---------|
| `PORT` | Port for the API server | `3001` |
| `CLIENT_ORIGIN_URL` | Allowed CORS origin for frontend | `http://localhost:4040` |
| `AUTH0_DOMAIN` | Your Auth0 tenant domain | `dev-abc123.us.auth0.com` |
| `AUTH0_AUDIENCE` | API identifier in Auth0 | `https://api.hereandnow.com` |

### 3. Auth0 Configuration

1. Create an Auth0 account at [auth0.com](https://auth0.com)
2. Create a new API in the Auth0 Dashboard
3. Note the **Domain** and **API Identifier** for your `.env` file
4. Create a test application for obtaining tokens

---

## Building the Project

### Build Entire Solution

```bash
dotnet build HereAndNow.sln
```

### Build with Release Configuration

```bash
dotnet build HereAndNow.sln --configuration Release
```

### Build Specific Project

```bash
# Web API only
dotnet build Web/HereAndNow.Web/HereAndNow.Web.csproj

# Business logic only
dotnet build Reminders/HereAndNow.Reminders/HereAndNow.Reminders.csproj
```

---

## Running the Application

### Development Mode

```bash
dotnet run --project Web/HereAndNow.Web/HereAndNow.Web.csproj
```

The API will start at `http://localhost:{PORT}` (default: 3001)

### With Hot Reload

```bash
dotnet watch run --project Web/HereAndNow.Web/HereAndNow.Web.csproj
```

### Verify It's Running

```bash
# Test public endpoint (no auth required)
curl http://localhost:3001/api/messages/public

# Access Swagger UI
open http://localhost:3001/swagger
```

---

## Testing

### Run All Tests

```bash
dotnet test
```

### Run Tests with Verbose Output

```bash
dotnet test --logger "console;verbosity=detailed"
```

### Run Specific Test

```bash
dotnet test --filter "FullyQualifiedName~ReminderInstancesControllerTests"
```

### Run with Code Coverage

```bash
dotnet test --collect:"XPlat Code Coverage"
```

### Watch Mode (Continuous Testing)

```bash
dotnet watch test --project Web/HereAndNow.Web.Tests/HereAndNow.Web.Tests.csproj
```

### Test Categories

| Test Type | Location | Description |
|-----------|----------|-------------|
| Unit Tests | `Controllers/` | Controller logic with mocked services |
| Integration Tests | `Integration/` | Full HTTP request/response testing |

---

## API Documentation

### Accessing Swagger UI

1. Start the application
2. Navigate to `http://localhost:{PORT}/swagger`
3. Explore and test endpoints interactively

### Testing Authenticated Endpoints

1. In Swagger UI, click **"Authorize"** button
2. Enter your Auth0 JWT token (without `Bearer` prefix)
3. Click **"Authorize"** then **"Close"**
4. Test protected endpoints

### Getting a Test Token

Option 1: Via your frontend SPA
Option 2: Auth0 test token endpoint
Option 3: Using Postman with Auth0 OAuth2 flow

---

## Common Development Tasks

### Adding a New API Endpoint

1. **Define interface** in `Reminders/Services/` (if new service)
2. **Implement service** in `Reminders/Services/`
3. **Register in DI** in `Program.cs`
4. **Create controller method** in `Web/Controllers/`
5. **Add XML documentation** for Swagger
6. **Write tests** in `Web.Tests/`

### Adding a New Model

1. Create model class in `Reminders/Models/`
2. Add XML documentation
3. Update related service interfaces
4. Update controller response types
5. Add tests for new functionality

### Modifying Middleware

1. Edit middleware in `Web/Middlewares/`
2. Test middleware order in `Program.cs`
3. Verify security headers with curl:
   ```bash
   curl -I http://localhost:3001/api/messages/public
   ```

---

## IDE Configuration

### Visual Studio 2022

1. Open `HereAndNow.sln`
2. Set `HereAndNow.Web` as startup project
3. Press F5 to debug

### Visual Studio Code

Recommended extensions:
- C# Dev Kit
- .NET Extension Pack
- REST Client (for API testing)

Launch configuration is in `.vscode/launch.json`

---

## Troubleshooting

### "Config variable missing" Error

**Cause:** Required environment variable not set

**Solution:** Ensure all variables are in `.env`:
```env
PORT=3001
CLIENT_ORIGIN_URL=http://localhost:4040
AUTH0_DOMAIN=your-domain.auth0.com
AUTH0_AUDIENCE=your-api-identifier
```

### 401 Unauthorized on Protected Endpoints

**Cause:** Invalid or missing JWT token

**Solution:**
1. Verify Auth0 configuration matches `.env`
2. Ensure token is not expired
3. Check token audience matches `AUTH0_AUDIENCE`

### CORS Errors in Browser

**Cause:** Frontend URL doesn't match `CLIENT_ORIGIN_URL`

**Solution:** Update `CLIENT_ORIGIN_URL` in `.env` to match your frontend

### Build Errors

```bash
# Clean and rebuild
dotnet clean HereAndNow.sln
dotnet restore
dotnet build HereAndNow.sln
```

---

## Project References

- [API Contracts](./api-contracts.md) - API documentation
- [Architecture](./architecture.md) - System architecture
- [Source Tree](./source-tree-analysis.md) - Project structure
- [SWAGGER_SETUP.md](../Web/HereAndNow.Web/SWAGGER_SETUP.md) - Swagger configuration
