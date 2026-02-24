# Tasks: Notification Service

**Input**: Design documents from `/specs/001-notification-service/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/
**Generated**: 2025-12-05

**Tests**: Test tasks are included per constitution Principle III (Test-First Development). Tests MUST be authored immediately after plan approval, BEFORE implementation.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

Based on plan.md: Single API project structure
- **Source**: `src/Maliev.NotificationService.Api/`
- **Tests**: `tests/Maliev.NotificationService.Api.Tests/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [X] T001 Create solution file `Maliev.NotificationService.sln` at repository root
- [X] T002 Create API project structure at `src/Maliev.NotificationService.Api/` with directories: Controllers, Consumers, Services, Providers, Data, Models, Extensions, Middleware, Metrics
- [X] T003 Create test project at `tests/Maliev.NotificationService.Api.Tests/` with directories: Integration, Unit, TestHelpers
- [X] T004 [P] Add NuGet package references to API project: Maliev.Aspire.ServiceDefaults (from GitHub Packages), Npgsql.EntityFrameworkCore.PostgreSQL, MassTransit.RabbitMQ, Microsoft.AspNetCore.OpenApi, Scalar.AspNetCore, AspNetCore.HealthChecks.UI.Client
- [X] T005 [P] Add NuGet package references to Test project: xUnit, Moq, Testcontainers.PostgreSql, Testcontainers.RabbitMQ, Testcontainers.Redis, Microsoft.AspNetCore.Mvc.Testing
- [X] T006 [P] Create `nuget.config` at repository root with GitHub Packages source configuration for Maliev.Aspire.ServiceDefaults package (source: https://nuget.pkg.github.com/maliev/index.json, credentials: %NUGET_USERNAME% and %NUGET_PASSWORD% placeholders)
- [X] T007 [P] Create `.gitignore` at repository root with .NET patterns (bin/, obj/, etc.)
- [X] T008 [P] Create `.dockerignore` at repository root excluding build artifacts, IDE files, specs/, and test projects
- [X] T009 [P] Create `appsettings.json` at `src/Maliev.NotificationService.Api/` with ConnectionStrings placeholders
- [X] T010 [P] Create `appsettings.Development.json` at `src/Maliev.NotificationService.Api/` with localhost connection strings for PostgreSQL, Redis, RabbitMQ
- [X] T011 Create `Dockerfile` in `Maliev.NotificationService.Api/` following Docker Best Practices (multi-stage, built-in app user, BuildKit secrets, EXPOSE 8080)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T013 Create base entity class `BaseEntity` with Id, CreatedAt, UpdatedAt in `src/Maliev.NotificationService.Api/Data/Entities/BaseEntity.cs`
- [X] T014 Create `NotificationDbContext` in `src/Maliev.NotificationService.Api/Data/NotificationDbContext.cs` with DbSet placeholders for all 6 entities
- [X] T015 Create enum `ChannelType` in `src/Maliev.NotificationService.Api/Models/Enums/ChannelType.cs` with values: Email, Line, WhatsApp, Sms, Slack, Facebook, Instagram
- [X] T016 [P] Create enum `DeliveryStatus` in `src/Maliev.NotificationService.Api/Models/Enums/DeliveryStatus.cs` with values: Sent, Delivered, Failed, Pending, RateLimited
- [X] T017 [P] Create enum `UserType` in `src/Maliev.NotificationService.Api/Models/Enums/UserType.cs` with values: Customer, Staff, Administrator
- [X] T018 [P] Create enum `NotificationPriority` in `src/Maliev.NotificationService.Api/Models/Enums/NotificationPriority.cs` with values: Critical, Standard
- [X] T019 Create `ExceptionHandlingMiddleware` in `src/Maliev.NotificationService.Api/Middleware/ExceptionHandlingMiddleware.cs` with structured error responses
- [X] T020 Create `Program.cs` at `src/Maliev.NotificationService.Api/Program.cs` with: (1) builder.Configuration.AddGoogleSecretManager() for secret loading, (2) builder.AddServiceDefaults() immediately after, (3) PostgreSQL DbContext, (4) Redis connection, (5) MassTransit with RabbitMQ, (6) controllers, (7) Scalar OpenAPI at /scalar/v1, (8) health checks
- [X] T021 [P] Create `NotificationMetrics` class in `src/Maliev.NotificationService.Api/Metrics/NotificationMetrics.cs` with Counter, Histogram, and ObservableGauge for all required metrics
- [X] T022 Create `IChannelProvider` interface in `src/Maliev.NotificationService.Api/Providers/IChannelProvider.cs` with SendAsync, ValidateRecipientAsync, GetHealthAsync methods
- [X] T023 [P] Create `DeliveryResult` record in `src/Maliev.NotificationService.Api/Providers/DeliveryResult.cs` with Success, MessageId, FailureType, ErrorMessage, IsRetryable, RetryAfter properties
- [X] T024 [P] Create `ValidationResult` record in `src/Maliev.NotificationService.Api/Providers/ValidationResult.cs`
- [X] T025 Create `TestWebApplicationFactory` in `tests/Maliev.NotificationService.Api.Tests/Integration/TestWebApplicationFactory.cs` with dynamic RSA key generation, Testcontainers setup
- [X] T026 [P] Create `TestDatabaseFixture` in `tests/Maliev.NotificationService.Api.Tests/Integration/TestDatabaseFixture.cs` with PostgreSQL Testcontainer initialization
- [X] T027 [P] Create `MockHttpMessageHandler` in `tests/Maliev.NotificationService.Api.Tests/TestHelpers/MockHttpMessageHandler.cs` for mocking external provider HTTP calls

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Critical Business Notification Delivery (Priority: P1) 🎯 MVP

**Goal**: Enable delivery of critical business notifications (payment failures, order confirmations, system outages) through user-preferred channels with guaranteed delivery, retry logic, and fallback support.

**Independent Test**: Trigger a payment failure event via RabbitMQ and verify the affected user receives notification on their preferred channel with delivery confirmation logged within 30 seconds.

### Tests for User Story 1 ✅

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T028 [P] [US1] Create integration test `NotificationDeliveryTests.cs` at `tests/Maliev.NotificationService.Api.Tests/Integration/NotificationDeliveryTests.cs` for end-to-end critical notification delivery flow
- [X] T029 [P] [US1] Create integration test `RetryLogicTests.cs` at `tests/Maliev.NotificationService.Api.Tests/Integration/RetryLogicTests.cs` for 3 retry attempts with exponential backoff (1s, 2s, 4s)
- [X] T030 [P] [US1] Create integration test `FallbackChannelTests.cs` at `tests/Maliev.NotificationService.Api.Tests/Integration/FallbackChannelTests.cs` for primary channel failure → fallback activation
- [X] T031 [P] [US1] Create integration test `DeduplicationTests.cs` at `tests/Maliev.NotificationService.Api.Tests/Integration/DeduplicationTests.cs` for duplicate event detection via Redis cache
- [X] T032 [P] [US1] Create unit test `NotificationRouterTests.cs` at `tests/Maliev.NotificationService.Api.Tests/Unit/Services/NotificationRouterTests.cs` for routing logic
- [X] T033 [P] [US1] Create unit test `RetryServiceTests.cs` at `tests/Maliev.NotificationService.Api.Tests/Unit/Services/RetryServiceTests.cs` for retry scheduling
- [X] T034 [P] [US1] Create unit test `DeduplicationServiceTests.cs` at `tests/Maliev.NotificationService.Api.Tests/Unit/Services/DeduplicationServiceTests.cs` for hash-based deduplication

### Data Entities for User Story 1

- [X] T035 [P] [US1] Create `NotificationEvent` model in `src/Maliev.NotificationService.Api/Models/Events/NotificationEvent.cs` with CloudEvents properties (Id, Source, Type, Time, Data)
- [X] T036 [P] [US1] Create `DeliveryLog` entity in `src/Maliev.NotificationService.Api/Data/Entities/DeliveryLog.cs` with all properties from data-model.md (EventId, UserId, ChannelType, Status, ProviderResponse, etc.)
- [X] T037 [P] [US1] Create `RetryQueueEntry` entity in `src/Maliev.NotificationService.Api/Data/Entities/RetryQueueEntry.cs` with EventPayload, AttemptNumber, ScheduledTime
- [X] T038 [P] [US1] Create `DeadLetterRecord` entity in `src/Maliev.NotificationService.Api/Data/Entities/DeadLetterRecord.cs` with FailureReasons, TotalAttempts, EscalationStatus
- [X] T039 [US1] Update `NotificationDbContext` to include DbSets for DeliveryLog, RetryQueueEntry, DeadLetterRecord with entity configurations
- [X] T040 [US1] Create initial EF Core migration `001_CreateCoreEntities` for DeliveryLog, RetryQueueEntry, DeadLetterRecord tables

### Services for User Story 1

- [X] T041 [US1] Create `IDeduplicationService` interface and implementation `DeduplicationService` in `src/Maliev.NotificationService.Api/Services/` using Redis with event ID + timestamp hash, 24-hour TTL
- [X] T042 [US1] Create `INotificationRouter` interface and implementation `NotificationRouter` in `src/Maliev.NotificationService.Api/Services/` with preference resolution and channel provider selection logic
- [X] T043 [US1] Create `IRetryService` interface and implementation `RetryService` in `src/Maliev.NotificationService.Api/Services/` with MassTransit scheduled message delivery for retries
- [X] T044 [P] [US1] Create `EmailProvider` in `src/Maliev.NotificationService.Api/Providers/EmailProvider.cs` implementing IChannelProvider using MailKit or SendGrid SDK
- [X] T045 [US1] Implement `NotificationEventConsumer` in `src/Maliev.NotificationService.Api/Consumers/NotificationEventConsumer.cs` with MassTransit consumer logic: deduplication → routing → retry/DLQ handling. NOTE: For large event batches exceeding memory limits, implement batching with configurable page size (default 1000) using IAsyncEnumerable for event processing

### Integration & Configuration for User Story 1

- [X] T046 [US1] Configure MassTransit in Program.cs with two RabbitMQ queues: `notification-critical` (prefetch=10) and `notification-standard` (prefetch=50) bound to routing keys
- [X] T047 [US1] Register DeduplicationService, NotificationRouter, RetryService, EmailProvider in Program.cs DI container
- [X] T048 [US1] Add rate limiting configuration in Program.cs using TokenBucketRateLimiter for email provider
- [X] T049 [US1] Seed NotificationDbContext with default email channel type configuration

**Checkpoint**: At this point, User Story 1 should be fully functional - critical notifications can be delivered via email with retry, fallback, and deduplication

---

## Phase 4: User Story 2 - User Channel Preference Management (Priority: P2)

**Goal**: Enable users to manage notification preferences (primary/fallback channels, opt-out settings) via REST API, with automatic application to subsequent notifications.

**Independent Test**: Create/update user preferences via API endpoints and verify subsequent notification events respect the configured preferences.

### Tests for User Story 2 ✅

- [X] T050 [P] [US2] Create integration test `PreferencesApiTests.cs` at `tests/Maliev.NotificationService.Api.Tests/Integration/PreferencesApiTests.cs` for full CRUD operations on preferences endpoints
- [X] T051 [P] [US2] Create integration test for channel bindings API in `PreferencesApiTests.cs` covering create, update, delete, get operations
- [X] T052 [P] [US2] Create unit test `PreferenceExtensionsTests.cs` at `tests/Maliev.NotificationService.Api.Tests/Unit/Extensions/PreferenceExtensionsTests.cs` for ToResponse/ToEntity mapping methods

### Data Entities for User Story 2

- [X] T053 [P] [US2] Create `UserNotificationPreference` entity in `src/Maliev.NotificationService.Api/Data/Entities/UserNotificationPreference.cs` with UserId (PK), PrimaryChannelType, FallbackChannelTypes (JSONB), OptOutCategories (JSONB)
- [X] T054 [P] [US2] Create `ChannelBinding` entity in `src/Maliev.NotificationService.Api/Data/Entities/ChannelBinding.cs` with UserId, ChannelType, ChannelIdentifier (encrypted), IsValid, InvalidatedReason
- [X] T055 [US2] Update `NotificationDbContext` to include DbSets for UserNotificationPreference and ChannelBinding with unique index on (UserId, ChannelType)
- [X] T056 [US2] Create EF Core migration `002_AddPreferencesAndBindings` for UserNotificationPreference and ChannelBinding tables

### Request/Response Models for User Story 2

- [X] T057 [P] [US2] Create `CreatePreferenceRequest` in `src/Maliev.NotificationService.Api/Models/Requests/CreatePreferenceRequest.cs` with DataAnnotations validation
- [X] T058 [P] [US2] Create `UpdatePreferenceRequest` in `src/Maliev.NotificationService.Api/Models/Requests/UpdatePreferenceRequest.cs`
- [X] T059 [P] [US2] Create `PreferenceResponse` in `src/Maliev.NotificationService.Api/Models/Responses/PreferenceResponse.cs`
- [X] T060 [P] [US2] Create `CreateChannelBindingRequest` in `src/Maliev.NotificationService.Api/Models/Requests/CreateChannelBindingRequest.cs` with channel-specific validation
- [X] T061 [P] [US2] Create `UpdateChannelBindingRequest` in `src/Maliev.NotificationService.Api/Models/Requests/UpdateChannelBindingRequest.cs`
- [X] T062 [P] [US2] Create `ChannelBindingResponse` in `src/Maliev.NotificationService.Api/Models/Responses/ChannelBindingResponse.cs` with obfuscated identifier
- [X] T063 [P] [US2] Create `DeliveryLogResponse` in `src/Maliev.NotificationService.Api/Models/Responses/DeliveryLogResponse.cs`

### Extension Methods for User Story 2

- [X] T064 [P] [US2] Create `PreferenceExtensions.cs` in `src/Maliev.NotificationService.Api/Extensions/` with ToResponse and ToEntity extension methods for UserNotificationPreference
- [X] T065 [P] [US2] Create `ChannelBindingExtensions.cs` in `src/Maliev.NotificationService.Api/Extensions/` with ToResponse and ToEntity extension methods for ChannelBinding, including identifier obfuscation logic
- [X] T066 [P] [US2] Create `DeliveryLogExtensions.cs` in `src/Maliev.NotificationService.Api/Extensions/` with ToResponse extension method

### API Controllers for User Story 2

- [X] T067 [US2] Create `PreferencesController` in `src/Maliev.NotificationService.Api/Controllers/PreferencesController.cs` with endpoints: POST /api/v1/preferences, GET /api/v1/preferences/{userId}, PUT /api/v1/preferences/{userId}, DELETE /api/v1/preferences/{userId}
- [X] T068 [US2] Create `ChannelBindingsController` in `src/Maliev.NotificationService.Api/Controllers/ChannelBindingsController.cs` with endpoints per preferences-api.yaml contract
- [X] T069 [US2] Update NotificationRouter to query UserNotificationPreference and ChannelBinding from database before selecting provider
- [X] T070 [US2] Implement default channel logic in NotificationRouter for users without preferences (customer=email, staff=email+Slack, admin=email+SMS)

**Checkpoint**: At this point, User Stories 1 AND 2 should both work - users can manage preferences via API and notifications respect those preferences

---

## Phase 5: User Story 3 - Multi-Template Multi-Language Notification Formatting (Priority: P3)

**Goal**: Enable template-based notification content with parameter substitution, multilingual support, and channel-specific formatting (rich content vs plain text).

**Independent Test**: Create notification template with parameters and multiple languages, trigger notification, verify correct parameter substitution and channel-specific formatting.

### Tests for User Story 3 ✅

- [X] T [P] [US3] Create unit test `TemplateRendererTests.cs` at `tests/Maliev.NotificationService.Api.Tests/Unit/Services/TemplateRendererTests.cs` for parameter substitution logic
- [X] T [P] [US3] Create integration test for template API CRUD operations in `PreferencesApiTests.cs`
- [X] T [P] [US3] Create unit test `TemplateExtensionsTests.cs` at `tests/Maliev.NotificationService.Api.Tests/Unit/Extensions/TemplateExtensionsTests.cs`

### Data Entities for User Story 3

- [X] T [P] [US3] Create `NotificationTemplate` entity in `src/Maliev.NotificationService.Api/Data/Entities/NotificationTemplate.cs` with TemplateKey, Version, Language, ChannelType, ContentTemplate, Parameters (JSONB)
- [X] T [US3] Update `NotificationDbContext` to include DbSet for NotificationTemplate with unique index on (TemplateKey, Version, Language, ChannelType)
- [X] T [US3] Create EF Core migration `003_AddNotificationTemplates` for NotificationTemplate table

### Request/Response Models for User Story 3

- [X] T [P] [US3] Create `CreateTemplateRequest` in `src/Maliev.NotificationService.Api/Models/Requests/CreateTemplateRequest.cs` with validation for template_key format and parameter presence
- [X] T [P] [US3] Create `UpdateTemplateRequest` in `src/Maliev.NotificationService.Api/Models/Requests/UpdateTemplateRequest.cs`
- [X] T [P] [US3] Create `TemplateResponse` in `src/Maliev.NotificationService.Api/Models/Responses/TemplateResponse.cs`

### Services for User Story 3

- [X] T [US3] Create `ITemplateRenderer` interface and implementation `TemplateRenderer` in `src/Maliev.NotificationService.Api/Services/` with: (1) {{parameter}} interpolation using Regex.Replace, (2) validation that all required parameters from template.Parameters array are present in event data, (3) throw TemplateRenderingException with missing parameter names when validation fails, (4) in-memory cache for rendered templates with parameter hash key
- [X] T [P] [US3] Create `TemplateExtensions.cs` in `src/Maliev.NotificationService.Api/Extensions/` with ToResponse and ToEntity extension methods

### API Controller for User Story 3

- [X] T [US3] Create `TemplatesController` in `src/Maliev.NotificationService.Api/Controllers/TemplatesController.cs` with endpoints: POST /api/v1/templates, GET /api/v1/templates/{id}, PUT /api/v1/templates/{id} (admin only)
- [X] T [US3] Update NotificationRouter to query NotificationTemplate by templateId, language, and channelType, then call TemplateRenderer.Render before sending to provider
- [X] T [US3] Seed NotificationDbContext with default templates: order-confirmed (en, th), payment-failed (en, th), system-outage (en)

**Checkpoint**: All user stories 1, 2, AND 3 should now work - templates support multilingual, parameterized content

---

## Phase 6: User Story 4 - Best-Effort Marketing Notifications (Priority: P4)

**Goal**: Support non-critical marketing notifications with relaxed retry constraints (1 retry, 5s delay), batch processing, and rate limit throttling.

**Independent Test**: Submit batch of 10,000 marketing notifications and verify delivery within 24 hours without impacting critical notification performance.

### Tests for User Story 4 ✅

- [X] T085 [P] [US4] Create integration test for marketing notification priority queue in `NotificationDeliveryTests.cs` verifying separate queue processing
- [X] T086 [P] [US4] Create integration test for rate limiting behavior under high load in `RetryLogicTests.cs`

### Implementation for User Story 4

- [X] T087 [US4] Update RetryService to implement different retry strategies: critical (3 retries, exponential backoff) vs standard (1 retry, fixed 5s delay)
- [X] T088 [US4] Configure separate MassTransit queue binding for marketing notifications with routing key `maliev.notification.v1.*.standard` in Program.cs
- [X] T089 [US4] Update NotificationEventConsumer to route marketing vs critical messages to appropriate queues based on priority field
- [X] T090 [US4] Implement priority queue depth monitoring in NotificationMetrics (retry_queue_depth gauge)

**Checkpoint**: All user stories 1-4 work - marketing notifications processed asynchronously without blocking critical messages

---

## Phase 7: User Story 5 - New Channel Provider Integration (Priority: P5)

**Goal**: Enable extensibility by demonstrating multiple channel provider implementations following adapter pattern, allowing new channels to be added without core logic changes.

**Independent Test**: Implement test provider adapter for mock channel, register it, and verify notifications route correctly through the new provider without changes to core service code.

### Tests for User Story 5 ✅

- [X] T091 [P] [US5] Create unit test `LineProviderTests.cs` at `tests/Maliev.NotificationService.Api.Tests/Unit/Providers/LineProviderTests.cs` for LINE API integration
- [X] T092 [P] [US5] Create unit test `SmsProviderTests.cs` at `tests/Maliev.NotificationService.Api.Tests/Unit/Providers/SmsProviderTests.cs` for Twilio integration
- [X] T093 [P] [US5] Create unit test for provider factory pattern in `NotificationRouterTests.cs`

### Channel Provider Implementations for User Story 5

- [X] T094 [P] [US5] Create `LineProvider` in `src/Maliev.NotificationService.Api/Providers/LineProvider.cs` implementing IChannelProvider using LINE Messaging SDK
- [X] T095 [P] [US5] Create `WhatsAppProvider` in `src/Maliev.NotificationService.Api/Providers/WhatsAppProvider.cs` implementing IChannelProvider using Facebook Graph API HttpClient
- [X] T096 [P] [US5] Create `SmsProvider` in `src/Maliev.NotificationService.Api/Providers/SmsProvider.cs` implementing IChannelProvider using Twilio SDK
- [X] T097 [P] [US5] Create `SlackProvider` in `src/Maliev.NotificationService.Api/Providers/SlackProvider.cs` implementing IChannelProvider using Slack Webhooks
- [X] T098 [P] [US5] Create `FacebookMessengerProvider` in `src/Maliev.NotificationService.Api/Providers/FacebookMessengerProvider.cs` implementing IChannelProvider using Graph API
- [X] T099 [P] [US5] Create `InstagramProvider` in `src/Maliev.NotificationService.Api/Providers/InstagramProvider.cs` implementing IChannelProvider using Instagram Messaging API

### Provider Registration & Configuration

- [X] T100 [US5] Create `ChannelProviderFactory` in `src/Maliev.NotificationService.Api/Services/ChannelProviderFactory.cs` to resolve IChannelProvider by channel type
- [X] T101 [US5] Register all channel providers in Program.cs DI container as scoped services with IHttpClientFactory configuration per provider
- [X] T102 [US5] Configure rate limiters in Program.cs for each provider using TokenBucketRateLimiter with provider-specific limits (LINE=1000/s, WhatsApp=80/s, etc.)
- [X] T103 [US5] Update NotificationRouter to use ChannelProviderFactory instead of direct EmailProvider dependency
- [X] T104 [US5] Implement channel binding invalidation logic: when provider returns permanent failure (invalid recipient), mark ChannelBinding.IsValid=false and attempt fallback

**Checkpoint**: All user stories complete - multiple channel providers operational with adapter pattern demonstrating extensibility

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories and production readiness

### Metrics & Observability

- [ ] T105 [P] Create integration test `MetricsTests.cs` at `tests/Maliev.NotificationService.Api.Tests/Integration/MetricsTests.cs` validating all required metrics are exposed
- [ ] T106 [P] Instrument NotificationMetrics counters in NotificationRouter: notification_sent_total, notification_failed_total (with channel and priority tags)
- [ ] T107 [P] Instrument NotificationMetrics histograms in providers: notification_delivery_latency_ms (with channel and status tags)
- [ ] T108 [P] Instrument NotificationMetrics counters in retry service: notification_fallback_total
- [ ] T109 [P] Register NotificationMetrics.Meter with OpenTelemetry in Program.cs
- [ ] T110 [P] Initialize NotificationMetrics.RetryQueueDepth observable gauge with database query lambda in Program.cs

### Delivery Logs API

- [ ] T111 Create `DeliveryLogsController` in `src/Maliev.NotificationService.Api/Controllers/DeliveryLogsController.cs` with GET /api/v1/delivery-logs endpoint supporting filtering and pagination per preferences-api.yaml
- [ ] T112 [P] Implement delivery log cleanup background service using IHostedService: permanently delete DeliveryLog entries older than 90 days (no archival to cold storage), run daily at 2 AM UTC using timer-based execution

### Documentation & Deployment

- [ ] T113 [P] Create `README.md` at repository root with project overview, quick start link, architecture diagram
- [ ] T114 [P] Validate Dockerfile builds successfully with BuildKit secrets: `docker build -f Maliev.NotificationService.Api/Dockerfile --secret id=nuget_username --secret id=nuget_password -t notification-service .`
- [ ] T116 [P] Run all tests and verify >80% code coverage for business-critical logic: `dotnet test /p:CollectCoverage=true`
- [ ] T117 [P] Validate OpenAPI documentation accessible at `/notificationservice/scalar/v1`
- [ ] T118 [P] Validate health check endpoints: `/notificationservice/health`, `/notificationservice/liveness`, `/notificationservice/readiness`
- [ ] T119 [P] Validate metrics endpoint: `/notificationservice/metrics` exposes all required metrics
- [ ] T120 Run quickstart.md validation end-to-end: setup → publish event → verify delivery → query logs

### Security Hardening

- [ ] T121 [P] Implement ChannelBinding.ChannelIdentifier encryption at rest using PostgreSQL pgcrypto extension with AES-256: (1) enable pgcrypto in migration, (2) use pgp_sym_encrypt/pgp_sym_decrypt functions with encryption key retrieved from Google Secret Manager, (3) store encrypted bytea in channel_identifier column
- [ ] T122 [P] Add JWT authentication validation in controllers using `[Authorize]` attributes with scope checks
- [ ] T123 [P] Implement recipient identifier obfuscation in ChannelBindingExtensions.ToResponse (e.g., "j***@example.com")
- [ ] T124 [P] Audit delivery logs to ensure no sensitive message content stored (truncate to 500 chars)

### Code Quality & Refactoring

- [ ] T125 [P] Run .NET code analyzer and fix all warnings to comply with Zero Warnings Policy
- [ ] T126 [P] Verify no banned libraries used: NO AutoMapper, NO FluentValidation, NO FluentAssertions
- [ ] T127 [P] Review all extension methods for proper separation of concerns
- [ ] T128 [P] Add XML documentation comments to all public interfaces and classes
- [ ] T129 [P] Implement monitoring alert webhook integration for critical delivery failures: trigger HTTP POST to alerting system (Prometheus Alertmanager or similar) when DeadLetterRecord is created or when critical notification retry count exceeds threshold
- [ ] T130 [P] Instrument DeduplicationService with cache hit/miss counter metrics: increment deduplication_cache_hits and deduplication_cache_misses counters, calculate hit rate as hits/(hits+misses)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phases 3-7)**: All depend on Foundational phase completion
  - User Story 1 (P1): Can start after Foundational - No dependencies on other stories
  - User Story 2 (P2): Can start after Foundational - Integrates with US1 routing but independently testable
  - User Story 3 (P3): Can start after Foundational - Enhances US1 routing with templates but independently testable
  - User Story 4 (P4): Can start after Foundational - Uses US1 infrastructure but independently testable
  - User Story 5 (P5): Can start after Foundational - Extends US1 providers but independently testable
