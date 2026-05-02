# Here and Now Service - Source Tree Analysis

**Date:** 2026-05-01
**Scan Level:** Exhaustive

---

## Annotated Directory Tree

```
here-and-now-service/               # Solution root
│
├── HereAndNow.sln                  # Solution file (5 projects)
├── .env                            # Local env vars (not committed)
├── .gitignore
├── README.md                       # Auth0 sample origin story
├── CLAUDE.md                       # AI assistant instructions
│
├── Message/                        # Auth0 demo assembly
│   └── HereAndNow.Message/
│       ├── HereAndNow.Message.csproj  # No external deps beyond logging
│       ├── Models/
│       │   └── Message.cs          # Simple model: Text, Type
│       └── Services/
│           ├── IMessageService.cs  # GetPublic/Protected/AdminMessage
│           └── MessageService.cs   # Returns static hardcoded messages
│
├── Task/                           # Core business logic assembly
│   ├── HereAndNow.Task/
│   │   ├── HereAndNow.Task.csproj  # Deps: Cosmos 3.46.1, Ical.Net 5.2.0, Newtonsoft.Json 13.0.3
│   │   │
│   │   ├── Models/
│   │   │   ├── TaskDocument.cs                     # Cosmos doc: id, type, userId, name, state, timestamps, reminderId?
│   │   │   ├── TaskReminderDocument.cs              # Cosmos doc: denormalized taskName, scheduledTime, isDismissed
│   │   │   ├── RecurringTaskConfigDocument.cs       # ★ Cosmos doc: text, rrule, startDateAndTime
│   │   │   ├── RecurringTaskStateOverrideDocument.cs # ★ Cosmos doc: compositeId={configId}_{timestamp}
│   │   │   ├── RecurringTaskInstance.cs             # ★ Computed (NOT persisted): derived from config+overrides
│   │   │   ├── TaskState.cs                        # String constants: OnDeck, InProgress, Completed, Deleted, Scheduled, Skipped
│   │   │   └── PagedResult.cs                      # Generic wrapper: Items, TotalCount, HasMore
│   │   │
│   │   ├── Exceptions/             # 12 domain exceptions
│   │   │   ├── TaskNotFoundException.cs
│   │   │   ├── TaskAlreadyExistsException.cs
│   │   │   ├── ReminderNotFoundException.cs
│   │   │   ├── ReminderAlreadyExistsException.cs
│   │   │   ├── ReminderAlreadyDismissedException.cs
│   │   │   ├── InvalidScheduledTimeException.cs
│   │   │   ├── UnityTransactionFailedException.cs
│   │   │   ├── InvalidRecurrenceRuleException.cs        # ★ RRULE validation
│   │   │   ├── InvalidStateTransitionException.cs       # ★ State machine guard
│   │   │   ├── RecurringTaskConfigAlreadyExistsException.cs  # ★
│   │   │   ├── RecurringTaskConfigNotFoundException.cs        # ★
│   │   │   └── TaskReminderAlreadyExistsException.cs
│   │   │
│   │   ├── Repositories/
│   │   │   ├── CosmosDbSettings.cs          # POCO: ConnectionString, DatabaseName, ContainerName
│   │   │   ├── ITaskRepository.cs           # CRUD + paginated query + soft-delete + Unity ops
│   │   │   ├── TaskRepository.cs            # Cosmos impl; /userId partition key
│   │   │   ├── ITaskReminderRepository.cs   # CRUD + list non-dismissed
│   │   │   ├── TaskReminderRepository.cs    # Cosmos impl
│   │   │   ├── IRecurringTaskRepository.cs  # ★ Config CRUD + bulk override ops
│   │   │   └── RecurringTaskRepository.cs   # ★ Cosmos impl; chunked delete for overrides
│   │   │
│   │   └── Services/
│   │       ├── ITaskService.cs              # CreateTaskWithIdAsync, GetTasksPagedAsync, UpdateStateAsync, Unity ops
│   │       ├── TaskService.cs               # GUID normalization, state validation, Unity dispatch
│   │       ├── ITaskReminderService.cs      # Create/Snooze/Dismiss/Get reminder
│   │       ├── TaskReminderService.cs       # Validates future time on snooze, sets timestamps
│   │       ├── IRecurringTaskService.cs     # ★ Config CRUD + ComputeInstances + state commands
│   │       └── RecurringTaskService.cs      # ★ RRULE validation, 4-phase computation pipeline
│   │
│   └── HereAndNow.Task.Tests/
│       ├── Models/
│       │   └── RecurringTaskModelsTests.cs   # Override ID format, TaskState constants
│       └── Services/
│           ├── TaskServiceTests.cs           # 1660 lines: full task lifecycle
│           ├── TaskReminderServiceTests.cs   # Snooze, dismiss, timestamps
│           └── RecurringTaskServiceTests.cs  # 723 lines: RRULE computation, state resolution
│
├── Web/                            # ASP.NET Core Web API
│   ├── HereAndNow.Web/
│   │   ├── HereAndNow.Web.csproj   # Deps: dotenv.net 3.2.1, JwtBearer 8.0.11, Swashbuckle 6.9.0
│   │   ├── Program.cs              # ★ Entry point: DI, middleware, Cosmos init
│   │   ├── SWAGGER_SETUP.md        # Azure Swagger IP restriction guide
│   │   │
│   │   ├── Commands/               # ★ All mutation payloads (one file per command)
│   │   │   ├── CommandRequest.cs   # { command: string, payload: JsonElement }
│   │   │   ├── CommandResponse.cs  # { success: bool, message?: string }
│   │   │   ├── CreateTaskCommand.cs
│   │   │   ├── CreateTaskAndTaskReminderCommand.cs
│   │   │   ├── UpdateTaskNameCommand.cs
│   │   │   ├── UpdateTaskStateCommand.cs
│   │   │   ├── DismissTaskReminderCommand.cs
│   │   │   ├── UpdateTaskReminderScheduledTimeCommand.cs
│   │   │   ├── CreateRecurringTaskConfigCommand.cs
│   │   │   ├── UpdateRecurringTaskConfigCommand.cs
│   │   │   ├── DeleteRecurringTaskConfigCommand.cs
│   │   │   ├── CompleteRecurringTaskCommand.cs
│   │   │   ├── RevertRecurringTaskToOnDeckCommand.cs
│   │   │   ├── SkipRecurringTaskCommand.cs
│   │   │   └── StartRecurringTaskCommand.cs
│   │   │
│   │   ├── Controllers/
│   │   │   ├── CommandsController.cs           # ★ POST /api/v1/commands — 13-command switch dispatch
│   │   │   ├── TasksController.cs              # GET /api/v1/tasks[/{id}], PUT /{id}/complete
│   │   │   ├── RemindersController.cs          # GET+POST /api/v1/reminders, GET/{id}, PUT/{id}/dismiss
│   │   │   ├── RecurringTaskConfigsController.cs  # ★ GET /api/v1/recurring-task-configs[/{id}]
│   │   │   ├── RecurringTasksController.cs     # ★ GET /api/v1/recurring-tasks?from=&to=
│   │   │   ├── MessagesController.cs           # GET /api/messages/public|protected|admin
│   │   │   └── ErrorController.cs              # /error ExceptionHandler route
│   │   │
│   │   ├── DTOs/
│   │   │   ├── TaskDto.cs                  # id, name, state, createdAt, completedAt?, reminderId?, lastModifiedAt
│   │   │   ├── TaskReminderDto.cs          # id, taskId, taskName, scheduledTime, isDismissed, timestamps
│   │   │   ├── CreateTaskDto.cs            # Legacy request: name, scheduledTime?
│   │   │   ├── CreateReminderDto.cs        # Legacy request: taskId, scheduledTime
│   │   │   ├── PagedTasksDto.cs            # items, totalCount, hasMore
│   │   │   ├── ErrorResponseDto.cs         # { error: { code, message } }
│   │   │   ├── RecurringTaskConfigDto.cs   # id, text, recurrenceRule, startDateAndTime, createdAt
│   │   │   ├── RecurringTaskDto.cs         # id, configId, text, recurrenceDateAndTime, state, recurrenceRule
│   │   │   └── TaskAndReminderDto.cs       # { task: TaskDto, reminder: TaskReminderDto }
│   │   │
│   │   ├── Mappers/                # Static classes (no AutoMapper)
│   │   │   ├── TaskMapper.cs
│   │   │   ├── ReminderMapper.cs
│   │   │   ├── RecurringTaskConfigMapper.cs
│   │   │   └── RecurringTaskMapper.cs
│   │   │
│   │   ├── Middlewares/
│   │   │   ├── ErrorHandlerMiddleware.cs   # Domain exceptions → HTTP status + ErrorResponseDto
│   │   │   └── SecureHeadersMiddleware.cs  # X-Content-Type-Options, X-Frame-Options, etc.
│   │   │
│   │   └── Validation/
│   │       └── FutureTimeValidationAttribute.cs  # [FutureTime] for legacy ScheduledTime fields
│   │
│   └── HereAndNow.Web.Tests/
│       ├── Controllers/            # Unit tests per controller
│       │   ├── CommandsControllerTests.cs
│       │   ├── RecurringTaskConfigsControllerTests.cs
│       │   ├── RecurringTasksControllerTests.cs
│       │   ├── RecurringTaskStateCommandTests.cs
│       │   ├── RemindersControllerTests.cs
│       │   └── TasksControllerTests.cs
│       ├── Helpers/
│       │   ├── TestAuthHandler.cs           # Auth0 JWT simulation; X-Test-UserId header support
│       │   └── TestWebApplicationFactory.cs # In-memory host with mocked Cosmos services
│       ├── Integration/            # Full HTTP stack tests (no real Cosmos)
│       │   ├── CommandsApiTests.cs          # 1217 lines: all 13 commands end-to-end
│       │   ├── TasksApiTests.cs
│       │   ├── RemindersApiTests.cs
│       │   ├── AuthorizationTests.cs
│       │   └── CorsTests.cs
│       └── Services/
│           ├── RecurringTaskServiceTests.cs
│           └── RecurringTaskStateCommandServiceTests.cs
│
├── docs/                           # Project documentation
├── .github/
│   ├── workflows/main_here-and-now-service.yml  # CI/CD → Azure Web Apps
│   ├── agents/                     # BMAD AI agent definitions
│   └── skills/                     # BMAD skill definitions
├── _bmad/                          # BMAD method config
├── _bmad-output/project-context.md # ★ Critical AI agent rules
└── Reminders/                      # ⚠ Stale scaffold (not in active solution)
```

---

## Assembly Dependency Graph

```
HereAndNow.Web ──────────────► HereAndNow.Task
     │                         HereAndNow.Message
     │
HereAndNow.Web.Tests ────────► HereAndNow.Web
     │                    ──► HereAndNow.Task
     │                    ──► HereAndNow.Message
     │
HereAndNow.Task.Tests ───────► HereAndNow.Task
```

---

## Critical Folders for AI Agents

| Folder | Why It Matters |
|--------|----------------|
| `Task/HereAndNow.Task/Services/` | Business logic — read before writing any service |
| `Task/HereAndNow.Task/Models/` | Domain models and TaskState constants |
| `Web/HereAndNow.Web/Controllers/CommandsController.cs` | All mutations dispatch here |
| `Web/HereAndNow.Web/Commands/` | One file per command — add new mutations here |
| `Web/HereAndNow.Web/Program.cs` | DI registration and middleware pipeline |
| `docs/compute-instances-algorithm.md` | Mandatory before any recurring task work |
| `_bmad-output/project-context.md` | Rules an AI agent is likely to miss |
