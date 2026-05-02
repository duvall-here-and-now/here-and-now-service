# Here and Now Service - Documentation Index

**Type:** Monolith Backend API
**Primary Language:** C# (.NET 8.0)
**Architecture:** Clean Architecture (3-Layer) + Command Pattern + Unity Pattern + Computed Instance Model
**Last Updated:** 2026-05-01

---

## Project Overview

Here and Now Service is an ASP.NET Core 8.0 REST API for personal task management with reminders and recurring tasks. The service provides task, reminder, and recurring task management through a **Command Pattern API** with Auth0 JWT authentication and Azure Cosmos DB persistence.

**v2.0 features:** Recurring Tasks — RRULE-based recurrence patterns (RFC 5545/iCal) with computed instances, state overrides, and one-active-at-a-time constraint. 7 new commands, 2 new document types, and a computed instance model.

---

## Quick Reference

- **Tech Stack:** ASP.NET Core 8.0, Azure Cosmos DB 3.46.1, Auth0 JWT, Ical.Net 5.2.0, Swagger
- **Entry Point:** `Web/HereAndNow.Web/Program.cs`
- **Architecture Pattern:** Clean Architecture + Command Pattern + Unity Pattern
- **Data Storage:** Azure Cosmos DB (NoSQL with `/userId` partition key, 4 document types)
- **Deployment:** Azure Web Apps via GitHub Actions

---

## API Overview

| Controller | Endpoints | Purpose |
|------------|-----------|---------|
| **Commands** | 1 (13 commands) | All mutations — tasks, reminders, recurring tasks |
| Tasks | 4 | Task queries + legacy complete |
| Reminders | 4 | Reminder queries + legacy endpoints |
| Recurring Configs | 2 | Query recurring task configurations |
| Recurring Tasks | 1 | Query computed recurring task instances |
| Messages | 3 | Public/protected/admin demo endpoints |

### Commands (13)

| Command | Returns | Purpose |
|---------|---------|---------|
| `CreateTask` | 201 TaskDto | Create task with client-generated ID |
| `CreateTaskAndTaskReminder` | 201 TaskAndReminderDto | Atomic task + reminder creation |
| `UpdateTaskName` | 200 TaskDto | Rename task (Unity reminder sync) |
| `UpdateTaskState` | 200 TaskDto | State transition (Unity on Completed/Deleted) |
| `UpdateTaskReminderScheduledTime` | 200 TaskReminderDto | Snooze/reschedule reminder |
| `DismissTaskReminder` | 204 | Dismiss reminder (idempotent) |
| `CreateRecurringTaskConfig` | 201 RecurringTaskConfigDto | Create RRULE config |
| `UpdateRecurringTaskConfig` | 200 RecurringTaskConfigDto | Update RRULE config |
| `DeleteRecurringTaskConfig` | 204 | Delete config + all overrides |
| `StartRecurringTask` | 200 | OnDeck → InProgress |
| `RevertRecurringTaskToOnDeck` | 200 | InProgress → OnDeck |
| `CompleteRecurringTask` | 200 | → Completed |
| `SkipRecurringTask` | 200 | → Skipped |

---

## Generated Documentation

- [Project Overview](./project-overview.md) — Executive summary, tech stack, API surface
- [Architecture](./architecture.md) — Assembly layout, Command Pattern, Unity Pattern, Cosmos DB design, middleware pipeline, DI, testing
- [Source Tree Analysis](./source-tree-analysis.md) — Annotated directory tree with assembly dependency graph
- [API Contracts](./api-contracts.md) — All endpoints, request/response shapes, error codes, DTO reference
- [Data Models](./data-models.md) — Cosmos documents, computed model, state machines, exceptions, service interfaces
- [Development Guide](./development-guide.md) — Prerequisites, setup, build/test commands, adding commands, coding conventions, curl examples
- [Deployment Guide](./deployment-guide.md) — CI/CD pipeline, Azure config, Cosmos setup, security, troubleshooting

---

## Deep Dives

- [Compute Instances Algorithm](./compute-instances-algorithm.md) — **★ Read before any recurring task work** — full documented state resolution pipeline, activeCandidate sentinel, invariants

---

## AI-Assisted Development

When creating a brownfield PRD or implementing features in this service, provide this index as context.

**Critical rules for implementation agents:** Read `_bmad-output/project-context.md` before implementing any service changes.

**Key rules to remember:**
- All mutations use `POST /api/v1/commands` — never add new REST mutation endpoints
- Client generates IDs — normalize to lowercase GUID before persisting
- `userId` is always the first service parameter from `ClaimTypes.NameIdentifier`
- Use `TaskState` string constants — never raw string literals
- All timestamps are UTC (`DateTime.UtcNow` only)
- Task + reminder updates must be atomic (Unity pattern)
- Never persist recurring task instances — only configs and state overrides
- RRULE stored without `RRULE:` prefix
- Reject `Secondly` and `Minutely` RRULE frequencies
