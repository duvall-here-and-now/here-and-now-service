# API Contracts

> REST API endpoint documentation for Here and Now Service

---

## Overview

| Attribute | Value |
|-----------|-------|
| **Base URL** | `/api` |
| **Authentication** | Auth0 JWT Bearer Token |
| **Content Type** | `application/json` |
| **API Documentation** | Swagger UI at `/swagger` |
| **Total Endpoints** | 8 |

---

## Authentication

All endpoints except `/api/messages/public` require a valid JWT Bearer token:

```http
Authorization: Bearer <your-auth0-jwt-token>
```

The token must be issued by the configured Auth0 domain and contain the correct audience claim.

### User Identification

Authenticated endpoints extract the user ID from the JWT `sub` claim (or `ClaimTypes.NameIdentifier`). This user ID:
- Serves as the partition key in Cosmos DB for efficient multi-tenant data isolation
- Ensures users can only access their own reminders
- Is automatically associated with created/updated reminders

### Required Environment Variables

| Variable | Description |
|----------|-------------|
| `AUTH0_DOMAIN` | Auth0 tenant domain |
| `AUTH0_AUDIENCE` | API audience identifier |
| `CLIENT_ORIGIN_URL` | Allowed CORS origins (comma-separated) |
| `PORT` | Server port |

---

## Endpoints

### Messages Controller

**Base Path:** `/api/messages`
**Controller:** `MessagesController.cs:13`

#### GET /api/messages/public

Returns a public message accessible without authentication.

| Attribute | Value |
|-----------|-------|
| **Auth Required** | No |
| **Method** | GET |

**Response (200 OK):**
```json
{
  "text": "This is a public message."
}
```

---

#### GET /api/messages/protected

Returns a protected message requiring authentication.

| Attribute | Value |
|-----------|-------|
| **Auth Required** | Yes |
| **Method** | GET |

**Response (200 OK):**
```json
{
  "text": "This is a protected message, and Mike is cool."
}
```

| Status Code | Description |
|-------------|-------------|
| 401 Unauthorized | Missing or invalid token |

---

#### GET /api/messages/admin

Returns an admin message requiring authentication.

| Attribute | Value |
|-----------|-------|
| **Auth Required** | Yes |
| **Method** | GET |

**Response (200 OK):**
```json
{
  "text": "This is an admin message."
}
```

| Status Code | Description |
|-------------|-------------|
| 401 Unauthorized | Missing or invalid token |

---

### Reminder Instances Controller

**Base Path:** `/api/reminder-instances`
**Controller:** `ReminderInstancesController.cs:16`
**Authorization:** All endpoints require authentication (controller-level `[Authorize]`)

---

#### GET /api/reminder-instances

Returns all reminder instances for the authenticated user.

| Attribute | Value |
|-----------|-------|
| **Auth Required** | Yes |
| **Method** | GET |

**Response (200 OK):**
```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "text": "Take medication",
    "scheduledDateAndTime": "2025-12-17T10:00:00Z",
    "isCompleted": false,
    "isDeleted": false,
    "shouldPlaySound": true,
    "shouldDoVibration": false,
    "state": "Scheduled"
  }
]
```

| Status Code | Description |
|-------------|-------------|
| 401 Unauthorized | Missing or invalid token |
| 503 Service Unavailable | Cosmos DB unavailable |

---

#### GET /api/reminder-instances/{id}

Returns a specific reminder instance by ID.

| Attribute | Value |
|-----------|-------|
| **Auth Required** | Yes |
| **Method** | GET |
| **Path Parameter** | `id` (GUID) |

**Response (200 OK):**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "text": "Take medication",
  "scheduledDateAndTime": "2025-12-17T10:00:00Z",
  "isCompleted": false,
  "isDeleted": false,
  "shouldPlaySound": true,
  "shouldDoVibration": false,
  "state": "Scheduled"
}
```

| Status Code | Description |
|-------------|-------------|
| 401 Unauthorized | Missing or invalid token |
| 404 Not Found | Reminder not found or belongs to different user |
| 503 Service Unavailable | Cosmos DB unavailable |

---

#### POST /api/reminder-instances

Creates a new reminder instance for the authenticated user.

| Attribute | Value |
|-----------|-------|
| **Auth Required** | Yes |
| **Method** | POST |
| **Content-Type** | application/json |

**Request Body:**
```json
{
  "text": "Take medication",
  "scheduledDateAndTime": "2025-12-17T10:00:00Z",
  "isCompleted": false,
  "isDeleted": false,
  "shouldPlaySound": true,
  "shouldDoVibration": false
}
```

**Response (201 Created):**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "text": "Take medication",
  "scheduledDateAndTime": "2025-12-17T10:00:00Z",
  "isCompleted": false,
  "isDeleted": false,
  "shouldPlaySound": true,
  "shouldDoVibration": false,
  "state": "Scheduled"
}
```

