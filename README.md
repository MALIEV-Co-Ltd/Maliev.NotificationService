# Maliev Notification Service

[![Build Status](https://img.shields.io/badge/Build-Passing-success)](https://github.com/ORGANIZATION/Maliev.NotificationService)
[![.NET Version](https://img.shields.io/badge/.NET-10.0-blue)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![Database](https://img.shields.io/badge/Database-PostgreSQL%2018-blue)](https://www.postgresql.org/)

Unified multi-channel notification delivery platform for the Maliev ecosystem.

**Role in MALIEV Architecture**: The central communication hub for the entire platform. it reliably delivers high-priority business alerts and marketing messages across Email, LINE, WhatsApp, and SMS, ensuring stakeholders remain informed through their preferred channels.

---

## 🏗️ Architecture & Tech Stack

- **Framework**: ASP.NET Core 10.0 (C# 13)
- **Database**: PostgreSQL 18 with Entity Framework Core 10.x
- **Distributed Cache**: Redis 7.x (Message deduplication & preference caching)
- **Messaging**: RabbitMQ via MassTransit (Guaranteed delivery queues)
- **Providers**: Extensible adapter architecture (SendGrid, LINE, WhatsApp, Twilio)
- **API Documentation**: OpenAPI 3.1 + Scalar UI
- **Observability**: OpenTelemetry (Metrics, Traces, Logging)

---

## ⚖️ Constitution Rules

This service strictly adheres to the platform development mandates:

### Banned Libraries
To maintain high performance and low complexity, the following are **NOT** used:
- ❌ **AutoMapper**: Explicit manual mapping only.
- ❌ **FluentValidation**: Standard Data Annotations (`[Required]`, `[EmailAddress]`) only.
- ❌ **FluentAssertions**: Standard xUnit `Assert` methods only.
- ❌ **In-memory Test DB**: All integration tests use **Testcontainers** with real PostgreSQL 18.

### Mandatory Practices
- ✅ **TreatWarningsAsErrors**: Enabled in all `.csproj` files.
- ✅ **XML Documentation**: Required on all public methods and properties.
- ✅ **No Secrets in Code**: All sensitive configuration injected via environment variables.
- ✅ **No Test Config in Program.cs**: Test configuration in test fixtures only.
- ✅ **IAM Integration**: Self-registers permissions with the IAM Service using GCP-style naming: `{service}.{resource}.{action}`.

---

## ✨ Key Features

- **Guaranteed Multi-Channel Delivery**: Extensible provider system supporting Email, LINE, WhatsApp, SMS, and Slack with automatic failover.
- **Priority-Based Routing**: Intelligent queuing that separates critical system alerts from standard marketing notifications.
- **Dynamic Template Engine**: Centralized multi-language template management with Mustache-style parameter substitution.
- **Deduplication Logic**: High-performance Redis-based detection to prevent redundant notification delivery.
- **Granular User Preferences**: Empowerment of users to configure their primary and fallback channels and opt-out categories.

---

## 🚀 Quick Start

### Prerequisites
- .NET 10.0 SDK
- Docker Desktop (for infrastructure)
- PostgreSQL 18 (Alpine)

### Local Development Setup

1. **Clone the repository**
```bash
git clone https://github.com/ORGANIZATION/Maliev.NotificationService.git
cd Maliev.NotificationService
```

2. **Spin up Infrastructure**
```bash
docker run --name notification-db -e POSTGRES_PASSWORD=YOUR_PASSWORD -p 5432:5432 -d postgres:18-alpine
docker run --name notification-redis -p 6379:6379 -d redis:7-alpine
```

3. **Configure Environment**
```powershell
# Windows PowerShell
$env:ConnectionStrings__NotificationDbContext="YOUR_POSTGRES_CONNECTION_STRING"
$env:ConnectionStrings__Cache="YOUR_REDIS_CONNECTION_STRING"
```

4. **Configure Secrets (Local Development)**
The Notification Service requires several API tokens for its delivery providers. These should be managed via [.NET User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) during local development.

Initialize and set the required placeholders:
```bash
cd Maliev.NotificationService.Api
dotnet user-secrets init

# Set actual values for the providers you intend to use:
dotnet user-secrets set "ExternalProviders:Facebook:PageAccessToken" "YOUR_TOKEN"
dotnet user-secrets set "ExternalProviders:Instagram:PageAccessToken" "YOUR_TOKEN"
dotnet user-secrets set "Twilio:AccountSid" "YOUR_SID"
dotnet user-secrets set "Twilio:AuthToken" "YOUR_AUTH_TOKEN"
dotnet user-secrets set "Slack:BotToken" "YOUR_TOKEN"
dotnet user-secrets set "LINE:ChannelAccessToken" "YOUR_TOKEN"
dotnet user-secrets set "Brevo:ApiKey" "YOUR_API_KEY"
```

5. **Apply Migrations & Run**
```bash
dotnet ef database update --project Maliev.NotificationService.Api
dotnet run --project Maliev.NotificationService.Api
```

The service will be available at `http://localhost:5000/notifications`. Access the interactive documentation at `http://localhost:5000/notifications/scalar`.

---

## 📡 API Endpoints

All endpoints are prefixed with `/notifications/v1/`.

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/templates` | Create a multi-language notification template |
| GET | `/preferences/{userId}` | Retrieve user-specific channel settings |
| POST | `/channel-bindings` | Bind a user to a delivery channel (e.g., LINE ID) |
| GET | `/delivery-logs` | Audit trail of all sent notifications |

---

## 🏥 Health & Monitoring

Standardized health probes for Kubernetes orchestration:
- **Liveness**: `GET /notifications/liveness`
- **Readiness**: `GET /notifications/readiness` (Checks DB and Redis connectivity)
- **Metrics**: `GET /notifications/metrics` (Prometheus format)

---

## 🧪 Testing

We prioritize reliable tests over mock-heavy unit tests.

```bash
# Run all tests using Testcontainers
dotnet test --verbosity normal
```

- **Integration Tests**: Use real PostgreSQL 18 containers.
- **Contract Tests**: Ensure API stability for consumers.

---

## 📦 Deployment

Infrastructure management is handled via GitOps patterns.

- **Docker Image**: `REGION-docker.pkg.dev/PROJECT_ID/REPOSITORY/maliev-notification-service:{sha}`
- **Environments**: Development, Staging, Production

---

## 📄 License

Proprietary - © 2025 MALIEV Co., Ltd. All rights reserved.