- **Polish (Phase 8)**: Depends on all desired user stories being complete

### User Story Dependencies (Minimal by Design)

```
Foundational (Phase 2)
    ↓
    ├─→ US1 (P1) ─┐
    ├─→ US2 (P2) ─┤ (Can run in parallel after Foundational)
    ├─→ US3 (P3) ─┤
    ├─→ US4 (P4) ─┤
    └─→ US5 (P5) ─┘
         ↓
    Polish (Phase 8)
```

**Key**: US2-US5 enhance US1 but don't fundamentally block each other. Each story delivers independent value.

### Within Each User Story

1. **Tests FIRST** - Write all tests, verify they FAIL
2. **Models** - Can parallelize all entity creation
3. **Services** - Depend on models being complete
4. **Controllers/Consumers** - Depend on services
5. **Integration** - Wire everything together
6. **Checkpoint** - Validate story independently

### Parallel Opportunities by Phase

**Phase 1 (Setup)**: T003, T004, T005, T006, T007, T008, T009, T010, T012 can run in parallel

**Phase 2 (Foundational)**: T015, T016, T017, T018, T021, T023, T024, T026, T027 can run in parallel

**Phase 3 (US1 Tests)**: T028-T034 can all run in parallel (7 parallel test tasks)

**Phase 3 (US1 Entities)**: T035-T038 can all run in parallel (4 parallel entity tasks)