| Status Code | Description |
|-------------|-------------|
| 400 Bad Request | Invalid request body |
| 401 Unauthorized | Missing or invalid token |
| 503 Service Unavailable | Cosmos DB unavailable |

**Note:** The `id` field in the request is ignored; a new GUID is generated server-side.

---

#### PUT /api/reminder-instances/{id}

Updates an existing reminder instance.

| Attribute | Value |
|-----------|-------|
| **Auth Required** | Yes |
| **Method** | PUT |
| **Path Parameter** | `id` (GUID) |
| **Content-Type** | application/json |

**Request Body:**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "text": "Take medication (updated)",
  "scheduledDateAndTime": "2025-12-17T11:00:00Z",
  "isCompleted": true,
  "isDeleted": false,
  "shouldPlaySound": false,
  "shouldDoVibration": true
}
```

**Response (200 OK):**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "text": "Take medication (updated)",
  "scheduledDateAndTime": "2025-12-17T11:00:00Z",
  "isCompleted": true,
  "isDeleted": false,
  "shouldPlaySound": false,
  "shouldDoVibration": true,
  "state": "Completed"
}
```

| Status Code | Description |
|-------------|-------------|
| 400 Bad Request | ID mismatch between URL and body |
| 401 Unauthorized | Missing or invalid token |
| 404 Not Found | Reminder not found or belongs to different user |
| 503 Service Unavailable | Cosmos DB unavailable |

---

#### DELETE /api/reminder-instances/{id}

Soft-deletes a reminder instance (sets `isDeleted = true`).

| Attribute | Value |
|-----------|-------|
| **Auth Required** | Yes |
| **Method** | DELETE |
| **Path Parameter** | `id` (GUID) |

| Status Code | Description |
|-------------|-------------|
| 204 No Content | Reminder soft-deleted successfully |
| 401 Unauthorized | Missing or invalid token |
| 404 Not Found | Reminder not found or belongs to different user |
| 503 Service Unavailable | Cosmos DB unavailable |

---

## Response Schemas

### ReminderInstanceDto

```typescript
{
  id: string;                    // GUID
  text: string;                  // Required
  scheduledDateAndTime: string;  // ISO 8601 datetime
  isCompleted: boolean;
  isDeleted: boolean;
  shouldPlaySound: boolean;
  shouldDoVibration: boolean;
  state: "Scheduled" | "Active" | "Completed" | "Deleted";  // Computed
}
```

### ReminderState (Computed Property)

The `state` field is computed based on the reminder's flags and scheduled time:

| Priority | Condition | State |
|----------|-----------|-------|
| 1 | `isDeleted == true` | `Deleted` |
| 2 | `isCompleted == true` | `Completed` |
| 3 | `now >= scheduledDateAndTime` | `Active` |
| 4 | Otherwise | `Scheduled` |

### Message

```typescript
{
  text: string | null;
}
```

---

## Error Responses

### Standard Error Format

```json
{
  "message": "Error description"
}
```

### Authentication Errors (401)

Without Authorization header:
```json
{
  "message": "Requires authentication"
}
```

With invalid token:
```json
{
  "message": "Bad credentials"
}
```

### Service Unavailable (503)

Cosmos DB issues (ServiceUnavailable, RequestTimeout, GatewayTimeout, InternalServerError):
```json
{
  "message": "The reminder service is temporarily unavailable. Please try again later."
}
```

---

## Security Headers

All responses include security headers (via `SecureHeadersMiddleware`):

| Header | Value |
|--------|-------|
| X-XSS-Protection | 0 |
| Strict-Transport-Security | max-age=31536000; includeSubDomains |
| X-Frame-Options | deny |
| X-Content-Type-Options | nosniff |
| Content-Security-Policy | default-src 'self'; frame-ancestors 'none'; |
| Cache-Control | no-cache, no-store, max-age=0, must-revalidate |
| Pragma | no-cache |

---

## CORS Configuration

| Setting | Value |
|---------|-------|
| Allowed Origins | Configured via `CLIENT_ORIGIN_URL` (comma-separated) |
| Allowed Methods | GET, POST, PUT, DELETE |
| Allowed Headers | Content-Type, Authorization |
| Preflight Max Age | 86400 seconds (24 hours) |

---

## Rate Limiting & Retry

The Cosmos DB client is configured with SDK-level retry for 429 (TooManyRequests):

| Setting | Value |
|---------|-------|
| Max Retry Attempts | 9 |
| Max Retry Wait Time | 30 seconds |

---

## Documentation Metadata

| Field | Value |
|-------|-------|
| **Generated** | 2025-12-17 |
| **Scan Level** | Exhaustive |
| **Source Files Analyzed** | All controllers, services, DTOs |
