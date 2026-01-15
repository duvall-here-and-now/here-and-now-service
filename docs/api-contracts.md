# Here and Now Service - API Contracts

**Date:** 2026-01-15
**Base URL:** `http://localhost:{PORT}` (local) or `https://here-and-now-service.azurewebsites.net` (production)
**Authentication:** Auth0 JWT Bearer Token

## Overview

This document describes all REST API endpoints exposed by the Here and Now Service. The API uses JSON for request/response bodies and requires JWT authentication for protected endpoints.

| Controller | Base Path | Endpoints | Purpose |
|------------|-----------|-----------|---------|
| Messages | `/api/messages` | 3 | Auth0 demo endpoints |
| Tasks | `/api/v1/tasks` | 6 | Task management with state machine |
| Reminders | `/api/v1/reminders` | 5 | Reminder management with snooze/dismiss |

## Authentication

Protected endpoints require a valid Auth0 JWT token in the Authorization header:

```
Authorization: Bearer <your-jwt-token>
```

Unauthenticated requests to protected endpoints return `401 Unauthorized`.

---

## Error Response Format

All errors follow a consistent format:

```json
{
  "error": {
    "code": "TASK_NOT_FOUND",
    "message": "Task with ID abc123 not found"
  }
}
```

### Error Codes

| Code | HTTP Status | Description |
|------|-------------|-------------|
| `TASK_NOT_FOUND` | 404 | Task with given ID not found |
| `REMINDER_NOT_FOUND` | 404 | Reminder with given ID not found |
| `REMINDER_ALREADY_EXISTS` | 400 | Task already has a reminder attached |
| `REMINDER_ALREADY_DISMISSED` | 400 | Cannot modify a dismissed reminder |
| `INVALID_SCHEDULED_TIME` | 400 | Scheduled time must be in the future |
| `VALIDATION_ERROR` | 400 | General validation failure |
| `UNITY_TRANSACTION_FAILED` | 500 | Atomic Task+Reminder operation failed |

---

## Messages Controller

**Base Path:** `/api/messages`
**Source:** `Web/HereAndNow.Web/Controllers/MessagesController.cs`

Demo endpoints for Auth0 authentication testing.

### GET /api/messages/public

Retrieves a public message accessible without authentication.

**Authentication:** None required

**Response:** `200 OK`
```json
{
  "text": "This is a public message."
}
```

---

### GET /api/messages/protected

Retrieves a protected message accessible only to authenticated users.

**Authentication:** JWT Bearer token required

**Response:** `200 OK`
```json
{
  "text": "This is a protected message."
}
```

**Status Codes:**
| Code | Description |
|------|-------------|
| 200 | Success |
| 401 | Unauthorized - Missing or invalid token |

---

### GET /api/messages/admin

Retrieves an admin message accessible only to authenticated users.

**Authentication:** JWT Bearer token required

**Response:** `200 OK`
```json
{
  "text": "This is an admin message."
}
```

---

## Tasks Controller

**Base Path:** `/api/v1/tasks`
**Source:** `Web/HereAndNow.Web/Controllers/TasksController.cs`

Full CRUD for task management with state machine and Unity operations.

### POST /api/v1/tasks

Creates a new task, optionally with an associated reminder.

**Authentication:** JWT Bearer token required

