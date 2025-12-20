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
| **Total Endpoints** | 9 |
| **Data Storage** | Azure Cosmos DB (Required) |

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
| `COSMOS_ENDPOINT` | Azure Cosmos DB endpoint URL (Required) |
| `COSMOS_PRIMARY_KEY` | Azure Cosmos DB primary key (Required) |
| `COSMOS_DATABASE_NAME` | Database name |
| `COSMOS_CONTAINER_NAME` | Container name |

> **Note:** As of Story 1.3, Cosmos DB is required. The application throws `InvalidOperationException` at startup if `COSMOS_ENDPOINT` or `COSMOS_PRIMARY_KEY` are missing.

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
    "scheduledDateAndTime": "2025-12-18T10:00:00Z",
    "isCompleted": false,
    "isDeleted": false,
    "shouldPlaySound": true,
    "shouldDoVibration": false,
    "createdDateAndTime": "2025-12-18T09:30:00Z",
    "completedDateAndTime": null,
    "deletedDateAndTime": null,
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
  "scheduledDateAndTime": "2025-12-18T10:00:00Z",
  "isCompleted": false,
  "isDeleted": false,
  "shouldPlaySound": true,
  "shouldDoVibration": false,
  "createdDateAndTime": "2025-12-18T09:30:00Z",
  "completedDateAndTime": null,
  "deletedDateAndTime": null,
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

Creates a new reminder instance for the authenticated user. Client must provide the reminder ID (UUID) for idempotent operations.

| Attribute | Value |
|-----------|-------|
| **Auth Required** | Yes |
| **Method** | POST |
| **Content-Type** | application/json |
| **Request DTO** | `CreateReminderRequest` |

**Request Body:**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "text": "Take medication",
  "scheduledDateAndTime": "2025-12-18T10:00:00Z",
  "shouldPlaySound": true,
  "shouldDoVibration": false
}
```

**Response (201 Created):**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "text": "Take medication",
  "scheduledDateAndTime": "2025-12-18T10:00:00Z",
  "isCompleted": false,
  "isDeleted": false,
  "shouldPlaySound": true,
  "shouldDoVibration": false,
  "createdDateAndTime": "2025-12-18T09:30:00Z",
  "completedDateAndTime": null,
  "deletedDateAndTime": null,
  "state": "Scheduled"
}
```

| Status Code | Description |
|-------------|-------------|
| 400 Bad Request | Invalid request body (missing id or text, etc.) |
| 401 Unauthorized | Missing or invalid token |
| 409 Conflict | Reminder with provided ID already exists |
| 503 Service Unavailable | Cosmos DB unavailable |

**Client-Provided ID:** The `id` field is required and must be a valid UUID provided by the client. This enables:
- Idempotent create operations (safe to retry)
- Offline-first patterns (pre-generate IDs)
- Client-server correlation before confirmation

**Server-Controlled Fields:** The following fields are set by the server and cannot be provided in the request:
- `isCompleted` — Always `false` on creation
- `isDeleted` — Always `false` on creation
- `createdDateAndTime` — Set to current UTC time
- `completedDateAndTime` — Always `null` on creation
- `deletedDateAndTime` — Always `null` on creation

---

#### PATCH /api/reminder-instances/{id}

Partially updates an existing reminder instance. Only provided fields will be updated.

| Attribute | Value |
|-----------|-------|
| **Auth Required** | Yes |
| **Method** | PATCH |
| **Path Parameter** | `id` (GUID) |
| **Content-Type** | application/json |
| **Request DTO** | `UpdateReminderRequest` |

**Request Body (all fields optional):**
```json
{
  "text": "Take medication with food",
  "scheduledDateAndTime": "2025-12-18T12:00:00Z"
}
```

**Response (200 OK):**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "text": "Take medication with food",
  "scheduledDateAndTime": "2025-12-18T12:00:00Z",
  "isCompleted": false,
  "isDeleted": false,
  "shouldPlaySound": true,
  "shouldDoVibration": false,
  "createdDateAndTime": "2025-12-18T09:30:00Z",
  "completedDateAndTime": null,
  "deletedDateAndTime": null,
  "state": "Scheduled"
}
```

| Status Code | Description |
|-------------|-------------|
| 400 Bad Request | Invalid field value (empty text) OR attempting to update `scheduledDateAndTime` on non-Scheduled reminder |
| 401 Unauthorized | Missing or invalid token |
| 404 Not Found | Reminder not found, deleted, or belongs to different user |
| 503 Service Unavailable | Cosmos DB unavailable |

**Partial Update Semantics:**
- Only fields included in the request are updated
- Fields set to `null` or omitted retain their current values
- State flags (`isCompleted`, `isDeleted`) cannot be modified via this endpoint — use Complete or Delete endpoints instead

**State Validation for ScheduledDateAndTime:**
The `scheduledDateAndTime` field can only be updated when the reminder is in the **Scheduled** state:

| Current State | Can Update Time? | Error Message |
|---------------|------------------|---------------|
| Scheduled | ✅ Yes | — |
| Active | ❌ No | "Cannot update scheduled time. Reminder is in 'Active' state." |
| Completed | ❌ No | "Cannot update scheduled time. Reminder is in 'Completed' state." |
| Deleted | ❌ No | Returns 404 Not Found |

Other fields (`text`, `shouldPlaySound`, `shouldDoVibration`) can be updated in any non-deleted state.

---

#### POST /api/reminder-instances/{id}/complete

Marks a reminder as completed and records the completion timestamp.

| Attribute | Value |
|-----------|-------|
| **Auth Required** | Yes |
| **Method** | POST |
| **Path Parameter** | `id` (GUID) |

**Request Body:** None required

**Response (200 OK):**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "text": "Take medication",
  "scheduledDateAndTime": "2025-12-18T10:00:00Z",
  "isCompleted": true,
  "isDeleted": false,
  "shouldPlaySound": true,
  "shouldDoVibration": false,
  "createdDateAndTime": "2025-12-18T09:30:00Z",
  "completedDateAndTime": "2025-12-18T10:15:00Z",
  "deletedDateAndTime": null,
  "state": "Completed"
}
```

