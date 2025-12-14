# API Contracts

## Overview

The Here and Now Service exposes a REST API for managing reminders and authentication-gated messages. All endpoints are documented using OpenAPI/Swagger and follow REST conventions.

**Base URL:** `http://localhost:{PORT}` (local) or `https://here-and-now-service.azurewebsites.net` (Azure)

**API Documentation:** Available at `/swagger` endpoint

## Authentication

All protected endpoints require JWT Bearer authentication via Auth0.

### Authentication Flow

1. Client obtains JWT token from Auth0
2. Include token in `Authorization` header: `Bearer {token}`
3. Token is validated against Auth0 authority

### Required Environment Variables

| Variable | Description |
|----------|-------------|
| `AUTH0_DOMAIN` | Auth0 tenant domain |
| `AUTH0_AUDIENCE` | API audience identifier |
| `CLIENT_ORIGIN_URL` | Allowed CORS origin |
| `PORT` | Server port |

---

## Messages API

**Base Path:** `/api/messages`
**Controller:** `MessagesController`

### GET /api/messages/public

Retrieves a public message. No authentication required.

| Attribute | Value |
|-----------|-------|
| **Method** | `GET` |
| **Auth Required** | No |
| **Response Type** | `Message` |

**Response Codes:**

| Code | Description |
|------|-------------|
| `200 OK` | Returns the public message |

**Response Schema:**

```json
{
  "text": "string"
}
```

---

### GET /api/messages/protected

Retrieves a protected message. Requires authentication.

| Attribute | Value |
|-----------|-------|
| **Method** | `GET` |
| **Auth Required** | Yes (`[Authorize]`) |
| **Response Type** | `Message` |

**Response Codes:**

| Code | Description |
|------|-------------|
| `200 OK` | Returns the protected message |
| `401 Unauthorized` | Missing or invalid token |

---

### GET /api/messages/admin

Retrieves an admin message. Requires authentication.

| Attribute | Value |
|-----------|-------|
| **Method** | `GET` |
| **Auth Required** | Yes (`[Authorize]`) |
| **Response Type** | `Message` |

**Response Codes:**

| Code | Description |
|------|-------------|
| `200 OK` | Returns the admin message |
| `401 Unauthorized` | Missing or invalid token |

---

## Reminder Instances API

**Base Path:** `/api/reminder-instances`
**Controller:** `ReminderInstancesController`
**Authorization:** All endpoints require authentication (`[Authorize]` at controller level)

### GET /api/reminder-instances

Gets all reminder instances.

| Attribute | Value |
|-----------|-------|
| **Method** | `GET` |
| **Auth Required** | Yes |
| **Response Type** | `IEnumerable<ReminderInstance>` |

**Response Codes:**

| Code | Description |
|------|-------------|
| `200 OK` | Returns list of all reminders |
| `401 Unauthorized` | Not authenticated |

**Example Response:**

```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "text": "Team standup meeting",
    "scheduledDateAndTime": "2025-12-13T09:00:00Z",
    "status": "Scheduled"
  }
]
```

---

### GET /api/reminder-instances/{id}

Gets a specific reminder instance by ID.

| Attribute | Value |
|-----------|-------|
| **Method** | `GET` |
| **Auth Required** | Yes |
| **Response Type** | `ReminderInstance` |
| **URL Parameter** | `id` (Guid) |

**Response Codes:**

| Code | Description |
|------|-------------|
| `200 OK` | Returns the reminder |
| `401 Unauthorized` | Not authenticated |
| `404 Not Found` | Reminder with given ID not found |

---

### POST /api/reminder-instances

Creates a new reminder instance.

| Attribute | Value |
|-----------|-------|
| **Method** | `POST` |
| **Auth Required** | Yes |
| **Request Body** | `ReminderInstance` |
| **Response Type** | `ReminderInstance` |

**Request Body Schema:**

```json
{
  "text": "string (required)",
  "scheduledDateAndTime": "2025-12-13T09:00:00Z",
  "status": "Scheduled | Active | Completed"
}
```

**Response Codes:**

| Code | Description |
|------|-------------|
| `201 Created` | Returns created reminder with generated ID |
| `400 Bad Request` | Invalid request body |
| `401 Unauthorized` | Not authenticated |

**Response Headers:**

- `Location`: URL of the created resource

---

### PUT /api/reminder-instances/{id}

Updates an existing reminder instance.

| Attribute | Value |
|-----------|-------|
| **Method** | `PUT` |
| **Auth Required** | Yes |
| **URL Parameter** | `id` (Guid) |
| **Request Body** | `ReminderInstance` |
| **Response Type** | `ReminderInstance` |

**Request Body Schema:**

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6 (optional, must match URL)",
  "text": "string (required)",
  "scheduledDateAndTime": "2025-12-13T09:00:00Z",
  "status": "Scheduled | Active | Completed"
}
```

**Response Codes:**

| Code | Description |
|------|-------------|
| `200 OK` | Returns updated reminder |
| `400 Bad Request` | ID mismatch between URL and body |
| `401 Unauthorized` | Not authenticated |
| `404 Not Found` | Reminder with given ID not found |

---

### DELETE /api/reminder-instances/{id}

Deletes a reminder instance.

| Attribute | Value |
|-----------|-------|
| **Method** | `DELETE` |
| **Auth Required** | Yes |
| **URL Parameter** | `id` (Guid) |

**Response Codes:**

| Code | Description |
|------|-------------|
| `204 No Content` | Successfully deleted |
| `401 Unauthorized` | Not authenticated |
| `404 Not Found` | Reminder with given ID not found |

---

## Error Responses

The API uses custom error handling middleware that provides consistent error responses.

### Standard Error Response Format

```json
{
  "message": "Error description"
}
```

### Common Error Messages

| Status | Scenario | Message |
|--------|----------|---------|
| `401` | No Authorization header | `"Requires authentication"` |
| `401` | Invalid token | `"Bad credentials"` |
| `404` | Resource not found | `"Not Found"` |
| `500` | Server error | `"Internal Server Error."` |

---

## CORS Configuration

The API accepts requests from the configured `CLIENT_ORIGIN_URL` with the following settings:

- **Allowed Headers:** `Content-Type`, `Authorization`
- **Allowed Methods:** `GET`, `POST`, `PUT`, `DELETE`
- **Preflight Cache:** 86400 seconds (24 hours)

---

## Security Headers

All responses include security headers set by `SecureHeadersMiddleware`:

| Header | Value |
|--------|-------|
| `X-XSS-Protection` | `0` |
| `Strict-Transport-Security` | `max-age=31536000; includeSubDomains` |
| `X-Frame-Options` | `deny` |
| `X-Content-Type-Options` | `nosniff` |
| `Content-Security-Policy` | `default-src 'self'; frame-ancestors 'none';` |
| `Cache-Control` | `no-cache, no-store, max-age=0, must-revalidate` |
| `Pragma` | `no-cache` |
