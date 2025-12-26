# Implementation Plan: Permission-Based Authorization Migration

**Branch**: `002-iam-integration` | **Date**: 2025-12-22 | **Spec**: [specs/002-iam-integration/spec.md](../spec.md)
**Input**: Feature specification from `specs/002-iam-integration/spec.md` and `notification-plan.md`

## Summary

Migrate the NotificationService to a granular permission-based authorization system aligned with GCP IAM standards. This involves defining 23 permissions and 4 predefined roles (`roles.notification.*`), and implementing automated registration via the `IAMRegistrationService` base class from ServiceDefaults. All controllers will be secured with `[RequirePermission]` attributes, supporting self-service data access and legacy token fallbacks.

## Technical Context

**Language/Version**: .NET 10  
**Primary Dependencies**: ASP.NET Core, `Maliev.Aspire.ServiceDefaults`, `System.Net.Http.Json`  
**Storage**: N/A (Logic-based and external IAM)  
**Testing**: xUnit, Testcontainers (PostgreSQL/RabbitMQ as required by Constitution IV)  
**Target Platform**: Docker (ASP.NET 10)
**Project Type**: .NET Microservice (Flat structure)  
**Performance Goals**: Permissions registered within 30s of startup; Auth check latency < 10ms.  
**Constraints**: 5-minute max revocation latency; Fail-safe startup with background IAM registration retries.  
**Scale/Scope**: 23 permissions, 4 roles across 4 controllers. Service identifier: `notification`.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Note |
|-----------|--------|------|
| I. Service Autonomy | ✅ | Interactions with IAM are via API. |
| III. Test-First | ✅ | Integration tests for permissions mandatory. |
| IV. Real Infrastructure | ✅ | Using Testcontainers for dependencies. |
| IX. Clean Artifacts | ✅ | No additional root markdown files. |
| X. Docker Best Practices | ✅ | Dockerfile in API folder; using net10.0 images. |
| XIII. Aspire Integration | ✅ | Calling `AddServiceDefaults()` and extending `IAMRegistrationService`. |
| XIV. Code Quality | ✅ | NO AutoMapper, FluentValidation, or FluentAssertions. |
| XV. Flat Structure | ✅ | Projects are at the root level. |

## Project Structure

### Documentation (this feature)

```text
specs/002-iam-integration/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
└── tasks.md             # Phase 2 output (to be created)
```

### Source Code (repository root)

```text
Maliev.NotificationService.Api/
├── Authorization/
│   ├── NotificationPermissions.cs
│   └── NotificationIAMRegistration.cs (extends IAMRegistrationService)
├── Controllers/
│   ├── TemplatesController.cs
│   ├── ChannelBindingsController.cs
│   ├── PreferencesController.cs
│   └── DeliveryLogsController.cs
└── Program.cs

Maliev.NotificationService.Tests/
├── Integration/
│   └── AuthorizationTests.cs
```

**Structure Decision**: Single project API following the flat structure mandate (Constitution XV).

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

*No violations identified.*
