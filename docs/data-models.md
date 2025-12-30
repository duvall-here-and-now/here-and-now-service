# Here and Now Service - Data Models

**Date:** 2025-12-30

## Overview

This document describes the domain models and data structures used in the Here and Now Service. The project follows a clean architecture pattern with separation between the Message layer (business logic) and Web layer (API concerns).

## Domain Models

Located in: `Message/HereAndNow.Message/Models/`

### Message

**File:** `Message.cs`
**Namespace:** `HereAndNowService.Models`
**Purpose:** Simple message response model for the API.

```csharp
public class Message
{
    /// <summary>
    /// The text content of the message
    /// </summary>
    public string? text { get; set; }
}
```

**Field Descriptions:**

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| text | string | Yes | The message content |

**JSON Representation:**
```json
{
  "text": "This is a message."
}
```

**Notes:**
- Uses lowercase `text` property name (matches JSON convention)
- Used for demo endpoints showing public/protected/admin access levels

---

## Service Layer

Located in: `Message/HereAndNow.Message/Services/`

### IMessageService (Interface)

**File:** `IMessageService.cs`
**Namespace:** `HereAndNowService.Services`
**Purpose:** Defines the contract for message retrieval operations.

```csharp
public interface IMessageService
{
    Message GetPublicMessage();
    Message GetProtectedMessage();
    Message GetAdminMessage();
}
```

| Method | Returns | Description |
|--------|---------|-------------|
| `GetPublicMessage()` | `Message` | Returns the public message (no auth required) |
| `GetProtectedMessage()` | `Message` | Returns the protected message (auth required) |
| `GetAdminMessage()` | `Message` | Returns the admin message (auth required) |

---

### MessageService (Implementation)

**File:** `MessageService.cs`
**Namespace:** `HereAndNowService.Services`
**Purpose:** Default implementation returning static messages.

```csharp
public class MessageService : IMessageService
{
    public Message GetPublicMessage()
        => new Message { text = "This is a public message." };

    public Message GetProtectedMessage()
        => new Message { text = "This is a protected message, and Mike is cool." };

    public Message GetAdminMessage()
        => new Message { text = "This is an admin message." };
}
```

**Dependency Injection Registration:**
```csharp
// In Program.cs
builder.Services.AddScoped<IMessageService, MessageService>();
```

---

## Data Storage

### Current Implementation

**Storage Type:** In-memory (static messages)

The service returns hardcoded messages directly from `MessageService`. There is no database or persistent storage - this is a demo/sample API for demonstrating Auth0 authentication.

---

## DTOs and Mappers

**Current State:** Not used

The API returns domain models directly. The project structure includes empty folders prepared for future expansion:

- `Web/HereAndNow.Web/DTOs/` - For Data Transfer Objects
- `Web/HereAndNow.Web/Mappers/` - For domain-to-DTO mapping

For a production API with more complex data, consider adding:
1. **DTOs** to control what's exposed in the API
2. **Mappers** to convert between domain models and DTOs
3. **Validators** (e.g., FluentValidation) for request validation

---

## JSON Serialization

The API uses `System.Text.Json` with the following configuration:

```csharp
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
```

**Effect:** Enums are serialized as strings rather than integers.

---

## Extending the Data Model

To add new data types:

1. **Add domain model** in `Message/HereAndNow.Message/Models/`
2. **Add service interface** in `Message/HereAndNow.Message/Services/`
3. **Add service implementation** in `Message/HereAndNow.Message/Services/`
4. **Register in DI** in `Web/HereAndNow.Web/Program.cs`
5. **Create controller** in `Web/HereAndNow.Web/Controllers/`

For persistent storage, consider adding:
- Database context (e.g., Entity Framework Core, Cosmos DB SDK)
- Repository pattern for data access abstraction
- DTOs for API response shaping
- Mappers for domain-to-DTO conversion

---

## Architecture Diagram

```
┌─────────────────────────────────────────────┐
│               Web Layer                      │
│  ┌─────────────────────────────────────┐   │
│  │      MessagesController             │   │
│  │   (depends on IMessageService)      │   │
│  └──────────────┬──────────────────────┘   │
└─────────────────┼───────────────────────────┘
                  │
                  │ Dependency Injection
                  ▼
┌─────────────────────────────────────────────┐
│             Message Layer                    │
│  ┌─────────────────────────────────────┐   │
│  │        IMessageService              │   │
│  │   (interface - abstraction)         │   │
│  └──────────────┬──────────────────────┘   │
│                 │                           │
│                 ▼                           │
│  ┌─────────────────────────────────────┐   │
│  │         MessageService              │   │
│  │   (implementation - static data)    │   │
│  └─────────────────────────────────────┘   │
│                                             │
│  ┌─────────────────────────────────────┐   │
│  │            Message                  │   │
│  │        (domain model)               │   │
│  └─────────────────────────────────────┘   │
└─────────────────────────────────────────────┘
```

---

_Generated using BMAD Method `document-project` workflow_
_Last Updated: 2025-12-30_
