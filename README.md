# Maliev Notification Service

Multi-channel notification delivery platform with guaranteed delivery, retry logic, fallback support, and template-based messaging.

## Overview

The Notification Service is a production-ready, cloud-native microservice built on .NET 10 and .NET Aspire ServiceDefaults. It provides reliable notification delivery across multiple channels (Email, LINE, WhatsApp, SMS, Slack, Facebook Messenger, Instagram) with enterprise features including:

- **Guaranteed Delivery**: Automatic retry with exponential backoff and fallback channel support
- **Multi-Channel Support**: Extensible provider architecture for 7+ communication channels
- **Priority-Based Routing**: Separate queues for critical and marketing notifications
- **Template System**: Multi-language templates with parameter substitution
- **Deduplication**: Redis-based duplicate detection with 24-hour TTL
- **User Preferences**: Granular control over notification channels and opt-out categories
- **Observability**: OpenTelemetry metrics, health checks, and structured logging

## Quick Start

### Prerequisites

- .NET 10 SDK
- Docker Desktop
- Git

### Get Started in 5 Minutes

```bash
# 1. Clone and checkout
git clone https://github.com/maliev/Maliev.NotificationService.git
cd Maliev.NotificationService
git checkout develop

# 2. Run the service
cd src/Maliev.NotificationService.Api
dotnet run

# 4. Verify service is running
curl http://localhost:8080/notificationservice/health
```

For detailed setup instructions, see [Quick Start Guide](specs/001-notification-service/quickstart.md).

## Architecture

### Tech Stack

| Component | Technology | Purpose |
|-----------|-----------|---------|
| **Runtime** | .NET 10 | Application platform |
| **Framework** | ASP.NET Core | Web API framework |
| **Messaging** | RabbitMQ + MassTransit | Event-driven architecture |
| **Database** | PostgreSQL 18 | Primary data store |
| **Cache** | Redis 7 | Deduplication cache |
| **Observability** | OpenTelemetry | Metrics, tracing, logging |
| **API Docs** | Scalar (OpenAPI 3.1) | Interactive API documentation |

### System Architecture

```
┌──────────────┐
│ Event Source │ (Order Service, Payment Service, etc.)
└──────┬───────┘
       │ Publishes NotificationEvent
       ▼
┌─────────────────────────────────────────────────────────┐
│ RabbitMQ (maliev.notifications exchange)                │
│  ├─ notification-critical (prefetch=10, fast)           │
│  └─ notification-standard (prefetch=50, throughput)     │
└──────┬────────────────────────────────────────┬─────────┘
       │                                        │
       ▼                                        ▼
┌──────────────────────┐              ┌──────────────────┐
│ NotificationConsumer │              │ NotificationConsumer│
│ (MassTransit)        │              │ (MassTransit)       │
└──────┬───────────────┘              └──────┬─────────────┘
       │                                     │
       ▼                                     │
┌──────────────────────┐                     │
│ DeduplicationService │◄────────────────────┘
│ (Redis Cache)        │
└──────┬───────────────┘
       │
       ▼
┌──────────────────────┐       ┌──────────────────┐
│ NotificationRouter   │──────►│ TemplateRenderer │
│ (Preference Logic)   │       │ (Mustache-style) │
└──────┬───────────────┘       └──────────────────┘
       │
       ├─────► EmailProvider ──────► SendGrid/SMTP
       ├─────► LineProvider ───────► LINE Messaging API
       ├─────► WhatsAppProvider ───► Facebook Graph API
       ├─────► SmsProvider ─────────► Twilio
       ├─────► SlackProvider ───────► Slack Webhooks
       ├─────► FacebookMessengerProvider ─► Facebook Graph API
       └─────► InstagramProvider ───► Instagram Messaging API
```

### Data Model

