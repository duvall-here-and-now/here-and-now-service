# Here and Now Service - API Contracts

**Date:** 2025-12-29
**Base URL:** `http://localhost:{PORT}`
**Authentication:** Auth0 JWT Bearer Token

## Overview

This document describes all REST API endpoints exposed by the Here and Now Service. The API uses JSON for request/response bodies and requires JWT authentication for protected endpoints.

## Authentication

All endpoints under `/api/reminder-instances` require authentication. Include a valid Auth0 JWT token in the Authorization header:

```
Authorization: Bearer <your-jwt-token>
```

Unauthenticated requests return `401 Unauthorized`.

---

## Messages Controller

**Base Path:** `/api/messages`
**Source:** `Web/HereAndNow.Web/Controllers/MessagesController.cs`

### GET /api/messages/public

Retrieves a public message (no authentication required).

**Authentication:** None

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

---

### GET /api/messages/protected

Retrieves a protected message (authentication required).

**Authentication:** Required

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

---

### GET /api/messages/admin

Retrieves an admin message (authentication required).

**Authentication:** Required

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
| 401 | Unauthorized |

---

## Reminder Instances Controller

**Base Path:** `/api/reminder-instances`
**Source:** `Web/HereAndNow.Web/Controllers/ReminderInstancesController.cs`
**Authentication:** All endpoints require JWT authentication

### GET /api/reminder-instances

Retrieves all reminder instances.

**Response:**
```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "text": "Meeting reminder",
    "scheduledDateAndTime": "2025-12-30T10:00:00Z",
    "isCompleted": false,
    "isDeleted": false,
    "shouldPlaySound": true,
    "shouldDoVibration": false,
    "state": "Scheduled"
  }
]
```

**Status Codes:**
| Code | Description |
|------|-------------|
| 200 | Success - Returns array of reminders |
| 401 | Unauthorized |

---

### GET /api/reminder-instances/{id}

Retrieves a specific reminder instance by ID.

**Path Parameters:**
| Name | Type | Description |
|------|------|-------------|
| id | GUID | Unique identifier of the reminder |

**Response:**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "text": "Meeting reminder",
  "scheduledDateAndTime": "2025-12-30T10:00:00Z",
  "isCompleted": false,
  "isDeleted": false,
  "shouldPlaySound": true,
  "shouldDoVibration": false,
  "state": "Scheduled"
}
```

**Status Codes:**
| Code | Description |
|------|-------------|
| 200 | Success |
| 401 | Unauthorized |
| 404 | Not Found - Reminder does not exist |

---

### POST /api/reminder-instances

Creates a new reminder instance.

**Request Body:**
```json
{
  "text": "New reminder",
  "scheduledDateAndTime": "2025-12-30T10:00:00Z",
  "isCompleted": false,
  "isDeleted": false,
  "shouldPlaySound": true,
  "shouldDoVibration": false
}
```

**Response:** `201 Created` with Location header
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "text": "New reminder",
  "scheduledDateAndTime": "2025-12-30T10:00:00Z",
  "isCompleted": false,
  "isDeleted": false,
  "shouldPlaySound": true,
  "shouldDoVibration": false,
  "state": "Scheduled"
}
```

**Status Codes:**
| Code | Description |
|------|-------------|
| 201 | Created - Returns new reminder with generated ID |
| 400 | Bad Request - Invalid request body |
| 401 | Unauthorized |

---

### PUT /api/reminder-instances/{id}

Updates an existing reminder instance.

**Path Parameters:**
| Name | Type | Description |
|------|------|-------------|
| id | GUID | Unique identifier of the reminder |

**Request Body:**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "text": "Updated reminder text",
  "scheduledDateAndTime": "2025-12-30T14:00:00Z",
  "isCompleted": true,
  "isDeleted": false,
  "shouldPlaySound": false,
  "shouldDoVibration": true
}
```

**Response:**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "text": "Updated reminder text",
  "scheduledDateAndTime": "2025-12-30T14:00:00Z",
  "isCompleted": true,
  "isDeleted": false,
  "shouldPlaySound": false,
  "shouldDoVibration": true,
  "state": "Completed"
}
```

**Status Codes:**
| Code | Description |
|------|-------------|
| 200 | Success - Returns updated reminder |
| 400 | Bad Request - ID mismatch between URL and body |
| 401 | Unauthorized |
| 404 | Not Found - Reminder does not exist |

---

### DELETE /api/reminder-instances/{id}

Soft-deletes a reminder instance (sets `isDeleted` to true).

**Path Parameters:**
| Name | Type | Description |
|------|------|-------------|
| id | GUID | Unique identifier of the reminder |

**Response:** `204 No Content`

**Status Codes:**
| Code | Description |
|------|-------------|
| 204 | No Content - Successfully deleted |
| 401 | Unauthorized |
| 404 | Not Found - Reminder does not exist |

---

## Data Types

### ReminderInstanceDto

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| id | GUID | No (generated on create) | Unique identifier |
| text | string | Yes | Reminder text content |
| scheduledDateAndTime | DateTime | Yes | When the reminder triggers |
| isCompleted | boolean | No (default: false) | Completion status |
| isDeleted | boolean | No (default: false) | Soft-delete status |
| shouldPlaySound | boolean | No (default: false) | Audio notification |
| shouldDoVibration | boolean | No (default: false) | Vibration notification |
| state | ReminderState | Read-only | Computed state |

### ReminderState (Enum)

| Value | Description |
|-------|-------------|
| Scheduled | Future reminder, not yet triggered |
| Active | Time has passed, awaiting action |
| Completed | Marked as done |
| Deleted | Soft-deleted |

### Message

| Field | Type | Description |
|-------|------|-------------|
| text | string | Message content |

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
| 401 | "Requires authentication" (no token) |
| 401 | "Bad credentials" (invalid token) |
| 404 | "Not Found" or "Reminder with ID {id} not found." |
| 400 | "ID in URL and body do not match." |
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

---

_Generated using BMAD Method `document-project` workflow_
