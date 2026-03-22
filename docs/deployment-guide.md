# Here and Now Service - Deployment Guide

**Date:** 2026-03-19

## Overview

The Here and Now Service is deployed to Azure Web Apps using GitHub Actions. The CI/CD pipeline is triggered on every push to the `main` branch. The service connects to Azure Cosmos DB for task, reminder, and recurring task persistence.

## Deployment Architecture

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│    GitHub       │────►│  GitHub Actions │────►│  Azure Web App  │
│   (main branch) │     │                 │     │  (Production)   │
└─────────────────┘     └─────────────────┘     └─────────────────┘
                               │                        │
                               ▼                        ▼
                        ┌─────────────┐         ┌─────────────────┐
                        │   Tests     │         │  Azure Cosmos   │
                        │   (Gate)    │         │      DB         │
                        └─────────────┘         └─────────────────┘
                                                        │
                                                        ▼
                                                ┌─────────────────┐
                                                │     Auth0       │
                                                │   (Identity)    │
                                                └─────────────────┘
```

## CI/CD Pipeline

**Workflow File:** `.github/workflows/main_here-and-now-service.yml`

### Pipeline Stages

```
┌─────────┐    ┌─────────┐    ┌─────────┐    ┌─────────┐    ┌─────────┐
│  Build  │───►│  Test   │───►│ Publish │───►│ Upload  │───►│ Deploy  │
└─────────┘    └─────────┘    └─────────┘    └─────────┘    └─────────┘
                    │
                    │ Quality Gate
                    │ (must pass)
                    ▼
```

### Pipeline Details

| Stage | Command | Description |
|-------|---------|-------------|
| Setup | `actions/setup-dotnet@v4` | Install .NET 8.0 SDK |
| Build | `dotnet build --configuration Release` | Compile solution |
| Test | `dotnet test --configuration Release` | Run all tests (Task.Tests + Web.Tests) |
| Test Report | `dorny/test-reporter@v1` | Publish results to GitHub Actions UI |
| Coverage | `actions/upload-artifact@v4` | Upload coverage reports as artifacts |
| Publish | `dotnet publish -c Release` | Create deployment package |
| Deploy | `azure/webapps-deploy@v3` | Deploy to Azure Web App |

### Trigger Conditions

```yaml
on:
  push:
    branches:
      - main
  workflow_dispatch:  # Manual trigger
```

## Azure Configuration

### Required Azure Resources

| Resource | Type | Purpose |
|----------|------|---------|
| here-and-now-service | Azure Web App | Application hosting |
| App Service Plan | Hosting plan | Compute resources |
| Cosmos DB Account | Database | Task, reminder, and recurring task storage |

### Cosmos DB Setup

1. **Create Cosmos DB Account** (NoSQL API)
2. **Create Database:** `HereAndNow`
3. **Create Container:**
   - Name: `Tasks`
   - Partition Key: `/userId`
   - Throughput: 400 RU/s (minimum) or autoscale

**Note:** The application auto-creates the database and container on startup if they don't exist, so manual creation is optional.

### Document Types in Container

The single `Tasks` container stores 4 document types using a type discriminator:

| Type Value | Document | Description |
|-----------|----------|-------------|
| `Task` | TaskDocument | Regular tasks |
| `TaskReminder` | TaskReminderDocument | Time-based reminders |
| `RecurringTaskConfig` | RecurringTaskConfigDocument | Recurrence pattern definitions |
| `RecurringTaskStateOverride` | RecurringTaskStateOverrideDocument | Explicit state overrides for recurring instances |

### Application Settings

Configure these in Azure Portal → Web App → Configuration:

| Setting | Value | Description |
|---------|-------|-------------|
| PORT | 8080 | App Service default port |
| CLIENT_ORIGIN_URL | https://your-frontend.com | Production frontend URL |
| AUTH0_DOMAIN | your-tenant.auth0.com | Auth0 domain |
| AUTH0_AUDIENCE | https://your-api | Auth0 API identifier |
| COSMOS_CONNECTION_STRING | AccountEndpoint=... | CosmosDB connection |
| COSMOS_DATABASE_NAME | HereAndNow | Database name |
| COSMOS_CONTAINER_NAME | Tasks | Container name |

### Secrets Management

The deployment uses a publish profile stored in GitHub Secrets:

```
AZUREAPPSERVICE_PUBLISHPROFILE_BACC676B643B46F199F9AA3A4AF97999
```

**To update the publish profile:**
1. Azure Portal → Web App → Download publish profile
2. GitHub → Settings → Secrets → Update secret

## Manual Deployment

### Using Azure CLI

```bash
# Login to Azure
az login

# Deploy from local build
dotnet publish Web/HereAndNow.Web/HereAndNow.Web.csproj -c Release -o ./publish
az webapp deploy --resource-group <rg-name> --name here-and-now-service --src-path ./publish
```

### Using Visual Studio

1. Right-click `HereAndNow.Web` project
2. Select "Publish..."
3. Choose Azure Web App target
4. Follow wizard

## Deployment Verification

### Health Check

After deployment, verify the API is running:

```bash
# Public endpoint (always works)
curl https://here-and-now-service.azurewebsites.net/api/messages/public

