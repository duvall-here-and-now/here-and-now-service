# Here and Now Service - Source Tree Analysis

**Date:** 2026-03-19
**Scan Level:** Exhaustive

## Overview

This document provides an annotated directory tree of the Here and Now Service codebase, highlighting critical directories, entry points, and integration points.

## Project Root Structure

```
here-and-now-service/
├── HereAndNow.sln                    # Solution file (5 active projects)
├── .env                               # Environment variables (not committed)
├── CLAUDE.md                          # AI assistant context
├── README.md                          # Project readme
│
├── Message/                           # Demo business logic assembly
│   └── HereAndNow.Message/
│       ├── HereAndNow.Message.csproj
│       ├── Models/
│       │   └── Message.cs             # Simple message model
│       └── Services/
│           ├── IMessageService.cs     # Interface
│           └── MessageService.cs      # Static message responses
│
├── Task/                              # ★ Core business logic assembly
│   ├── HereAndNow.Task/
│   │   ├── HereAndNow.Task.csproj     # Cosmos DB 3.46.1, Ical.Net 5.2.0, Newtonsoft.Json
│   │   ├── Models/
│   │   │   ├── TaskDocument.cs                       # Task entity (Cosmos DB)
│   │   │   ├── TaskReminderDocument.cs               # Reminder entity (Cosmos DB)
│   │   │   ├── RecurringTaskConfigDocument.cs        # ★ NEW Recurrence config (Cosmos DB)
│   │   │   ├── RecurringTaskStateOverrideDocument.cs  # ★ NEW State override (Cosmos DB)
│   │   │   ├── RecurringTaskInstance.cs              # ★ NEW Computed model (not persisted)
│   │   │   ├── TaskState.cs                          # State constants (OnDeck, InProgress, Completed, Deleted, Scheduled, Skipped)
│   │   │   ├── PagedResult.cs                        # Generic pagination wrapper
│   │   │   └── Exceptions/                           # 12 domain exceptions
│   │   │       ├── TaskNotFoundException.cs
│   │   │       ├── TaskAlreadyExistsException.cs
│   │   │       ├── ReminderNotFoundException.cs
│   │   │       ├── ReminderAlreadyExistsException.cs
│   │   │       ├── ReminderAlreadyDismissedException.cs
│   │   │       ├── InvalidScheduledTimeException.cs
│   │   │       ├── InvalidStateTransitionException.cs
│   │   │       ├── UnityTransactionFailedException.cs
│   │   │       ├── RecurringTaskConfigNotFoundException.cs      # ★ NEW
│   │   │       ├── RecurringTaskConfigAlreadyExistsException.cs # ★ NEW
│   │   │       ├── InvalidRecurrenceRuleException.cs            # ★ NEW
│   │   │       └── TaskReminderAlreadyExistsException.cs
│   │   ├── Repositories/
│   │   │   ├── CosmosDbSettings.cs                   # DB connection config
│   │   │   ├── ITaskRepository.cs                    # Task CRUD + Unity batches
│   │   │   ├── TaskRepository.cs                     # Cosmos DB implementation
│   │   │   ├── ITaskReminderRepository.cs            # Reminder CRUD + atomic link
│   │   │   ├── TaskReminderRepository.cs             # Cosmos DB implementation
│   │   │   ├── IRecurringTaskRepository.cs           # ★ NEW Config + Override CRUD
│   │   │   └── RecurringTaskRepository.cs            # ★ NEW Cosmos DB impl (batch delete, upsert)
│   │   └── Services/
│   │       ├── ITaskService.cs                       # Task operations + Unity
│   │       ├── TaskService.cs                        # Business logic (state machine, reminder sync)
│   │       ├── ITaskReminderService.cs               # Reminder operations
│   │       ├── TaskReminderService.cs                # Snooze, dismiss, create
│   │       ├── IRecurringTaskService.cs              # ★ NEW Computation + CRUD + state commands
│   │       └── RecurringTaskService.cs               # ★ NEW RRULE engine, one-active-at-a-time
│   │
│   └── HereAndNow.Task.Tests/
│       ├── HereAndNow.Task.Tests.csproj              # xUnit, Moq, FluentAssertions
│       └── Services/                                 # Unit tests for services
│
├── Web/                               # API layer assembly
│   ├── HereAndNow.Web/
│   │   ├── HereAndNow.Web.csproj      # JWT Bearer 8.0.11, Swashbuckle 6.9.0, dotenv.net
│   │   ├── .env                       # Web-specific env vars
│   │   ├── Program.cs                 # ★ Entry point — DI, auth, middleware, Cosmos init
│   │   ├── Controllers/
│   │   │   ├── CommandsController.cs  # ★ POST /api/v1/commands (13 command types)
│   │   │   ├── TasksController.cs     # GET queries + legacy complete
│   │   │   ├── RemindersController.cs # GET queries + legacy dismiss/create
│   │   │   ├── MessagesController.cs  # Demo public/protected/admin
│   │   │   └── ErrorController.cs     # Development error page
│   │   ├── Commands/                  # ★ Command request/response models
│   │   │   ├── CommandRequest.cs      # Base: { command, payload }
│   │   │   ├── CommandResponse.cs     # Base: { success, message }
│   │   │   ├── CreateTaskCommand.cs
│   │   │   ├── CreateTaskAndTaskReminderCommand.cs
│   │   │   ├── UpdateTaskNameCommand.cs
│   │   │   ├── UpdateTaskStateCommand.cs
│   │   │   ├── UpdateTaskReminderScheduledTimeCommand.cs
│   │   │   ├── DismissTaskReminderCommand.cs
│   │   │   ├── CreateRecurringTaskConfigCommand.cs    # ★ NEW
│   │   │   ├── UpdateRecurringTaskConfigCommand.cs    # ★ NEW
│   │   │   ├── DeleteRecurringTaskConfigCommand.cs    # ★ NEW
│   │   │   ├── StartRecurringTaskCommand.cs           # ★ NEW
│   │   │   ├── RevertRecurringTaskToOnDeckCommand.cs  # ★ NEW
│   │   │   ├── CompleteRecurringTaskCommand.cs        # ★ NEW
│   │   │   └── SkipRecurringTaskCommand.cs            # ★ NEW
│   │   ├── DTOs/
│   │   │   ├── TaskDto.cs
│   │   │   ├── TaskReminderDto.cs
│   │   │   ├── TaskAndReminderDto.cs
│   │   │   ├── PagedTasksDto.cs
│   │   │   ├── CreateTaskDto.cs
│   │   │   ├── CreateReminderDto.cs
│   │   │   ├── RecurringTaskConfigDto.cs              # ★ NEW
│   │   │   └── ErrorResponseDto.cs
│   │   ├── Mappers/
│   │   │   ├── TaskMapper.cs
│   │   │   ├── ReminderMapper.cs
│   │   │   └── RecurringTaskConfigMapper.cs           # ★ NEW
│   │   ├── Middlewares/
│   │   │   ├── ErrorHandlerMiddleware.cs              # Exception → HTTP status mapping
│   │   │   └── SecureHeadersMiddleware.cs             # Security headers (CSP, HSTS, etc.)
│   │   └── Validation/
│   │       └── FutureTimeValidationAttribute.cs       # Custom [FutureTime] attribute
│   │
│   └── HereAndNow.Web.Tests/
│       ├── HereAndNow.Web.Tests.csproj                # Integration + unit tests
│       └── Controllers/                               # Controller tests
│
├── Reminders/                         # ⚠ STALE — abandoned scaffold from Dec 2025
│   └── HereAndNow.Reminders/
│       └── obj/                       # Only build artifacts, no source files
│
├── docs/                              # Project documentation (you are here)
│
├── _bmad/                             # BMAD Method configuration
├── _bmad-output/                      # BMAD output artifacts
└── .github/
    ├── workflows/
    │   └── main_here-and-now-service.yml  # CI/CD: Build → Test → Deploy to Azure
    ├── agents/                        # AI agent configs
    └── skills/                        # BMAD skills
```

