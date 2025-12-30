# Here and Now Service - Deployment Guide

**Date:** 2025-12-29

## Overview

The Here and Now Service is deployed to Azure Web Apps using GitHub Actions. The CI/CD pipeline is triggered on every push to the `main` branch.

## Deployment Architecture

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│    GitHub       │────►│  GitHub Actions │────►│  Azure Web App  │
│   (main branch) │     │                 │     │  (Production)   │
└─────────────────┘     └─────────────────┘     └─────────────────┘
                               │
                               ▼
                        ┌─────────────┐
                        │   Tests     │
                        │   (Gate)    │
                        └─────────────┘
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
| Test | `dotnet test --configuration Release` | Run all tests |
| Test Report | `dorny/test-reporter@v1` | Publish results to GitHub |
| Coverage | `actions/upload-artifact@v4` | Upload coverage reports |
| Publish | `dotnet publish -c Release` | Create deployment package |
| Deploy | `azure/webapps-deploy@v3` | Deploy to Azure |

### Trigger Conditions

```yaml
on:
  push:
    branches:
      - main
  workflow_dispatch:  # Manual trigger
```

## Quality Gates

### Test Requirements

- All tests must pass before deployment
- Test results are published to GitHub Actions UI
- Failed tests block deployment

### Test Reporting

Test results are visible in:
1. GitHub Actions workflow run
2. Pull Request checks (if opened)

## Azure Configuration

### Required Azure Resources

| Resource | Type | Purpose |
|----------|------|---------|
| here-and-now-service | Azure Web App | Application hosting |
| App Service Plan | Hosting plan | Compute resources |

### Application Settings

Configure these in Azure Portal → Web App → Configuration:

| Setting | Value | Description |
|---------|-------|-------------|
| PORT | 8080 | App Service default port |
| CLIENT_ORIGIN_URL | https://your-frontend.com | Production frontend URL |
| AUTH0_DOMAIN | your-tenant.auth0.com | Auth0 domain |
| AUTH0_AUDIENCE | https://your-api | Auth0 API identifier |

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
curl https://here-and-now-service.azurewebsites.net/api/messages/public
```

Expected response:
```json
{"text":"This is a public message."}
```

### Swagger Access

Swagger UI should be available at:
```
https://here-and-now-service.azurewebsites.net/swagger
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

### Metrics

Key metrics to monitor:
- HTTP response time
- Request count
- Error rate (4xx, 5xx)
- CPU/Memory usage

## Environment-Specific Configuration

### Development

```env
PORT=6060
CLIENT_ORIGIN_URL=http://localhost:3000
AUTH0_DOMAIN=dev-tenant.auth0.com
AUTH0_AUDIENCE=https://dev-api
```

### Production

Configure via Azure Application Settings:
- Use production Auth0 tenant
- Set production CLIENT_ORIGIN_URL
- Ensure HTTPS-only connections

## Security Considerations

### HTTPS

Azure Web Apps automatically provide HTTPS. Enforce HTTPS-only:
1. Azure Portal → Web App → TLS/SSL settings
2. Enable "HTTPS Only"

### Secrets

- Never commit `.env` files
- Use Azure Key Vault for sensitive values (future enhancement)
- Rotate Auth0 secrets periodically

### Access Control

- Limit who can deploy (GitHub branch protection)
- Use Azure RBAC for portal access
- Review publish profile access regularly

## Troubleshooting

### Deployment Failures

| Issue | Solution |
|-------|----------|
| Build failure | Check .NET version compatibility |
| Test failure | Review test results in Actions |
| Publish profile invalid | Regenerate in Azure Portal |
| 502 after deploy | Check application settings, view logs |

### Runtime Issues

| Issue | Solution |
|-------|----------|
| 500 errors | Check Azure Log Stream |
| Auth failures | Verify AUTH0_* settings |
| CORS errors | Check CLIENT_ORIGIN_URL |

## Future Enhancements

1. **Deployment Slots**: Add staging slot for zero-downtime deployments
2. **Blue/Green**: Implement blue/green deployment pattern
3. **Feature Flags**: Add LaunchDarkly or Azure App Configuration
4. **Containerization**: Docker-based deployment option

---

_Generated using BMAD Method `document-project` workflow_