**Core Entities**:
- `DeliveryLog` - Audit trail of all notification deliveries
- `RetryQueueEntry` - Failed notifications pending retry
- `DeadLetterRecord` - Permanently failed notifications
- `NotificationTemplate` - Multi-language message templates
- `UserNotificationPreference` - User channel preferences
- `ChannelBinding` - User contact information per channel

For detailed entity relationships, see [Data Model](specs/001-notification-service/data-model.md).

## Features

### User Story 1: Critical Business Notifications (MVP)

Deliver high-priority notifications (payment failures, order confirmations, system outages) with guaranteed delivery.

**Features**:
- Priority-based routing (critical vs standard)
- Retry with exponential backoff (3 attempts: 1s, 2s, 4s)
- Fallback channel support
- Redis-based deduplication (24h TTL)
- Delivery tracking and audit logs

**Example**:
```bash
# Publish payment failure event
curl -X POST http://localhost:15672/api/exchanges/maliev/maliev.notifications/publish \
  -u notification_user:notification_pass \
  -H "Content-Type: application/json" \
  -d '{
    "routing_key": "maliev.notification.v1.payment-failed.critical",
    "payload": "{\"id\":\"evt-123\",\"data\":{\"notificationType\":\"PaymentFailed\",\"priority\":\"critical\",\"targetUsers\":[{\"userId\":\"user123\",\"userType\":\"customer\"}],\"parameters\":{\"amount\":\"1500 THB\",\"reason\":\"Insufficient funds\"}}}"
  }'
```

### User Story 2: User Preference Management

Users can configure their preferred notification channels and opt-out settings via REST API.

**Features**:
- CRUD operations for user preferences
- Primary + fallback channel configuration
- Category-based opt-out
- Channel binding with validation

**Example**:
```bash
# Create user preferences
curl -X POST http://localhost:8080/api/v1/preferences \
  -H "Content-Type: application/json" \
  -d '{
    "userId": "user123",
    "primaryChannelType": "line",
    "fallbackChannelTypes": ["email", "sms"],
    "optOutCategories": ["marketing"]
  }'
```

### User Story 3: Multi-Language Templates

Template-based notifications with parameter substitution and multi-language support.

**Features**:
- Mustache-style parameter substitution ({{name}}, {{orderId}})
- Version control for templates
- Channel-specific formatting
- Language fallback (defaults to English)

**Example Template**:
```
Template: order-confirmed (en, v1)
---
Hello {{name}},

Your order #{{orderId}} has been confirmed!

Order total: {{amount}}

Thank you for your business.
```

### User Story 4: Marketing Notifications

Best-effort delivery for non-critical marketing campaigns with relaxed retry policies.

**Features**:
- Separate standard priority queue
- Reduced retry attempts (1 retry, 5s delay)
- Higher prefetch for throughput (50 vs 10)
- Rate limiting to prevent provider throttling

### User Story 5: Channel Provider Extensibility

Adapter pattern allows adding new notification channels without core logic changes.

**Supported Channels**:
- Email (SendGrid/SMTP)
- LINE Messaging API
- WhatsApp Business API
- SMS (Twilio)
- Slack Webhooks
- Facebook Messenger
- Instagram Direct

**Rate Limits** (configurable):
- LINE: 10 msg/s (1000 token bucket)
- WhatsApp: 80 msg/s
- Email: 100 msg/s
- SMS: 100 msg/s
- Slack: 1 msg/s
- Facebook: 200 msg/s
- Instagram: 200 msg/s

## API Endpoints

### Preferences API

- `POST /api/v1/preferences` - Create user preferences
- `GET /api/v1/preferences/{userId}` - Get user preferences
- `PUT /api/v1/preferences/{userId}` - Update preferences
- `DELETE /api/v1/preferences/{userId}` - Delete preferences

### Channel Bindings API

- `POST /api/v1/channel-bindings` - Create channel binding
- `GET /api/v1/channel-bindings?userId={id}` - List user bindings
- `PUT /api/v1/channel-bindings/{id}` - Update binding
- `DELETE /api/v1/channel-bindings/{id}` - Delete binding