| Status Code | Description |
|-------------|-------------|
| 401 Unauthorized | Missing or invalid token |
| 404 Not Found | Reminder not found or belongs to different user |
| 409 Conflict | Reminder is deleted (cannot complete a deleted reminder) |
| 503 Service Unavailable | Cosmos DB unavailable |

**Idempotency:** This operation is idempotent. Calling complete on an already-completed reminder returns 200 OK with the current state.

---

#### DELETE /api/reminder-instances/{id}

Soft-deletes a reminder instance by setting `isDeleted = true` and recording the deletion timestamp.

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

**Server Actions:**
- Sets `isDeleted = true`
- Sets `deletedDateAndTime` to current UTC time

**Idempotency:** This operation is idempotent. Deleting an already-deleted reminder returns 204 No Content.

---

## Request Schemas

### CreateReminderRequest

Used by: `POST /api/reminder-instances`

```typescript
{
  id: string;                      // Required, UUID (client-provided for idempotency)
  text: string;                    // Required, 1-1000 characters
  scheduledDateAndTime: string;    // Required, ISO 8601 datetime
  shouldPlaySound: boolean;        // Optional, defaults to false
  shouldDoVibration: boolean;      // Optional, defaults to false
}
```

> **Breaking Change (Story 1.1):** The `id` field is now required. Clients must generate their own UUID before creating reminders.

### UpdateReminderRequest

Used by: `PATCH /api/reminder-instances/{id}`

```typescript
{
  text?: string;                    // Optional, 1-1000 characters if provided
  scheduledDateAndTime?: string;    // Optional, ISO 8601 datetime
  shouldPlaySound?: boolean;        // Optional
  shouldDoVibration?: boolean;      // Optional
}
```

**Note:** All fields are nullable. Only non-null fields are updated; omitted fields retain their current values.

> **Behavior Change (Story 1.2):** The `scheduledDateAndTime` field can only be updated when the reminder is in the "Scheduled" state. Attempting to update it on Active or Completed reminders returns 400 Bad Request.

---

## Response Schemas

### ReminderInstanceDto

```typescript
{
  id: string;                         // GUID
  text: string;                       // Required
  scheduledDateAndTime: string;       // ISO 8601 datetime
  isCompleted: boolean;
  isDeleted: boolean;
  shouldPlaySound: boolean;
  shouldDoVibration: boolean;
  createdDateAndTime: string;         // ISO 8601 datetime, set on creation
  completedDateAndTime: string | null; // ISO 8601 datetime, set when completed
  deletedDateAndTime: string | null;   // ISO 8601 datetime, set when deleted
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
| Allowed Methods | GET, POST, PATCH, DELETE |
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

## Recent API Changes

### Story 1.3 (2025-12-19) - Cosmos DB Required

- **Breaking:** In-memory fallback removed; Cosmos DB is now required
- **Breaking:** `COSMOS_ENDPOINT` and `COSMOS_PRIMARY_KEY` environment variables are required
- Application throws `InvalidOperationException` at startup if Cosmos DB is not configured

### Story 1.2 (2025-12-19) - ScheduledDateAndTime State Validation

- **Behavior Change:** PATCH endpoint returns 400 Bad Request when updating `scheduledDateAndTime` on non-Scheduled reminders
- State validation: Only reminders in "Scheduled" state can have their time changed

### Story 1.1 (2025-12-19) - Client-Provided UUID

- **Breaking:** `id` field is now required in `CreateReminderRequest`
- POST endpoint returns 409 Conflict when a reminder with the provided ID already exists
- Enables idempotent create operations and offline-first patterns

---

## Documentation Metadata

| Field | Value |
|-------|-------|
| **Last Updated** | 2025-12-19 |
| **Scan Level** | Exhaustive |
| **Workflow** | document-project v1.2.0 |
| **Source Files Analyzed** | All controllers, services, DTOs |