**Phase 4 (US2 Tests)**: T050-T052 can all run in parallel

**Phase 4 (US2 Entities)**: T053-T054 can run in parallel

**Phase 4 (US2 Models)**: T057-T063 can all run in parallel (7 parallel model tasks)

**Phase 4 (US2 Extensions)**: T064-T066 can all run in parallel

**Phase 5 (US3 Tests)**: T071-T073 can all run in parallel

**Phase 7 (US5 Providers)**: T094-T099 can all run in parallel (6 parallel provider tasks)

**Phase 8 (Metrics)**: T105-T110 can run in parallel

**Phase 8 (Docs)**: T113-T119 can run in parallel

**Phase 8 (Security)**: T121-T124 can run in parallel

**Phase 8 (Quality)**: T125-T128 can run in parallel

---

## Parallel Example: User Story 1

### Test Tasks (Run Together FIRST)
```bash
# All US1 tests can launch in parallel:
[T028] Integration test: NotificationDeliveryTests.cs
[T029] Integration test: RetryLogicTests.cs
[T030] Integration test: FallbackChannelTests.cs
[T031] Integration test: DeduplicationTests.cs
[T032] Unit test: NotificationRouterTests.cs
[T033] Unit test: RetryServiceTests.cs
[T034] Unit test: DeduplicationServiceTests.cs
```