### Templates API

- `POST /api/v1/templates` - Create template
- `GET /api/v1/templates/{id}` - Get template
- `PUT /api/v1/templates/{id}` - Update template

### Delivery Logs API

- `GET /api/v1/delivery-logs` - Query delivery logs (with filtering and pagination)
- `GET /api/v1/delivery-logs/{id}` - Get specific log

### Health & Observability

- `GET /notificationservice/health` - Aggregate health status
- `GET /notificationservice/liveness` - Kubernetes liveness probe
- `GET /notificationservice/readiness` - Kubernetes readiness probe
- `GET /notificationservice/metrics` - Prometheus metrics (OpenTelemetry format)

### API Documentation

- `GET /notificationservice/scalar/v1` - Interactive API documentation (Scalar UI)

## Metrics

The service exposes OpenTelemetry metrics at `/notificationservice/metrics`:

| Metric | Type | Description |
|--------|------|-------------|
| `notification_sent_total` | Counter | Total notifications sent (tags: channel, priority) |
| `notification_failed_total` | Counter | Total failed notifications (tags: channel, error_type) |
| `notification_fallback_total` | Counter | Fallback channel activations (tags: primary_channel, reason) |
| `notification_delivery_latency_ms` | Histogram | Delivery latency in milliseconds (tags: channel, status) |
| `notification_retry_queue_depth` | Gauge | Current retry queue depth |
| `notification_deduplication_cache_hits` | Counter | Duplicate events detected |
| `notification_deduplication_cache_misses` | Counter | Unique events processed |
| `notification_auth_denials_total` | Counter | Permission-based access denials |

## Authorization

The NotificationService uses a granular permission-based authorization system aligned with GCP standards.

### Permissions

The service defines 23 permissions spanning Templates, Notifications, Bindings, Preferences, and Logs. Examples:
- `notification.templates.create`: Create notification templates
- `notification.notifications.send`: Send notifications to users
- `notification.bindings.list-user`: List user's channel bindings

### Predefined Roles

- `roles.notification.admin`: Full control.
- `roles.notification.manager`: Manage templates and view logs.
- `roles.notification.sender`: Send notifications and view status.
- `roles.notification.user`: Manage own data (Self-service).

### Self-Service Access

Users are automatically granted access to their own data (preferences, bindings, logs) without requiring administrative permissions, provided the `sub` claim in their JWT matches the `userId` of the resource.

### Feature Flag

Enable/disable IAM integration via `Features:PermissionBasedAuthEnabled` in `appsettings.json`.

## Configuration

### Connection Strings

```json
{
  "ConnectionStrings": {
    "NotificationDbContext": "Host=localhost;Database=notification_db;Username=user;Password=pass",
    "redis": "localhost:6379",
    "rabbitmq": "amqp://user:pass@localhost:5672/maliev"
  }
}
```

### External Provider Credentials

Credentials are loaded from Google Secret Manager in production:

```json
{
  "ExternalProviders": {
    "Line": {
      "ChannelAccessToken": "YOUR_CHANNEL_ACCESS_TOKEN",
      "ChannelSecret": "YOUR_CHANNEL_SECRET"
    },
    "WhatsApp": {
      "AccessToken": "YOUR_ACCESS_TOKEN",
      "PhoneNumberId": "YOUR_PHONE_NUMBER_ID"
    },
    "SendGrid": {
      "ApiKey": "YOUR_API_KEY",
      "FromEmail": "noreply@maliev.com"
    },
    "Twilio": {
      "AccountSid": "YOUR_ACCOUNT_SID",
      "AuthToken": "YOUR_AUTH_TOKEN",
      "PhoneNumber": "YOUR_PHONE_NUMBER"
    },
    "Slack": {
      "WebhookUrl": "YOUR_WEBHOOK_URL"
    }
  }
}
```

## Development

### Running Tests