# Task endpoint (requires auth + CosmosDB)
curl -H "Authorization: Bearer YOUR_TOKEN" \
  https://here-and-now-service.azurewebsites.net/api/v1/tasks
```

Expected response for public endpoint:
```json
{"text":"This is a public message."}
```

### Swagger Access

Swagger UI should be available at:
```
https://here-and-now-service.azurewebsites.net/swagger
```

### Verify CosmosDB Connection

Test by creating a task via the commands endpoint:

```bash
curl -X POST https://here-and-now-service.azurewebsites.net/api/v1/commands \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "command": "CreateTask",
    "payload": {
      "taskId": "550e8400-e29b-41d4-a716-446655440000",
      "name": "Test task"
    }
  }'
```

## Rollback Procedures

### Using Deployment Slots (Recommended)

If using deployment slots:
```bash
az webapp deployment slot swap \
  --resource-group <rg-name> \
  --name here-and-now-service \
  --slot staging \
  --target-slot production
```

### Using Previous Deployment

1. GitHub Actions → Select previous successful run
2. Re-run deployment job

### Using Azure Portal

1. Azure Portal → Web App → Deployment Center
2. View deployment history
3. Redeploy previous version

## Monitoring

### Application Logs

View logs in Azure Portal:
1. Web App → Monitoring → Log stream

Or using Azure CLI:
```bash
az webapp log tail --resource-group <rg-name> --name here-and-now-service
```

### Key Metrics

| Metric | Description | Alert Threshold |
|--------|-------------|-----------------|
| HTTP Response Time | API latency | > 2000ms |
| Request Count | Traffic volume | Baseline + 3σ |
| Error Rate (5xx) | Server errors | > 1% |
| Error Rate (4xx) | Client errors | > 10% |
| CPU Usage | Compute load | > 80% |
| Memory Usage | Memory pressure | > 80% |

### CosmosDB Metrics

Monitor in Azure Portal → Cosmos DB → Metrics:
- Request Units (RU) consumption
- 429 (throttled) responses
- Latency percentiles

Key patterns to watch:
- `GetComputedInstancesAsync` makes exactly 2 DB queries per call (configs + overrides for date range)
- `DeleteConfigWithOverridesAsync` uses chunked batches (100 items per batch) for large cascading deletes

## Environment-Specific Configuration

### Development

```env
PORT=6060
CLIENT_ORIGIN_URL=http://localhost:3000
AUTH0_DOMAIN=dev-tenant.auth0.com
AUTH0_AUDIENCE=https://dev-api
COSMOS_CONNECTION_STRING=AccountEndpoint=https://localhost:8081/...
```

### Production

Configure via Azure Application Settings:
- Use production Auth0 tenant
- Set production CLIENT_ORIGIN_URL
- Use production CosmosDB account
- Ensure HTTPS-only connections

## Security Considerations

### HTTPS

Azure Web Apps automatically provide HTTPS. Enforce HTTPS-only:
1. Azure Portal → Web App → TLS/SSL settings
2. Enable "HTTPS Only"

### Security Headers

The `SecureHeadersMiddleware` automatically adds:
- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `Content-Security-Policy`
- `Strict-Transport-Security`

### Secrets

- Never commit `.env` files
- Store CosmosDB connection string in Azure Application Settings
- Use Azure Key Vault for sensitive values (recommended)
- Rotate Auth0 secrets periodically

### Access Control

- Limit who can deploy (GitHub branch protection)
- Use Azure RBAC for portal access
- Review publish profile access regularly
- Use managed identities for CosmosDB (future enhancement)

### CosmosDB Security

- Use private endpoints in production
- Enable diagnostic logging
- Partition key `/userId` ensures tenant isolation — all queries are partition-scoped

## Troubleshooting

### Deployment Failures

| Issue | Solution |
|-------|----------|
| Build failure | Check .NET version compatibility |
| Test failure | Review test results in Actions UI |
| Publish profile invalid | Regenerate in Azure Portal |
| 502 after deploy | Check application settings, view logs |

### Runtime Issues

| Issue | Solution |
|-------|----------|
| 500 errors | Check Azure Log Stream |
| Auth failures | Verify AUTH0_* settings |
| CORS errors | Check CLIENT_ORIGIN_URL |
| Task endpoints 500 | Verify COSMOS_CONNECTION_STRING |
| Slow queries | Check CosmosDB RU consumption |
| RRULE parse errors | Verify recurrence rule format (no `RRULE:` prefix) |

### CosmosDB Issues

| Issue | Solution |
|-------|----------|
| 429 (throttled) | Increase RU/s or enable autoscale |
| Container not found | App auto-creates on startup; check connection string |
| Connection timeout | Check firewall rules, use private endpoint |

---

_Generated by BMAD document-project workflow | Exhaustive Scan | 2026-03-19_