### Entity Tasks (Run Together After Tests)
```bash
# All US1 entities can launch in parallel:
[T035] NotificationEvent model
[T036] DeliveryLog entity
[T037] RetryQueueEntry entity
[T038] DeadLetterRecord entity
```

---

## Parallel Example: User Story 5

### Provider Tasks (Run Together)
```bash
# All channel providers can launch in parallel:
[T094] LineProvider
[T095] WhatsAppProvider
[T096] SmsProvider
[T097] SlackProvider
[T098] FacebookMessengerProvider
[T099] InstagramProvider
```

---

## Implementation Strategy

### MVP First (User Story 1 Only) - RECOMMENDED

1. Complete **Phase 1: Setup** (T001-T012)
2. Complete **Phase 2: Foundational** (T013-T027) - CRITICAL GATE
3. Complete **Phase 3: User Story 1** (T028-T049)
4. **STOP and VALIDATE**:
   - Run all US1 tests (should pass)
   - Manually publish test event to RabbitMQ
   - Verify email delivery with retry/fallback
   - Check delivery logs
   - Validate metrics endpoint
5. Deploy/demo MVP (critical notification delivery operational)

**MVP Scope**: 49 tasks total (Phases 1-3)
**MVP Deliverable**: Critical business notifications via email with guaranteed delivery, retry, fallback, deduplication, and observability

