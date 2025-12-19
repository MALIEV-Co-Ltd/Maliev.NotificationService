# Implementation Plan: Notification Service

**Branch**: `001-notification-service` | **Date**: 2025-12-05 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-notification-service/spec.md`

## Summary

The Notification Service is a centralized, event-driven microservice responsible for delivering messages and alerts across multiple communication channels (LINE, WhatsApp, Email, SMS, Slack, Facebook Messenger, Instagram) in the MALIEV ecosystem. It consumes notification events from RabbitMQ, resolves user notification preferences, routes messages through appropriate channel providers using an adapter pattern, and ensures reliable delivery with retry logic, fallback channels, and comprehensive observability. The service maintains user preferences, channel bindings, notification templates, and delivery logs in PostgreSQL, uses Redis for deduplication caching, and exposes metrics for operational monitoring.

## Technical Context

**Language/Version**: .NET 10.0
**Primary Dependencies**:
- Maliev.Aspire.ServiceDefaults (NuGet from GitHub Packages)
- Npgsql.EntityFrameworkCore.PostgreSQL
- MassTransit.RabbitMQ
- Microsoft.AspNetCore.OpenApi / Scalar.AspNetCore
- AspNetCore.HealthChecks.UI.Client
- StackExchange.Redis (via ServiceDefaults)

**Storage**:
- PostgreSQL (user preferences, channel bindings, delivery logs, notification templates, retry queue entries, dead letter records)
- Redis (deduplication cache with 24-hour TTL)

**Testing**: xUnit, Moq, Testcontainers.PostgreSql, Testcontainers.RabbitMQ, Testcontainers.Redis

**Target Platform**: Linux server, containerized (Docker), horizontally scalable

**Project Type**: Web API microservice with background event processing

**Performance Goals**:
- 10,000 concurrent notification deliveries
- 95% of critical notifications delivered within 30 seconds
- 99% first-attempt delivery success rate
- 99.9% uptime for critical notification processing

**Constraints**:
- <30s p95 latency for critical notification delivery (from event to delivery)
- 3 retry attempts with exponential backoff (1s, 2s, 4s) for critical notifications
- 1 retry with 5s delay for non-critical notifications
- 24-hour deduplication window
- 90-day delivery log retention
- Encryption at rest for channel binding data

**Scale/Scope**:
- 8+ external messaging providers (LINE, WhatsApp, Email, SMS, Slack, Facebook Messenger, Instagram, future channels)
- Event-driven architecture consuming from RabbitMQ
- Multi-language template support (English, Thai, extensible)
- Multi-channel message formatting (rich content, HTML, plain text)
- User-type-based default channels (customer, staff, administrator)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| **I. Service Autonomy** | ✅ PASS | Own PostgreSQL database schema, own domain logic, interacts via RabbitMQ events and REST APIs only |
| **II. Explicit Contracts** | ✅ PASS | Will document APIs via OpenAPI/Scalar, version notification event schemas |
| **III. Test-First Development** | ✅ PASS | Tests will be authored immediately after plan approval, before implementation |
| **IV. Real Infrastructure Testing** | ✅ PASS | Will use Testcontainers for PostgreSQL, RabbitMQ, and Redis - no in-memory substitutes |
| **V. Auditability & Observability** | ✅ PASS | FR-016 requires delivery logs with timestamps, status, provider responses. FR-021 requires structured metrics |
| **VI. Security & Compliance** | ✅ PASS | JWT authentication for APIs, NFR-005 requires encryption at rest, GDPR compliance noted in assumptions |
| **VII. Secrets Management** | ✅ PASS | External provider credentials stored in Google Secret Manager, injected via `/mnt/secrets` |
| **VIII. Zero Warnings Policy** | ✅ PASS | Standard .NET build configuration enforces this |
| **IX. Clean Project Artifacts** | ✅ PASS | `.gitignore` and `.dockerignore` will exclude build artifacts and temp files |
| **X. Docker Best Practices** | ✅ PASS | Will use standard Dockerfile pattern with built-in `app` user, BuildKit secrets for NuGet auth |
| **XI. Simplicity & Maintainability** | ✅ PASS | Clean architecture with adapter pattern for channel providers, stateless API design |
| **XII. Business Metrics & Analytics** | ✅ PASS | FR-021 requires delivery success rate per channel, retry queue depth, latency, provider error rates, deduplication cache hit rate |
| **XIII. .NET Aspire Integration** | ✅ PASS | Will consume ServiceDefaults as NuGet package from GitHub Packages, call `AddServiceDefaults()` and `MapDefaultEndpoints()` |
| **XIV. Code Quality & Library Standards** | ✅ PASS | Will use extension methods for mapping (no AutoMapper), DataAnnotations for validation (no FluentValidation), standard xUnit Assert (no FluentAssertions) |

**Result**: All gates PASS. No violations. Proceed to Phase 0.

## Project Structure

### Documentation (this feature)

```text
specs/001-notification-service/
├── spec.md              # Feature specification (completed)
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (to be generated)
├── data-model.md        # Phase 1 output (to be generated)
├── quickstart.md        # Phase 1 output (to be generated)
├── contracts/           # Phase 1 output (to be generated)
│   ├── notification-event-schema.json
│   ├── preferences-api.yaml
│   └── metrics-api.yaml
├── checklists/          # Quality validation
│   └── requirements.md  # Spec quality checklist (completed)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
Maliev.NotificationService/
├── src/
│   └── Maliev.NotificationService.Api/
│       ├── Program.cs
│       ├── Controllers/
│       │   ├── PreferencesController.cs
│       │   ├── ChannelBindingsController.cs
│       │   └── TemplatesController.cs
│       ├── Consumers/
│       │   └── NotificationEventConsumer.cs
│       ├── Services/
│       │   ├── INotificationRouter.cs
│       │   ├── NotificationRouter.cs
│       │   ├── IRetryService.cs
│       │   ├── RetryService.cs
│       │   ├── IDeduplicationService.cs
│       │   ├── DeduplicationService.cs
│       │   ├── ITemplateRenderer.cs
│       │   └── TemplateRenderer.cs
│       ├── Providers/
│       │   ├── IChannelProvider.cs
│       │   ├── LineProvider.cs
│       │   ├── WhatsAppProvider.cs
│       │   ├── EmailProvider.cs
│       │   ├── SmsProvider.cs
│       │   ├── SlackProvider.cs
│       │   ├── FacebookMessengerProvider.cs
│       │   └── InstagramProvider.cs
│       ├── Data/
│       │   ├── NotificationDbContext.cs
│       │   ├── Entities/
│       │   │   ├── UserNotificationPreference.cs
│       │   │   ├── ChannelBinding.cs
│       │   │   ├── NotificationTemplate.cs
│       │   │   ├── DeliveryLog.cs
│       │   │   ├── RetryQueueEntry.cs
│       │   │   └── DeadLetterRecord.cs
│       │   └── Migrations/
│       ├── Models/
│       │   ├── Requests/
│       │   │   ├── CreatePreferenceRequest.cs
│       │   │   ├── UpdatePreferenceRequest.cs
│       │   │   ├── CreateChannelBindingRequest.cs
│       │   │   └── CreateTemplateRequest.cs
│       │   ├── Responses/
│       │   │   ├── PreferenceResponse.cs
│       │   │   ├── ChannelBindingResponse.cs
│       │   │   ├── TemplateResponse.cs
│       │   │   └── DeliveryLogResponse.cs
│       │   └── Events/
│       │       └── NotificationEvent.cs
│       ├── Extensions/
│       │   ├── PreferenceExtensions.cs
│       │   ├── ChannelBindingExtensions.cs
│       │   ├── TemplateExtensions.cs
│       │   └── DeliveryLogExtensions.cs
│       ├── Middleware/
│       │   └── ExceptionHandlingMiddleware.cs
│       └── Metrics/
│           └── NotificationMetrics.cs
├── tests/
│   └── Maliev.NotificationService.Api.Tests/
│       ├── Integration/
│       │   ├── TestWebApplicationFactory.cs
│       │   ├── TestDatabaseFixture.cs
│       │   ├── PreferencesApiTests.cs
│       │   ├── NotificationDeliveryTests.cs
│       │   ├── RetryLogicTests.cs
│       │   ├── FallbackChannelTests.cs
│       │   ├── DeduplicationTests.cs
│       │   └── MetricsTests.cs
│       ├── Unit/
│       │   ├── Services/
│       │   │   ├── NotificationRouterTests.cs
│       │   │   ├── RetryServiceTests.cs
│       │   │   ├── DeduplicationServiceTests.cs
│       │   │   └── TemplateRendererTests.cs
│       │   ├── Providers/
│       │   │   ├── LineProviderTests.cs
│       │   │   ├── EmailProviderTests.cs
│       │   │   └── SmsProviderTests.cs
│       │   └── Extensions/
│       │       └── MappingExtensionsTests.cs
│       └── TestHelpers/
│           └── MockHttpMessageHandler.cs
├── Maliev.NotificationService.Api/
│   ├── Dockerfile
├── .dockerignore
├── nuget.config
├── .gitignore
├── Maliev.NotificationService.sln
└── README.md
```

**Structure Decision**: Single API project structure selected. This is a focused microservice with one primary responsibility (notification delivery). All domain logic, event consumers, channel providers, and data access are contained within a single API project for simplicity and maintainability. Tests are organized into Integration (with Testcontainers) and Unit (with mocked dependencies) categories.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

No violations detected. All constitutional requirements satisfied.

---

## Phase 0: Research & Technology Decisions

**Status**: ✅ Completed

**Unknowns to Research**:
1. Channel provider SDK selection and best practices (LINE, WhatsApp, Facebook, Instagram APIs)
2. Rate limiting patterns for external provider APIs
3. RabbitMQ event schema design and versioning patterns for MALIEV ecosystem
4. Redis deduplication cache implementation patterns with TTL
5. MassTransit consumer configuration for priority-based message processing
6. Template rendering engine selection (lightweight, parameter substitution, multilingual)
7. Background job processing patterns for retry queue
8. Dead-letter queue patterns in MassTransit
9. Metrics instrumentation patterns for OpenTelemetry in .NET
10. Channel provider adapter interface design patterns

**Output**: `research.md` with decisions, rationale, and alternatives for each unknown

---

## Phase 1: Data Model & API Contracts

**Status**: ✅ Completed

**Artifacts to Generate**:
1. `data-model.md` - Entity definitions, relationships, validation rules, state transitions
2. `contracts/notification-event-schema.json` - RabbitMQ event payload schema
3. `contracts/preferences-api.yaml` - OpenAPI spec for preferences management endpoints
4. `contracts/metrics-api.yaml` - OpenAPI spec for metrics exposure endpoints
5. `quickstart.md` - Developer setup guide

**Key Entities** (from spec):
- UserNotificationPreference (user settings, channel priority, opt-in/out)
- ChannelBinding (user-to-channel identifiers: LINE ID, email, phone, etc.)
- NotificationTemplate (content with placeholders, multilingual, versioned)
- DeliveryLog (audit trail: timestamp, status, provider response)
- RetryQueueEntry (failed notification with retry metadata)
- DeadLetterRecord (exhausted retries, escalation status)
- NotificationEvent (incoming from RabbitMQ: type, users, content, priority)

**API Endpoints** (from functional requirements):
- `POST /api/v1/preferences` - Create user notification preferences
- `PUT /api/v1/preferences/{userId}` - Update user preferences
- `GET /api/v1/preferences/{userId}` - Get user preferences
- `DELETE /api/v1/preferences/{userId}` - Delete user preferences
- `POST /api/v1/channel-bindings` - Create channel binding
- `PUT /api/v1/channel-bindings/{id}` - Update channel binding
- `DELETE /api/v1/channel-bindings/{id}` - Delete channel binding
- `GET /api/v1/channel-bindings/{userId}` - Get user's channel bindings
- `POST /api/v1/templates` - Create notification template
- `PUT /api/v1/templates/{id}` - Update template
- `GET /api/v1/templates/{id}` - Get template
- `GET /api/v1/delivery-logs` - Query delivery logs (filtered)
- `GET /notificationservice/metrics` - Prometheus metrics endpoint
- `GET /notificationservice/health` - Health check endpoints (liveness, readiness)

---

## Phase 2: Task Decomposition

**Status**: NOT executed by `/speckit.plan` - requires separate `/speckit.tasks` command

**Output**: `tasks.md` with dependency-ordered implementation tasks

---

## Implementation Notes

### Critical Path (P1 - MVP)
1. Database schema & migrations (entities, relationships)
2. RabbitMQ event consumer setup (MassTransit configuration)
3. User preference resolution service (with default channel logic)
4. Channel provider adapter interface + Email provider implementation
5. Notification routing service (preference → provider)
6. Retry logic service (exponential backoff)
7. Fallback channel logic
8. Deduplication service (Redis-backed)
9. Delivery logging
10. Basic metrics exposure

### Secondary Features (P2-P5)
- Additional channel providers (LINE, WhatsApp, SMS, Slack, Facebook, Instagram)
- Template rendering engine with parameter substitution
- Multilingual template support
- Dead-letter queue handling
- Channel binding invalidation on permanent failures
- Comprehensive metrics dashboard
- Provider adapter extensibility

### Testing Strategy
1. **Unit Tests**: Services, providers, mapping extensions (mocked dependencies)
2. **Integration Tests**:
   - API endpoints (Testcontainers for PostgreSQL, Redis)
   - Event consumption (Testcontainers for RabbitMQ)
   - End-to-end notification delivery flow (mocked external providers via MockHttpMessageHandler)
   - Retry and fallback logic
   - Deduplication caching
   - Metrics validation
3. **Contract Tests**: RabbitMQ event schema validation

### Key Design Patterns
- **Adapter Pattern**: IChannelProvider interface for pluggable channel integrations
- **Strategy Pattern**: Retry strategies for critical vs non-critical notifications
- **Repository Pattern**: NOT USED (direct EF Core DbContext per constitution simplicity principle)
- **Extension Methods**: Explicit entity-to-DTO mapping (no AutoMapper)
- **Background Processing**: MassTransit consumers for async event handling
- **Circuit Breaker**: Via Polly policies in ServiceDefaults for external provider calls

### External Provider Integration Points
Each channel provider implements `IChannelProvider`:
```csharp
public interface IChannelProvider
{
    string ChannelType { get; }
    Task<DeliveryResult> SendAsync(string recipientId, string message, CancellationToken ct);
    Task<ValidationResult> ValidateRecipientAsync(string recipientId, CancellationToken ct);
    Task<HealthCheckResult> GetHealthAsync(CancellationToken ct);
}
```

**Interface Methods**:
- `ChannelType`: Returns channel identifier (e.g., "email", "line", "sms")
- `SendAsync`: Sends notification to recipient, returns DeliveryResult with success status, provider message ID, error details
- `ValidateRecipientAsync`: Validates recipient identifier format and existence, returns ValidationResult
- `GetHealthAsync`: Checks provider API connectivity and authentication, returns HealthCheckResult for health endpoint aggregation

Providers encapsulate:
- Authentication (API keys from Secret Manager)
- Rate limiting (provider-specific)
- Message formatting (rich content vs plain text)
- Error handling (transient vs permanent failures)

### Performance Measurement Methodology

**NFR-001 (30s delivery latency p95)**:
- Measurement: p95 latency calculated from RabbitMQ event timestamp (`NotificationEvent.Time`) to `DeliveryLog.CreatedAt` for successful deliveries
- Collection: Histogram metric `notification_delivery_latency_ms` with percentile aggregation (p50, p95, p99)
- Alerting: Trigger alert when p95 exceeds 30,000ms over 5-minute rolling window

**NFR-002 (10,000 concurrent deliveries)**:
- Measurement: Sustained throughput measured as successful deliveries per second during load testing
- Load Test: Apache JMeter or K6 with 10,000 concurrent event publications to RabbitMQ
- Success Criteria: System maintains >95% delivery success rate at 10,000 concurrent operations without CPU/memory saturation

**NFR-003 (99.9% uptime)**:
- Measurement: (total minutes - downtime minutes) / total minutes over rolling 30-day window
- Uptime Definition: `/notificationservice/health` endpoint returns HTTP 200 with all dependencies healthy
- Downtime Definition: 3 consecutive failed health checks (30-second interval = 90 seconds outage minimum)
- Monitoring: External uptime monitor (Pingdom, UptimeRobot) polling health endpoint every 30 seconds

### Observability Requirements
**Structured Logging** (ILogger):
- All notification events (received, routed, delivered, failed)
- Retry attempts with backoff intervals
- Channel binding invalidations
- Deduplication cache hits/misses

**Metrics** (OpenTelemetry via ServiceDefaults):
- `notification_delivery_success_rate` (by channel)
- `notification_retry_queue_depth`
- `notification_delivery_latency_ms` (p50, p95, p99)
- `notification_provider_error_rate` (by provider, by error type)
- `notification_deduplication_cache_hit_rate`
- `notification_processed_total` (by type: critical, marketing)
- `notification_fallback_triggered_total`

**Health Checks**:
- PostgreSQL connection (via ServiceDefaults)
- Redis connection (via ServiceDefaults)
- RabbitMQ connection (via MassTransit health check)
- DbContext readiness

---

## Risks & Mitigations

| Risk | Impact | Mitigation Strategy |
|------|--------|---------------------|
| External provider rate limits exceeded | Delivery delays, failures | Implement per-provider rate limiting using token bucket pattern, separate queues for batch processing |
| RabbitMQ consumer overwhelmed by event spike | Message backlog, delayed delivery | Implement consumer prefetch limits, backpressure monitoring, horizontal scaling based on queue depth |
| Deduplication cache eviction before 24h window | Duplicate notifications sent | Use Redis TTL with monitoring, alerts on cache memory pressure, fallback to database check |
| Channel provider API changes | Integration breakage | Version provider adapters, comprehensive integration tests, monitor provider changelogs |
| Template rendering performance bottleneck | Slow notification processing | Cache rendered templates with parameter hashing, consider precompilation for frequently used templates |
| Database connection pool exhaustion | Service degradation | Configure connection pool limits, use connection resilience policies from ServiceDefaults, monitor connection metrics |

---

## Next Steps

1. **Execute Phase 0**: Generate `research.md` with technology decisions
2. **Execute Phase 1**: Generate `data-model.md`, `contracts/`, `quickstart.md`
3. **Update Agent Context**: Run `.specify/scripts/powershell/update-agent-context.ps1`
4. **Task Generation** (separate command): Run `/speckit.tasks` to generate `tasks.md`
5. **Implementation**: Begin Red-Green-Refactor cycle with test-first approach

---

**Plan Version**: 1.0
**Last Updated**: 2025-12-05
**Status**: Phase 0 & Phase 1 Complete - Ready for Task Generation