```bash
# Unit tests
dotnet test --filter Category=Unit

# Integration tests (requires Docker)
dotnet test --filter Category=Integration

# All tests with coverage
dotnet test /p:CollectCoverage=true
```

### Database Migrations

```bash
# Add new migration
dotnet ef migrations add MigrationName

# Apply migrations
dotnet ef database update

# Rollback migration
dotnet ef database update PreviousMigrationName
```

### Docker Build

```bash
# Build image
docker build -f Maliev.NotificationService.Api/Dockerfile -t maliev-notification-service:latest .

# Run container
docker run -d -p 8080:8080 \
  -e ConnectionStrings__NotificationDbContext="YOUR_CONNECTION_STRING" \
  -e ConnectionStrings__redis="YOUR_CONNECTION_STRING" \
  -e ConnectionStrings__rabbitmq="YOUR_CONNECTION_STRING" \
  maliev-notification-service:latest
```

## Project Structure

```
Maliev.NotificationService/
├── src/
│   └── Maliev.NotificationService.Api/
│       ├── Consumers/           # MassTransit message consumers
│       ├── Controllers/         # REST API controllers
│       ├── Data/                # EF Core DbContext, entities, migrations
│       ├── Extensions/          # Extension methods for mapping
│       ├── Metrics/             # OpenTelemetry metrics
│       ├── Middleware/          # Exception handling middleware
│       ├── Models/              # DTOs, events, enums
│       ├── Providers/           # Channel provider implementations
│       ├── Services/            # Business logic services
│       └── Program.cs           # Application entry point
├── tests/
│   └── Maliev.NotificationService.Api.Tests/
│       ├── Integration/         # Integration tests (with Testcontainers)
│       ├── Unit/                # Unit tests
│       └── TestHelpers/         # Test utilities
├── specs/
│   └── 001-notification-service/
│       ├── spec.md              # Detailed specification
│       ├── plan.md              # Implementation plan
│       ├── tasks.md             # Task breakdown
│       ├── data-model.md        # Entity relationship diagram
│       ├── quickstart.md        # Developer quick start guide
│       └── contracts/           # OpenAPI contracts
├── Maliev.NotificationService.Api/
│   ├── Dockerfile               # Multi-stage build definition
└── nuget.config                 # NuGet package sources
```

## Documentation

- [Quick Start Guide](specs/001-notification-service/quickstart.md) - Setup and local development
- [Specification](specs/001-notification-service/spec.md) - Detailed requirements
- [Implementation Plan](specs/001-notification-service/plan.md) - Architecture decisions
- [Task Breakdown](specs/001-notification-service/tasks.md) - Development tasks
- [Data Model](specs/001-notification-service/data-model.md) - Entity relationships
- [API Contracts](specs/001-notification-service/contracts/) - OpenAPI specifications

## Support

### Troubleshooting

See [Troubleshooting section](specs/001-notification-service/quickstart.md#troubleshooting) in the Quick Start Guide.

### Common Issues

1. **Build Errors**: Ensure .NET 10 SDK is installed and NuGet credentials are configured
2. **Missing Dependencies**: Run `dotnet restore` to restore NuGet packages
4. **Test Failures**: Ensure Docker is running for Testcontainers integration tests

## Contributing

This project follows the MALIEV Constitution principles:

1. **Data Sovereignty**: All user data stored in PostgreSQL with full control
2. **Vendor Independence**: Extensible provider architecture with no hard dependencies
3. **Test-First Development**: Tests written before implementation
4. **Zero Warnings Policy**: All compiler warnings treated as errors
5. **No Banned Libraries**: NO AutoMapper, NO FluentValidation, NO FluentAssertions

For coding standards, see [.specify/memory/constitution.md](.specify/memory/constitution.md).

## License

Copyright (C) 2025 MALIEV. All rights reserved.

---

**Version**: 1.0.0
**Branch**: 001-notification-service
**Last Updated**: 2025-12-09
**Status**: Production Ready