### Incremental Delivery (Recommended Path)

1. **Phase 1-2**: Setup + Foundational → 27 tasks → Foundation ready
2. **Phase 3**: Add US1 → 22 tasks → Test independently → **Deploy MVP**
3. **Phase 4**: Add US2 → 21 tasks → Test independently → **Deploy v1.1** (user preference management)
4. **Phase 5**: Add US3 → 15 tasks → Test independently → **Deploy v1.2** (template support)
5. **Phase 6**: Add US4 → 6 tasks → Test independently → **Deploy v1.3** (marketing notifications)
6. **Phase 7**: Add US5 → 13 tasks → Test independently → **Deploy v1.4** (multi-channel support)
7. **Phase 8**: Polish → 24 tasks → **Deploy v1.5** (production-ready)

Each deployment adds value without breaking previous functionality.

### Parallel Team Strategy

With multiple developers after Foundational phase completes:

- **Developer A**: User Story 1 (T028-T049) - 22 tasks
- **Developer B**: User Story 2 (T050-T070) - 21 tasks
- **Developer C**: User Story 3 (T071-T084) - 14 tasks

Stories complete independently, then integrate and test together.

---

## Task Summary

### Total Tasks: 130

**By Phase**:
- Phase 1 (Setup): 12 tasks
- Phase 2 (Foundational): 15 tasks
- Phase 3 (US1 - Critical Delivery): 22 tasks
- Phase 4 (US2 - Preferences): 21 tasks
- Phase 5 (US3 - Templates): 14 tasks
- Phase 6 (US4 - Marketing): 6 tasks
- Phase 7 (US5 - Multi-Channel): 13 tasks
- Phase 8 (Polish): 27 tasks (includes T129 alert integration, T130 cache metrics)