**Request Body:**
```json
{
  "name": "Buy groceries",
  "scheduledTime": "2026-01-20T10:00:00Z"
}
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `name` | string | Yes | 1-500 characters |
| `scheduledTime` | DateTime | No | Must be UTC and in the future. Creates reminder if provided. |

**Response:** `201 Created`
```json
{
  "id": "abc123",
  "name": "Buy groceries",
  "state": "OnDeck",
  "createdAt": "2026-01-15T10:00:00Z",
  "completedAt": null,
  "reminderId": "xyz789",
  "lastModifiedAt": "2026-01-15T10:00:00Z"
}
```

**Example:**
```bash
curl -X POST http://localhost:6060/api/v1/tasks \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"name": "Buy groceries", "scheduledTime": "2026-01-20T10:00:00Z"}'
```

---

### GET /api/v1/tasks

Retrieves paginated list of tasks with sorting and filtering.

**Authentication:** JWT Bearer token required

**Query Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `state` | string | null | Filter by state: `OnDeck`, `InProgress`, `Completed` |
| `orderBy` | string | `createdAt` | Sort field: `createdAt`, `completedAt` |
| `direction` | string | `asc` | Sort direction: `asc`, `desc` |
| `skip` | int | 0 | Number of items to skip |
| `take` | int | 50 | Items per page (max 100) |

**Response:** `200 OK`
```json
{
  "items": [
    {
      "id": "abc123",
      "name": "Buy groceries",
      "state": "OnDeck",
      "createdAt": "2026-01-15T10:00:00Z",
      "completedAt": null,
      "reminderId": "xyz789",
      "lastModifiedAt": "2026-01-15T10:00:00Z"
    }
  ],
  "totalCount": 42,
  "hasMore": true
}
```

**Example:**
```bash
curl "http://localhost:6060/api/v1/tasks?state=OnDeck&orderBy=createdAt&direction=desc&take=10" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN"
```

---

### GET /api/v1/tasks/{id}

Retrieves a single task by ID.

**Authentication:** JWT Bearer token required

**Path Parameters:**
| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | string | Task ID (GUID) |

**Response:** `200 OK`
```json
{
  "id": "abc123",
  "name": "Buy groceries",
  "state": "OnDeck",
  "createdAt": "2026-01-15T10:00:00Z",
  "completedAt": null,
  "reminderId": "xyz789",
  "lastModifiedAt": "2026-01-15T10:00:00Z"
}
```

**Status Codes:**
| Code | Error Code | Description |
|------|------------|-------------|
| 200 | - | Success |
| 404 | `TASK_NOT_FOUND` | Task not found |

---

### PUT /api/v1/tasks/{id}

Updates a task's name and/or state.

**Authentication:** JWT Bearer token required

**Request Body:**
```json
{
  "name": "Buy groceries and milk",
  "state": "InProgress"
}
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `name` | string | No | Cannot be empty/whitespace if provided |
| `state` | string | No | `OnDeck`, `InProgress`, `Completed`, `Deleted` |

> **Note:** At least one field must be provided. If the task has a reminder and `name` is updated, the reminder's `taskName` is synced atomically.

**Response:** `200 OK` - Returns updated task

**Status Codes:**
| Code | Error Code | Description |
|------|------------|-------------|
| 200 | - | Success |
| 400 | `VALIDATION_ERROR` | Invalid request |
| 404 | `TASK_NOT_FOUND` | Task not found |

---

### PUT /api/v1/tasks/{id}/complete

Completes a task with Unity operation. If the task has a reminder, it is atomically dismissed.

**Authentication:** JWT Bearer token required

**Response:** `200 OK`
```json
{
  "id": "abc123",
  "name": "Buy groceries",
  "state": "Completed",
  "createdAt": "2026-01-15T10:00:00Z",
  "completedAt": "2026-01-15T14:30:00Z",
  "reminderId": null,
  "lastModifiedAt": "2026-01-15T14:30:00Z"
}
```

> **Unity Pattern:** The `reminderId` is cleared after the reminder is dismissed atomically.

**Status Codes:**
| Code | Error Code | Description |
|------|------------|-------------|
| 200 | - | Success |
| 404 | `TASK_NOT_FOUND` | Task not found |
| 500 | `UNITY_TRANSACTION_FAILED` | Atomic operation failed |

---

### DELETE /api/v1/tasks/{id}

Soft-deletes a task with Unity operation. If the task has a reminder, it is atomically dismissed.

**Authentication:** JWT Bearer token required

**Response:** `204 No Content`

**Status Codes:**
| Code | Error Code | Description |
|------|------------|-------------|
| 204 | - | Successfully deleted |
| 404 | `TASK_NOT_FOUND` | Task not found |
| 500 | `UNITY_TRANSACTION_FAILED` | Atomic operation failed |

---

## Reminders Controller

**Base Path:** `/api/v1/reminders`
**Source:** `Web/HereAndNow.Web/Controllers/RemindersController.cs`

Reminder management with snooze and dismiss functionality.

### POST /api/v1/reminders

Creates a reminder for an existing task.

**Authentication:** JWT Bearer token required

