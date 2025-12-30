# Here and Now Service - API Contracts

**Date:** 2025-12-30
**Base URL:** `http://localhost:{PORT}` (local) or `https://here-and-now-service.azurewebsites.net` (production)
**Authentication:** Auth0 JWT Bearer Token

## Overview

This document describes all REST API endpoints exposed by the Here and Now Service. The API uses JSON for request/response bodies and requires JWT authentication for protected endpoints.

## Authentication

Protected endpoints require a valid Auth0 JWT token in the Authorization header:

```
Authorization: Bearer <your-jwt-token>
```

Unauthenticated requests to protected endpoints return `401 Unauthorized`.

---

## Messages Controller

**Base Path:** `/api/messages`
**Source:** `Web/HereAndNow.Web/Controllers/MessagesController.cs`

### GET /api/messages/public

Retrieves a public message accessible without authentication.

**Authentication:** None required

**Response:**
```json
{
  "text": "This is a public message."
}
```

**Status Codes:**
| Code | Description |
|------|-------------|
| 200 | Success |

**Example:**
```bash
curl http://localhost:6060/api/messages/public
```

---

### GET /api/messages/protected

Retrieves a protected message accessible only to authenticated users.

**Authentication:** JWT Bearer token required

**Response:**
```json
{
  "text": "This is a protected message, and Mike is cool."
}
```

**Status Codes:**
| Code | Description |
|------|-------------|
| 200 | Success |
| 401 | Unauthorized - Missing or invalid token |

**Example:**
```bash
curl -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  http://localhost:6060/api/messages/protected
```

---

### GET /api/messages/admin

Retrieves an admin message accessible only to authenticated users.

**Authentication:** JWT Bearer token required

**Response:**
```json
{
  "text": "This is an admin message."
}
```

**Status Codes:**
| Code | Description |
|------|-------------|
| 200 | Success |
| 401 | Unauthorized - Missing or invalid token |

**Example:**
```bash
curl -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  http://localhost:6060/api/messages/admin
```

---

## Data Types

### Message

| Field | Type | Description |
|-------|------|-------------|
| text | string? | The message content |

---

## Error Responses

All error responses follow this format:

```json
{
  "message": "Error description"
}
```

### Common Error Messages

| Status | Message |
|--------|---------|
| 401 | "Requires authentication" (no token provided) |
| 401 | "Bad credentials" (invalid token) |
| 404 | "Not Found" (invalid route) |
| 500 | "Internal Server Error." |

---

## CORS Configuration

The API supports CORS for configured origins:

- **Allowed Origins:** Configured via `CLIENT_ORIGIN_URL` environment variable (comma-separated)
- **Allowed Methods:** GET, POST, PUT, DELETE
- **Allowed Headers:** Content-Type, Authorization
- **Preflight Cache:** 86400 seconds (24 hours)

---

## Swagger/OpenAPI

Interactive API documentation is available at:

- **Swagger UI:** `/swagger`
- **OpenAPI Spec:** `/swagger/v1/swagger.json`

See [SWAGGER_SETUP.md](../Web/HereAndNow.Web/SWAGGER_SETUP.md) for Azure IP restriction configuration.

---

_Generated using BMAD Method `document-project` workflow_
_Last Updated: 2025-12-30_