## Critical Directories

| Directory | Purpose | Key Files |
|-----------|---------|-----------|
| `Task/HereAndNow.Task/Models/` | Domain entities stored in Cosmos DB | TaskDocument, TaskReminderDocument, RecurringTaskConfigDocument, RecurringTaskStateOverrideDocument |
| `Task/HereAndNow.Task/Services/` | Core business logic | TaskService, TaskReminderService, RecurringTaskService (RRULE engine) |
| `Task/HereAndNow.Task/Repositories/` | Data access layer | Cosmos DB implementations with Unity (transactional batch) |
| `Web/HereAndNow.Web/Controllers/` | API surface | CommandsController (13 commands), TasksController, RemindersController |
| `Web/HereAndNow.Web/Commands/` | Command definitions | 13 command request/response models |
| `Web/HereAndNow.Web/DTOs/` | API response shapes | TaskDto, RecurringTaskConfigDto, ErrorResponseDto |
| `Web/HereAndNow.Web/Middlewares/` | Cross-cutting concerns | Error handling, security headers |

## Assembly Dependency Graph

```
HereAndNow.Web ──────► HereAndNow.Task ──────► Microsoft.Azure.Cosmos 3.46.1
    │                       │                      Ical.Net 5.2.0
    │                       │                      Newtonsoft.Json 13.0.3
    ├──► HereAndNow.Message                        Logging.Abstractions 8.0.0
    │
    ├──► Microsoft.AspNetCore.Authentication.JwtBearer 8.0.11
    ├──► Swashbuckle.AspNetCore 6.9.0
    └──► dotenv.net 3.2.1

HereAndNow.Web.Tests ──► HereAndNow.Web + Task + Message
    ├──► xUnit 2.9.2
    ├──► Moq 4.20.72
    ├──► FluentAssertions 6.12.0
    ├──► Microsoft.AspNetCore.Mvc.Testing 8.0.11
    └──► coverlet.collector 6.0.2

HereAndNow.Task.Tests ──► HereAndNow.Task
    ├──► xUnit 2.9.2, Moq 4.20.72, FluentAssertions 6.12.0
    └──► coverlet.collector 6.0.2
```

## Changes Since Last Scan (Jan 2026)

Files marked with ★ NEW are additions since the previous documentation scan. The major addition is the **RecurringTask** feature set:
- 3 new Cosmos DB document models
- 1 new computed model (RecurringTaskInstance)
- 3 new exception types
- 1 new repository (IRecurringTaskRepository/RecurringTaskRepository)
- 1 new service (IRecurringTaskService/RecurringTaskService)
- 7 new command types
- 1 new DTO and mapper

---

_Generated by BMAD document-project workflow | Exhaustive Scan | 2026-03-19_