**Request Body:**
```json
{
  "taskId": "abc123",
  "scheduledTime": "2026-01-20T10:00:00Z"
}
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `taskId` | string | Yes | Must reference an existing task |
| `scheduledTime` | DateTime | Yes | Must be UTC |

**Response:** `201 Created`
```json
{
  "id": "xyz789",
  "taskId": "abc123",
  "taskName": "Buy groceries",
  "scheduledTime": "2026-01-20T10:00:00Z",
  "isDismissed": false,
  "dismissedAt": null,
  "createdAt": "2026-01-15T10:00:00Z",
  "lastModifiedAt": "2026-01-15T10:00:00Z"
}
```

**Status Codes:**
| Code | Error Code | Description |
|------|------------|-------------|
| 201 | - | Created successfully |
| 400 | `REMINDER_ALREADY_EXISTS` | Task already has a reminder |
| 404 | `TASK_NOT_FOUND` | Task not found |

---

### GET /api/v1/reminders

Retrieves all non-dismissed reminders sorted by scheduled time.

**Authentication:** JWT Bearer token required

**Response:** `200 OK`
```json
[
  {
    "id": "xyz789",
    "taskId": "abc123",
    "taskName": "Buy groceries",
    "scheduledTime": "2026-01-20T10:00:00Z",
    "isDismissed": false,
    "dismissedAt": null,
    "createdAt": "2026-01-15T10:00:00Z",
    "lastModifiedAt": "2026-01-15T10:00:00Z"
  }
]
```

---

### GET /api/v1/reminders/{id}

Retrieves a single reminder by ID.

**Authentication:** JWT Bearer token required

**Response:** `200 OK` - Returns reminder object

**Status Codes:**
| Code | Error Code | Description |
|------|------------|-------------|
| 200 | - | Success |
| 404 | `REMINDER_NOT_FOUND` | Reminder not found |

---

### PUT /api/v1/reminders/{id}

Snoozes (reschedules) a reminder to a new time.

**Authentication:** JWT Bearer token required

**Request Body:**
```json
{
  "scheduledTime": "2026-01-21T10:00:00Z"
}
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `scheduledTime` | DateTime | Yes | Must be UTC and in the future |

**Response:** `200 OK` - Returns updated reminder

**Status Codes:**
| Code | Error Code | Description |
|------|------------|-------------|
| 200 | - | Success |
| 400 | `REMINDER_ALREADY_DISMISSED` | Cannot snooze dismissed reminder |
| 400 | `INVALID_SCHEDULED_TIME` | Time must be in the future |
| 404 | `REMINDER_NOT_FOUND` | Reminder not found |

---

### PUT /api/v1/reminders/{id}/dismiss

Dismisses a reminder without affecting the associated task.

**Authentication:** JWT Bearer token required

**Response:** `204 No Content`

**Status Codes:**
| Code | Error Code | Description |
|------|------------|-------------|
| 204 | - | Successfully dismissed |
| 400 | `REMINDER_ALREADY_DISMISSED` | Already dismissed |
| 404 | `REMINDER_NOT_FOUND` | Reminder not found |

---

## Unity Operations

The "Unity" pattern ensures Task and Reminder are updated atomically using CosmosDB transactional batches:

| Operation | Endpoint | Task Change | Reminder Change |
|-----------|----------|-------------|-----------------|
| Complete | `PUT /tasks/{id}/complete` | state → Completed, completedAt set | isDismissed → true, dismissedAt set |
| Delete | `DELETE /tasks/{id}` | state → Deleted | isDismissed → true, dismissedAt set |

This prevents orphaned reminders and ensures data consistency.

---

## Data Types

### TaskDto

| Field | Type | Description |
|-------|------|-------------|
| `id` | string | Unique identifier (GUID) |
| `name` | string | Task name/title |
| `state` | string | OnDeck, InProgress, Completed, Deleted |
| `createdAt` | DateTime | UTC creation timestamp |
| `completedAt` | DateTime? | UTC completion timestamp (null if not completed) |
| `reminderId` | string? | Associated reminder ID (null if none) |
| `lastModifiedAt` | DateTime | UTC last modification timestamp |

### TaskReminderDto

| Field | Type | Description |
|-------|------|-------------|
| `id` | string | Unique identifier (GUID) |
| `taskId` | string | Associated task ID |
| `taskName` | string | Denormalized task name |
| `scheduledTime` | DateTime | UTC time when reminder triggers |
| `isDismissed` | bool | Whether reminder is dismissed |
| `dismissedAt` | DateTime? | UTC dismissal timestamp |
| `createdAt` | DateTime | UTC creation timestamp |
| `lastModifiedAt` | DateTime | UTC last modification timestamp |

### PagedTasksDto

| Field | Type | Description |
|-------|------|-------------|
| `items` | TaskDto[] | Tasks in current page |
| `totalCount` | int | Total matching tasks across all pages |
| `hasMore` | bool | Whether more items exist |

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
_Last Updated: 2026-01-15_
