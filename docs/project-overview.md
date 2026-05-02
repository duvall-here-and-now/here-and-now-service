# Here and Now Service - Project Overview

**Date:** 2026-05-01
**Type:** Monolith Backend API
**Architecture:** Clean Architecture (3-Layer) + Command Pattern + Unity Pattern + Computed Instance Model

---

## Executive Summary

Here and Now Service is a task management REST API with reminders and recurring tasks. It is the backend for the HereAndNow product (web SPA + Android app). The service uses a **Command Pattern API** — all mutations flow through a single `POST /api/v1/commands` endpoint with explicit intent-based commands and client-generated IDs for optimistic UI.

The **Unity Pattern** atomically completes or deletes a task and dismisses its reminder in a single Cosmos DB transactional batch. The **Recurring Task** feature adds RRULE-based repeating tasks (RFC 5545/iCal) with computed instances and sparse state overrides.

---

## Technology Stack

| Category | Technology | Version |
|----------|------------|---------|
| Runtime | .NET | 8.0 |
| Language | C# | 12 |
| Framework | ASP.NET Core Web API | 8.0 |
| Database | Azure Cosmos DB | SDK 3.46.1 |
| Authentication | Auth0 JWT Bearer | 8.0.11 |
| Recurrence Engine | Ical.Net | 5.2.0 |
| API Docs | Swashbuckle/Swagger | 6.9.0 |
| JSON | Newtonsoft.Json | 13.0.3 |
| Config | dotenv.net | 3.2.1 |
| Test Framework | xUnit | 2.9.2 |
| Mocking | Moq | 4.20.72 |
| Assertions | FluentAssertions | 6.12.0 |
| Integration Testing | Microsoft.AspNetCore.Mvc.Testing | 8.0.11 |

---

## Architecture Type

**Monolith** — single repository, single deployable unit, 3 production assemblies:

| Assembly | Role |
|----------|------|
| `HereAndNow.Web` | ASP.NET Core API — controllers, commands, DTOs, middleware |
| `HereAndNow.Task` | Business logic — services, repositories, domain models |
| `HereAndNow.Message` | Auth0 demo — static messages only |

---

## API Overview

| Controller | Route Prefix | Endpoints |
|------------|-------------|-----------|
| **Commands** | `/api/v1/commands` | 1 (POST) — dispatches 13 commands |
| Tasks | `/api/v1/tasks` | GET list, GET by ID, POST (deprecated), PUT complete |
| Reminders | `/api/v1/reminders` | GET list, GET by ID, POST (deprecated), PUT dismiss |
| Recurring Configs | `/api/v1/recurring-task-configs` | GET list, GET by ID |
| Recurring Tasks | `/api/v1/recurring-tasks` | GET computed instances (date range) |
| Messages | `/api/messages` | GET public, protected, admin |

---

## Data Storage

- **Azure Cosmos DB** — single container `Tasks`, database `HereAndNow`
- Partition key: `/userId`
- 4 document types: `Task`, `TaskReminder`, `RecurringTaskConfig`, `RecurringTaskStateOverride`
- Recurring task instances are computed in-memory, not persisted

---

## Entry Point

`Web/HereAndNow.Web/Program.cs`

---

## Getting Started

1. Copy `.env.example` to `.env` and fill in Auth0 credentials
2. Set `COSMOS_CONNECTION_STRING` to connect to Cosmos (optional for message-only testing)
3. `dotnet run --project Web/HereAndNow.Web/HereAndNow.Web.csproj`
4. Open `http://localhost:6060/swagger`

See [development-guide.md](./development-guide.md) for full setup instructions.
