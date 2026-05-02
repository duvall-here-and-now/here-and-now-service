# Here and Now Service - Deployment Guide

**Date:** 2026-05-01

---

## Overview

The service is deployed to **Azure Web Apps** via GitHub Actions CI/CD. Deployment is triggered automatically on push to `main`.

---

## CI/CD Pipeline

**Workflow:** `.github/workflows/main_here-and-now-service.yml`

**Trigger:** Push to `main` branch or manual (`workflow_dispatch`)

### Pipeline Steps

```
1. Checkout code
2. Set up .NET 8
3. dotnet build HereAndNow.sln --configuration Release
4. dotnet test HereAndNow.sln --configuration Release --no-build
   ↳ Publishes TRX test results to GitHub Actions UI
   ↳ Uploads coverage reports (cobertura.xml) as artifacts
   ↳ Tests must PASS before deployment proceeds (fail-on-error: true)
5. dotnet publish Web/HereAndNow.Web/HereAndNow.Web.csproj -c Release
6. Upload artifact (.net-app)
7. Download artifact
8. azure/webapps-deploy@v3 → Production slot
   ↳ clean: true (removes old artifacts)
```

### Required GitHub Secrets

| Secret | Purpose |
|--------|---------|
| `AZUREAPPSERVICE_PUBLISHPROFILE_BACC676B643B46F199F9AA3A4AF97999` | Azure App Service publish profile |

---

## Azure Resources Required

| Resource | Name | Purpose |
|----------|------|---------|
| Azure Web App | `here-and-now-service` | Hosts the API |
| Azure Cosmos DB | — | Stores tasks, reminders, recurring configs |

### Azure App Service Configuration

Set these as Application Settings in the Azure Portal (or via CLI):

```
PORT=80
CLIENT_ORIGIN_URL=https://your-spa-domain.com
AUTH0_DOMAIN=your-domain.auth0.com
AUTH0_AUDIENCE=https://your-api-identifier
COSMOS_CONNECTION_STRING=AccountEndpoint=...;AccountKey=...
COSMOS_DATABASE_NAME=HereAndNow
COSMOS_CONTAINER_NAME=Tasks
```

---

## Cosmos DB Setup

### Container Configuration

The service **auto-creates** the database and container on startup if they don't exist:

```csharp
var database = await cosmosClient.CreateDatabaseIfNotExistsAsync("HereAndNow");
await database.Database.CreateContainerIfNotExistsAsync(
    new ContainerProperties("Tasks", "/userId"));
```

### Document Types in Container

All 4 document types co-exist in the `Tasks` container:

| Type | Discriminator | Key Pattern |
|------|--------------|-------------|
| Task | `"Task"` | GUID |
| TaskReminder | `"TaskReminder"` | GUID |
| RecurringTaskConfig | `"RecurringTaskConfig"` | GUID |
| RecurringTaskStateOverride | `"RecurringTaskStateOverride"` | `{configId}_{timestamp}` |

---

## Swagger UI Access on Azure

By default, Swagger UI is exposed at `/swagger` on production. For security, restrict access to specific IPs:

See `Web/HereAndNow.Web/SWAGGER_SETUP.md` for step-by-step Azure IP restriction configuration.

**Note:** API endpoints remain accessible publicly (Auth0 JWT still required for protected endpoints).

---

## Security Headers

`SecureHeadersMiddleware` adds the following headers to all responses:

- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `Strict-Transport-Security` (HSTS)
- `X-XSS-Protection`
- Server header is suppressed via Kestrel configuration (`AddServerHeader = false`)

---

## CORS Configuration

CORS is configured from the `CLIENT_ORIGIN_URL` environment variable. Multiple origins can be comma-separated:

```
CLIENT_ORIGIN_URL=https://app.mysite.com,https://staging.mysite.com
```

Allowed headers: `Content-Type`, `Authorization`
Allowed methods: `GET`, `POST`, `PUT`, `DELETE`
Preflight max age: 86400 seconds (24 hours)

---

## Monitoring and Troubleshooting

### Health Check

The `/api/messages/public` endpoint requires no authentication and can serve as a basic health check.

### Common Issues

| Issue | Likely Cause | Resolution |
|-------|-------------|------------|
| 401 on all protected endpoints | `AUTH0_DOMAIN` or `AUTH0_AUDIENCE` misconfigured | Check App Service env vars match Auth0 API settings |
| Task endpoints return 503 | `COSMOS_CONNECTION_STRING` missing or invalid | Verify connection string and Cosmos account status |
| CORS errors in browser | `CLIENT_ORIGIN_URL` doesn't include SPA origin | Add SPA origin to the env var (comma-separated) |
| Container not found on startup | First deploy or new environment | Container auto-created on startup — check startup logs |

### Logs

Use Azure Portal → App Service → Log stream, or Application Insights if configured.

---

## Rolling Back

If a deployment causes issues:

1. Go to Azure Portal → App Service → Deployment Center
2. View deployment history and swap to a previous deployment slot, or
3. Revert the commit on `main` to trigger a new deployment of the previous version