**By User Story**:
- US1: 22 tasks (7 tests, 4 entities, 4 services, 7 integration)
- US2: 21 tasks (3 tests, 2 entities, 7 models, 3 extensions, 6 controllers/integration)
- US3: 14 tasks (3 tests, 1 entity, 3 models, 2 services, 5 integration)
- US4: 6 tasks (2 tests, 4 integration)
- US5: 13 tasks (3 tests, 6 providers, 4 integration)

**Parallelizable Tasks**: 81 tasks marked [P] (62% can run in parallel within their phase)

**Test Tasks**: 27 tasks (21% of total) - Constitution Principle III compliance

### MVP Scope

**Recommended MVP**: Phases 1-3 only (User Story 1)
- **Total tasks**: 49 tasks
- **Parallel opportunities**: 23 tasks can run concurrently
- **Deliverable**: Critical business notification delivery via email with retry, fallback, deduplication, and full observability
- **Independent test**: Publish payment failure event → verify email delivery within 30s → check delivery logs

### Independent Test Criteria per Story

- **US1**: Publish critical event to RabbitMQ → User receives email notification within 30s → Delivery logged → Metrics updated
- **US2**: Create/update user preferences via API → Publish notification → Verify preferences respected → Fallback works if primary fails
- **US3**: Create template with parameters → Publish event with template ID → Verify rendered content includes substituted parameters
- **US4**: Publish 1000 marketing events → Verify processed within 24h → Critical queue unaffected → Rate limits respected
- **US5**: Register new mock provider → Configure user with mock channel → Publish event → Verify routed to mock provider

---

## Notes

- **[P] tasks** = different files, no blocking dependencies within phase
- **[Story] label** = maps task to user story for traceability and parallel team coordination
- **Test-First**: Constitution mandates tests BEFORE implementation - tests MUST fail initially
- **Testcontainers**: All integration tests use real PostgreSQL, RabbitMQ, Redis (no in-memory substitutes)
- **Extension Methods**: All mapping uses extension methods (NO AutoMapper per constitution)
- **DataAnnotations**: All validation uses DataAnnotations (NO FluentValidation per constitution)
- **Standard Assert**: All tests use xUnit Assert.* (NO FluentAssertions per constitution)
- **Each user story independently testable** - can deploy after any phase 3-7 completion
- **Commit frequently**: After each task or logical group
- **Stop at checkpoints**: Validate each story works before proceeding

---

**Tasks Version**: 1.0
**Generated**: 2025-12-05
**Ready for**: Implementation (Test-First approach)
**Suggested Start**: MVP (Phases 1-3, 49 tasks)
