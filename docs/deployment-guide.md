# Deployment Guide

## Overview

The Here and Now Service is deployed to **Azure App Service** using GitHub Actions for CI/CD automation.

**Production URL:** `https://here-and-now-service.azurewebsites.net`

---

## CI/CD Pipeline

### Workflow Location

`.github/workflows/main_here-and-now-service.yml`

### Trigger Events

| Event | Description |
|-------|-------------|
| `push` to `main` | Automatic deployment on merge to main |
| `workflow_dispatch` | Manual trigger via GitHub UI |

### Pipeline Stages

```
┌─────────────┐     ┌─────────────┐
│    Build    │────▶│   Deploy    │
│  (ubuntu)   │     │  (ubuntu)   │
└─────────────┘     └─────────────┘
      │
      ├── Checkout
      ├── Setup .NET 8
      ├── Build (Release)
      ├── Run Tests
      ├── Publish Results
      ├── Upload Coverage
      └── Publish Artifact
```

### Build Job Details

```yaml
steps:
  - uses: actions/checkout@v4
  - uses: actions/setup-dotnet@v4
    with:
      dotnet-version: '8.x'
  - run: dotnet build HereAndNow.sln --configuration Release
  - run: dotnet test HereAndNow.sln --configuration Release --no-build
  - run: dotnet publish Web/HereAndNow.Web/HereAndNow.Web.csproj -c Release
```

### Quality Gates

| Gate | Requirement |
|------|-------------|
| Build | Must succeed |
| Tests | All tests must pass |
| Coverage | Reports uploaded as artifacts |

**Important:** Tests must pass before deployment proceeds.

---

## Azure App Service Configuration

### App Service Details

| Setting | Value |
|---------|-------|
| **App Name** | `here-and-now-service` |
| **Slot** | `Production` |
| **Platform** | Linux / Windows |
| **Runtime** | .NET 8 |

### Required App Settings

Configure these in Azure Portal → App Service → Configuration → Application settings:

| Setting | Description |
|---------|-------------|
| `PORT` | Application port (usually 80 for App Service) |
| `CLIENT_ORIGIN_URL` | Frontend URL for CORS |
| `AUTH0_DOMAIN` | Auth0 tenant domain |
| `AUTH0_AUDIENCE` | Auth0 API identifier |
| `COSMOS_ENDPOINT` | Cosmos DB account endpoint URL |
| `COSMOS_PRIMARY_KEY` | Cosmos DB primary key for authentication |
| `COSMOS_DATABASE_NAME` | Cosmos DB database name (e.g., `HereAndNow`) |
| `COSMOS_CONTAINER_NAME` | Cosmos DB container name (e.g., `Reminders`) |

### Cosmos DB Configuration

The application requires an Azure Cosmos DB account with the following setup:

1. **Create Cosmos DB Account** (Serverless recommended for dev/test)
2. **Create Database**: `HereAndNow`
3. **Create Container**: `Reminders` with partition key `/userId`
4. **Configure App Settings** with connection details

**Important:** The Cosmos container must have `/userId` as the partition key for optimal query performance and user data isolation.

**Fallback Behavior:** If `COSMOS_ENDPOINT` or `COSMOS_PRIMARY_KEY` are not configured, the service will use an in-memory implementation (data lost on restart).

### Deployment Credentials

The GitHub Actions workflow uses a **Publish Profile** stored in GitHub Secrets:

```
AZUREAPPSERVICE_PUBLISHPROFILE_BACC676B643B46F199F9AA3A4AF97999
```

To update:
1. Azure Portal → App Service → Deployment Center → Manage publish profile
2. Download publish profile
3. Update GitHub secret: Settings → Secrets → Actions

---

## Manual Deployment

### Build for Production

```bash
dotnet publish Web/HereAndNow.Web/HereAndNow.Web.csproj -c Release -o ./publish
```

### Deploy via Azure CLI

```bash
# Login to Azure
az login

# Deploy to App Service
az webapp deploy \
  --resource-group <resource-group> \
  --name here-and-now-service \
  --src-path ./publish \
  --type zip
```

### Deploy via VS Code

1. Install Azure App Service extension
2. Right-click on `publish` folder
3. Select "Deploy to Web App"
4. Choose `here-and-now-service`

---

## Swagger Access in Production

Swagger UI is available in production but should be secured with IP restrictions.

### Azure IP Restriction Setup

1. Azure Portal → App Service → Networking → Access restriction
2. Add rule for `/swagger*` path:
   - Name: `Allow-Developer-IP`
   - Action: Allow
   - IP: Your public IP with `/32` suffix
   - Priority: 100

See [SWAGGER_SETUP.md](../Web/HereAndNow.Web/SWAGGER_SETUP.md) for detailed instructions.

---

## Monitoring

### Azure Application Insights

Consider enabling Application Insights for:
- Request tracking
- Exception logging
- Performance metrics
- Custom telemetry

### Log Streaming

```bash
# Stream live logs
az webapp log tail --name here-and-now-service --resource-group <rg>
```

### View Deployment Logs

GitHub Actions → Select workflow run → View logs

---

## Rollback Procedures

### Revert to Previous Deployment

1. GitHub → Actions → Select previous successful run
2. Click "Re-run all jobs"

Or use Azure deployment slots:
```bash
az webapp deployment slot swap \
  --name here-and-now-service \
  --resource-group <rg> \
  --slot staging \
  --target-slot production
```

### Quick Rollback via Git

```bash
# Revert to previous commit
git revert HEAD
git push origin main
# Pipeline will automatically deploy
```

---

## Environment-Specific Configuration

### Development

- Local `.env` file
- Swagger UI enabled
- Debug logging

### Production (Azure)

- Azure App Settings
- Swagger secured by IP restriction
- Production logging level

---

## Security Checklist

- [ ] Auth0 credentials stored in Azure App Settings (not in code)
- [ ] Swagger UI secured with IP restrictions
- [ ] HTTPS enforced in Azure App Service
- [ ] Publish profile secret rotated periodically
- [ ] Security headers verified (X-Frame-Options, CSP, etc.)

---

## Related Documentation

- [Development Guide](./development-guide.md) - Local development setup
- [Architecture](./architecture.md) - System architecture
- [SWAGGER_SETUP.md](../Web/HereAndNow.Web/SWAGGER_SETUP.md) - Swagger security configuration
