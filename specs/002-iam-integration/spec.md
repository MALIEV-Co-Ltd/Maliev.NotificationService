# Feature Specification: Permission-Based Authorization Migration

**Feature Branch**: `002-iam-integration`  
**Created**: 2025-12-22  
**Status**: Draft  
**Input**: User description: "Migration of NotificationService to a permission-based auth system including 23 permissions and 4 predefined roles as specified in notification-specify.md"

## Clarifications

### Session 2025-12-22
- Q: What is the maximum acceptable delay between a permission change in IAM and its enforcement? → A: 5 minutes
- Q: How should the system handle audit logs for permission-based access denials? → A: Integrated Structured Logging (Security metadata in standard logs)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Secure Self-Service Access (Priority: P1)

As a Notification Service user, I want to manage my own notification preferences and channel bindings while being prevented from accessing or modifying other users' data, so that my personal information remains private and secure.

**Why this priority**: Core privacy requirement. Preventing Insecure Direct Object Reference (IDOR) is a critical security baseline for the service.

**Independent Test**: An authenticated user attempts to read their own preferences (succeeds) and then attempts to read another user's preferences (fails with 403 Forbidden).

**Acceptance Scenarios**:

1. **Given** an authenticated user "Alice", **When** she requests her own channel bindings, **Then** the system returns her bindings.
2. **Given** an authenticated user "Alice", **When** she attempts to update "Bob's" notification preferences, **Then** the system denies access with a Forbidden error.

---

### User Story 2 - Automated Permission Registration (Priority: P1)

As a System Administrator, I want the Notification Service to automatically register its required permissions and predefined roles with the Identity and Access Management (IAM) service on startup, so that the authorization infrastructure is always synchronized with the service's needs.

**Why this priority**: Essential foundation for the entire feature. Without registration, the IAM service won't recognize the permissions required by the Notification Service.

**Independent Test**: Start the service and verify that the IAM service's registry contains the 23 specified permissions and 4 predefined roles.

**Acceptance Scenarios**:

1. **Given** a fresh installation of Notification Service, **When** the service starts up, **Then** it sends a registration request to the IAM service containing all 23 permissions.
2. **Given** the service is running, **When** the IAM configuration is checked, **Then** the 'roles.notification.admin', 'roles.notification.manager', 'roles.notification.sender', and 'roles.notification.user' roles are correctly defined.

---

### User Story 3 - Authorized Management Operations (Priority: P2)

As a Notification Manager, I want to manage notification templates and view delivery logs across the entire system, while users without this role are restricted from these sensitive operations, so that system configuration and audit trails are protected.

**Why this priority**: Protects business logic and system integrity. Templates define the communication patterns and logs contain sensitive delivery history.

**Independent Test**: A user with the 'roles.notification.manager' role creates a template (succeeds); a user with only the 'roles.notification.user' role attempts the same (fails).

**Acceptance Scenarios**:

1. **Given** a user with the `notification.templates.create` permission, **When** they submit a new template, **Then** the template is created successfully.
2. **Given** a user without `notification.logs.read` permission, **When** they attempt to view global delivery logs, **Then** the system denies access.

---

### Edge Cases

- **IAM Service Offline**: The system will start and attempt registration. If the IAM service is unavailable, it will log a warning and periodically retry registration in the background until successful.
- **Token Cache Inconsistency**: Permission changes in the IAM service MUST be reflected in the Notification Service within a maximum of 5 minutes.
- **Migration Phase Tokens**: Tokens without the new permission claims will fall back to a default `roles.notification.user` role for a transition period to ensure service continuity for existing sessions.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST define 23 granular permissions spanning Templates, Notifications, Bindings, Preferences, Logs, and System operations.
- **FR-002**: System MUST implement 4 predefined roles (`roles.notification.admin`, `roles.notification.manager`, `roles.notification.sender`, `roles.notification.user`) following GCP role identifier standards.
- **FR-003**: System MUST automatically register all permissions and roles with the external IAM service upon startup using the standard `IAMRegistrationService` base class.
- **FR-004**: System MUST enforce permission-based access control on all Template management endpoints.
- **FR-005**: System MUST enforce permission-based access control on all global Log and System configuration endpoints.
- **FR-006**: System MUST allow users to access their OWN preferences, bindings, and logs without requiring administrative permissions (Self-service rule).
- **FR-007**: System MUST provide a configuration-driven feature flag `PermissionBasedAuthEnabled` to toggle the enforcement logic.
- **FR-008**: System MUST integrate with the external IAM service using the standardized `/iam/v1/` registration endpoints.
- **FR-009**: System MUST log all permission-based access denials using structured logging with security-related metadata for audit purposes.

### Key Entities *(include if feature involves data)*

- **NotificationPermission**: A unique identifier for a specific operation (e.g., `notification.templates.create`).
- **NotificationRole**: A GCP-style role identifier (e.g., `roles.notification.manager`).
- **IAM Registry**: The external system of record for permissions and roles.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of the 23 defined permissions are successfully registered in the IAM service within 30 seconds of service startup.
- **SC-002**: All administrative endpoints return a `403 Forbidden` status code when accessed by a user lacking the required permissions.
- **SC-003**: 100% of existing integration tests pass with the `PermissionBasedAuthEnabled` flag enabled.
- **SC-004**: Self-service operations (accessing own data) have a 0% failure rate for validly authenticated users.
- **SC-005**: Registration process successfully retries or reports failure without crashing the main service loop.
- **SC-006**: Permission revocation is enforced across all instances within 5 minutes of the change in IAM.
- **SC-007**: 100% of access denials generate a corresponding structured log entry with 'Security' metadata.
