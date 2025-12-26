# Research: Permission-Based Authorization Migration

## Decisions & Findings

### Decision 1: IAM Registration Payload
**Decision**: Use the structured registration model provided in `notification-plan.md`.
**Rationale**: Aligns with existing patterns used across MALIEV services.
**Alternatives considered**: Manual registration via SQL (rejected as it violates Service Autonomy).

### Decision 2: Background Retry Strategy
**Decision**: Implement registration using a `BackgroundService` that retries every 30 seconds upon failure, up to a maximum duration or until success.
**Rationale**: Ensures the service can start even if IAM is momentarily down, but eventually reaches consistency.
**Alternatives considered**: Fail-fast on startup (rejected as per user clarification).

### Decision 3: Permission Enforcement
**Decision**: Use `[RequirePermission]` attribute from `Maliev.Aspire.ServiceDefaults`.
**Rationale**: Centralized authorization logic ensures consistency and reduces boilerplate in controllers.
**Alternatives considered**: Manual `User.HasPermission()` checks (too verbose).

## Dependencies Research

### IAM Service Integration
- **Endpoint**: `POST /api/v1/permissions/register`
- **Endpoint**: `POST /api/v1/roles/register`
- **Authentication**: Bearer token via `ExternalServices:IAM:ServiceAccountToken`.

### ServiceDefaults Integration
- Ensure `builder.AddServiceDefaults()` is called in `Program.cs` to enable the authorization middleware that handles `RequirePermission`.
