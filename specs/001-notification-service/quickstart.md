# Quick Start Guide: Notification Service

**Feature**: Notification Service
**Branch**: `001-notification-service`
**Date**: 2025-12-05

## Table of Contents

- [Prerequisites](#prerequisites)
- [Local Development Setup](#local-development-setup)
- [Running the Service](#running-the-service)
- [Testing](#testing)
- [API Usage Examples](#api-usage-examples)
- [Troubleshooting](#troubleshooting)

---

## Prerequisites

### Required Tools
- **.NET 10 SDK** - [Download here](https://dotnet.microsoft.com/download/dotnet/10.0)
- **Docker Desktop** - [Download here](https://www.docker.com/products/docker-desktop)
- **Git** - For repository access
- **Visual Studio 2025** or **VS Code with C# DevKit** (recommended IDEs)

### External Services (via Docker)
- **PostgreSQL 16** - Primary database
- **Redis 7** - Deduplication cache
- **RabbitMQ 3.13** - Event bus

### Optional Tools
- **Postman** or **curl** - API testing
- **Azure Data Studio** or **pgAdmin** - Database management
- **Redis Insight** - Redis cache inspection

---

## Local Development Setup

### 1. Clone Repository

```bash
git clone https://github.com/maliev/Maliev.NotificationService.git
cd Maliev.NotificationService
git checkout 001-notification-service
```

### 2. Configure Application

Create `appsettings.Development.json` in `src/Maliev.NotificationService.Api/`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "NotificationDbContext": "Host=localhost;Port=5432;Database=notification_db;Username=notification_user;Password=notification_pass",
    "redis": "localhost:6379",
    "rabbitmq": "amqp://notification_user:notification_pass@localhost:5672/maliev"
  },
  "ExternalProviders": {
    "Line": {
      "ChannelAccessToken": "<your-token-here>",
      "ChannelSecret": "YOUR_LINE_CHANNEL_SECRET"
    },
    "WhatsApp": {
      "AccessToken": "<your-token-here>",
      "PhoneNumberId": "YOUR_PHONE_NUMBER_ID"
    },
    "SendGrid": {
      "ApiKey": "YOUR_SENDGRID_API_KEY",
      "FromEmail": "noreply@maliev.com",
      "FromName": "MALIEV Notifications"
    },
    "Twilio": {
      "AccountSid": "YOUR_TWILIO_ACCOUNT_SID",
      "AuthToken": "<your-token-here>",
      "PhoneNumber": "+15551234567"
    },
    "Slack": {
      "WebhookUrl": "YOUR_SLACK_WEBHOOK_URL"
    }
  },
  "RateLimiting": {
    "Line": {
      "TokenLimit": 1000,
      "ReplenishmentPeriod": "00:00:01",
      "TokensPerPeriod": 10
    },
    "WhatsApp": {
      "TokenLimit": 80,
      "ReplenishmentPeriod": "00:00:01",
      "TokensPerPeriod": 80
    },
    "SendGrid": {
      "TokenLimit": 100,
      "ReplenishmentPeriod": "00:00:01",
      "TokensPerPeriod": 100
    },
    "Twilio": {
      "TokenLimit": 100,
      "ReplenishmentPeriod": "00:00:01",
      "TokensPerPeriod": 100
    }
  }
}
```

**Note**: For local development, you can use mock values for external provider credentials. Actual credentials should be stored in Google Secret Manager for production.

### 4. Configure NuGet Package Source

Create `nuget.config` in repository root (if not exists):

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="github" value="https://nuget.pkg.github.com/maliev/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <github>
      <add key="Username" value="%NUGET_USERNAME%" />
      <add key="ClearTextPassword" value="%NUGET_PASSWORD%" />
    </github>
  </packageSourceCredentials>
</configuration>
```

Set environment variables for GitHub Packages access:

**Windows PowerShell**:
```powershell
$env:NUGET_USERNAME = "your-github-username"
$env:NUGET_PASSWORD = "<your-token-here>"
```

**Linux/macOS**:
```bash
export NUGET_USERNAME="your-github-username"
export NUGET_PASSWORD="<your-token-here>"
```

### 5. Restore Dependencies

```bash
cd src/Maliev.NotificationService.Api
dotnet restore
```

### 6. Apply Database Migrations

```bash
dotnet ef database update
```

Verify migration succeeded:

```bash
dotnet ef migrations list
```

### 7. Seed Default Data (Optional)

Run seed script to create default notification templates:

```bash
dotnet run --seed
```

---

## Running the Service

### Option 1: Run with .NET CLI

```bash
cd src/Maliev.NotificationService.Api
dotnet run
```

Service will start on `http://localhost:8080`

### Option 2: Run with Visual Studio

1. Open `Maliev.NotificationService.sln`
2. Set `Maliev.NotificationService.Api` as startup project
3. Press `F5` to start debugging

### Option 3: Run with Docker

Build image:

```bash
docker build -f Maliev.NotificationService.Api/Dockerfile -t maliev-notification-service:dev .
```

Run container:

```bash
docker run -d \
  --name notification-service \
  -p 8080:8080 \
  -e ConnectionStrings__NotificationDbContext="Host=host.docker.internal;Port=5432;Database=notification_db;Username=notification_user;Password=notification_pass" \
  -e ConnectionStrings__redis="host.docker.internal:6379" \
  -e ConnectionStrings__rabbitmq="amqp://notification_user:notification_pass@host.docker.internal:5672/maliev" \
  maliev-notification-service:dev
```

**Note**: Use `host.docker.internal` to access services running on host machine from Docker container.

### Verify Service is Running

Open browser and navigate to:

- **API Documentation**: `http://localhost:8080/notificationservice/scalar/v1`
- **Health Check**: `http://localhost:8080/notificationservice/health`
- **Liveness**: `http://localhost:8080/notificationservice/liveness`
- **Readiness**: `http://localhost:8080/notificationservice/readiness`
- **Metrics**: `http://localhost:8080/notificationservice/metrics`

---

## Testing

### Run Unit Tests

```bash
cd tests/Maliev.NotificationService.Api.Tests
dotnet test --filter Category=Unit
```

### Run Integration Tests

**Prerequisites**: Docker must be running (Testcontainers will start PostgreSQL, Redis, RabbitMQ automatically)

```bash
dotnet test --filter Category=Integration
```

### Run All Tests

```bash
dotnet test
```

### Generate Code Coverage Report

```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

---

## API Usage Examples

### 1. Create User Notification Preferences

**Request**:
```bash
curl -X POST http://localhost:8080/api/v1/preferences \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <your-token-here>" \
  -d '{
    "userId": "user123",
    "primaryChannelType": "line",
    "fallbackChannelTypes": ["email", "sms"],
    "optOutCategories": []
  }'
```

**Response** (201 Created):
```json
{
  "userId": "user123",
  "primaryChannelType": "line",
  "fallbackChannelTypes": ["email", "sms"],
  "optOutCategories": [],
  "createdAt": "2025-12-05T10:00:00Z",
  "updatedAt": "2025-12-05T10:00:00Z"
}
```

### 2. Create Channel Binding

**Request**:
```bash
curl -X POST http://localhost:8080/api/v1/channel-bindings \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <your-token-here>" \
  -d '{
    "userId": "user123",
    "channelType": "email",
    "channelIdentifier": "user@example.com"
  }'
```

**Response** (201 Created):
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "userId": "user123",
  "channelType": "email",
  "channelIdentifier": "u***@example.com",
  "isValid": true,
  "invalidatedAt": null,
  "invalidatedReason": null,
  "createdAt": "2025-12-05T10:01:00Z",
  "updatedAt": "2025-12-05T10:01:00Z"
}
```

### 3. Publish Notification Event to RabbitMQ

**Using RabbitMQ Management UI** (`http://localhost:15672`):

1. Navigate to "Exchanges" tab
2. Select `maliev.notifications` exchange
3. Expand "Publish message" section
4. Set routing key: `maliev.notification.v1.order-confirmed.critical`
5. Set payload:

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "source": "maliev.order.v1",
  "type": "order.confirmed",
  "time": "2025-12-05T10:30:00Z",
  "datacontenttype": "application/json",
  "specversion": "1.0",
  "data": {
    "notificationType": "OrderConfirmation",
    "priority": "critical",
    "targetUsers": [
      {
        "userId": "user123",
        "userType": "customer"
      }
    ],
    "templateId": "order-confirmed",
    "parameters": {
      "customerName": "John Doe",
      "orderNumber": "ORD-12345",
      "totalAmount": "1500.00 THB"
    },
    "metadata": {
      "language": "en",
      "source": "order-service"
    }
  }
}
```

6. Click "Publish message"

**Using .NET Client**:

```csharp
var bus = serviceProvider.GetRequiredService<IBus>();

var notificationEvent = new NotificationEvent
{
    Id = Guid.NewGuid(),
    Source = "maliev.order.v1",
    Type = "order.confirmed",
    Time = DateTimeOffset.UtcNow,
    DataContentType = "application/json",
    SpecVersion = "1.0",
    Data = new NotificationEventData
    {
        NotificationType = "OrderConfirmation",
        Priority = "critical",
        TargetUsers = new[]
        {
            new TargetUser { UserId = "user123", UserType = "customer" }
        },
        TemplateId = "order-confirmed",
        Parameters = new Dictionary<string, string>
        {
            ["customerName"] = "John Doe",
            ["orderNumber"] = "ORD-12345",
            ["totalAmount"] = "1500.00 THB"
        }
    }
};

await bus.Publish(notificationEvent);
```

### 4. Query Delivery Logs

**Request**:
```bash
curl "http://localhost:8080/api/v1/delivery-logs?userId=user123&startDate=2025-12-05T00:00:00Z&endDate=2025-12-06T00:00:00Z" \
  -H "Authorization: Bearer <your-token-here>"
```

**Response** (200 OK):
```json
{
  "items": [
    {
      "id": "660e8400-e29b-41d4-a716-446655440000",
      "eventId": "550e8400-e29b-41d4-a716-446655440000",
      "userId": "user123",
      "channelType": "line",
      "recipientIdentifier": "U***********1234",
      "status": "sent",
      "messageContent": "Hello John Doe, your order #ORD-12345...",
      "providerMessageId": "line_msg_abc123",
      "attemptNumber": 1,
      "deliveredAt": "2025-12-05T10:05:30Z",
      "createdAt": "2025-12-05T10:05:29Z"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1
}
```

---

### Viewing Logs

**Application logs**:
```bash
dotnet run --verbosity detailed
```

### Inspecting Data

**RabbitMQ Management UI**:
- Open `http://localhost:15672`
- Login: `notification_user` / `notification_pass`
- Navigate to "Queues" tab to view message counts
- View message details and re-queue if needed

---

## Next Steps

After completing local setup:

1. **Review Specification**: Read [spec.md](./spec.md) for full requirements
2. **Review Data Model**: Read [data-model.md](./data-model.md) for entity relationships
3. **Review API Contracts**: Explore OpenAPI specs in [contracts/](./contracts/)
4. **Run Tests**: Execute test suite to verify setup
5. **Start Development**: Pick tasks from [tasks.md](./tasks.md) (after `/speckit.tasks` command)

---

## Additional Resources

- **MALIEV Constitution**: [.specify/memory/constitution.md](../../.specify/memory/constitution.md)
- **Service Implementation Guidelines**: See `/speckit.plan` command arguments
- **Research Decisions**: [research.md](./research.md)
- **Implementation Plan**: [plan.md](./plan.md)

---

**Quick Start Version**: 1.0
**Last Updated**: 2025-12-05
**Maintained By**: MALIEV Development Team
