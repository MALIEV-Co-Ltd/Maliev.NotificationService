# Tasks: Permission-Based Authorization Migration

**Input**: Design documents from `specs/002-iam-integration/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Tests are included as per Constitution III (Test-First Development).

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic configuration

- [X] T001 [P] Configure `PermissionBasedAuthEnabled` feature flag in `Maliev.NotificationService.Api/appsettings.json`
- [X] T002 [P] Configure `ExternalServices:IAM` with standard `/iam/v1/` endpoints in `Maliev.NotificationService.Api/appsettings.json`
- [X] T003 [P] Ensure `builder.AddServiceDefaults()` and `builder.Services.AddIAMClient()` are present in `Maliev.NotificationService.Api/Program.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Define the core permissions and roles that all stories depend on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T004 [P] Create `NotificationPermissions.cs` defining 23 permissions in `Maliev.NotificationService.Api/Authorization/NotificationPermissions.cs`
- [X] T005 [P] Create `NotificationPredefinedRoles.cs` defining 4 GCP-style roles in `Maliev.NotificationService.Api/Authorization/NotificationPredefinedRoles.cs`

**Checkpoint**: Foundation ready - user story implementation can now begin

---

## Phase 3: User Story 2 - Automated Permission Registration (Priority: P1) 🎯 MVP

**Goal**: Automatically register permissions and roles with IAM on startup

**Independent Test**: Verify IAM registry contains the 23 permissions and 4 roles after service startup

### Tests for User Story 2

- [X] T006 [US2] Create integration test for automated registration in `Maliev.NotificationService.Tests/Integration/AuthorizationTests.cs`

### Implementation for User Story 2

- [X] T007 [P] [US2] Implement `NotificationIAMRegistration` extending `IAMRegistrationService` in `Maliev.NotificationService.Api/Authorization/NotificationIAMRegistration.cs`
- [X] T008 [US2] Register `NotificationIAMRegistration` as a hosted service in `Maliev.NotificationService.Api/Program.cs`

**Checkpoint**: User Story 2 functional - Permissions are registered with IAM on startup

---

## Phase 4: User Story 1 - Secure Self-Service Access (Priority: P1)

**Goal**: Allow users to manage own preferences/bindings while preventing unauthorized access to others

**Independent Test**: Alice can read her preferences; Alice receives 403 when reading Bob's preferences

### Tests for User Story 1

- [X] T009 [US1] Create integration test for self-service access and IDOR prevention in `Maliev.NotificationService.Tests/Integration/AuthorizationTests.cs`

### Implementation for User Story 1

- [X] T010 [P] [US1] Update `ChannelBindingsController.cs` with self-service logic and `[RequirePermission]` in `Maliev.NotificationService.Api/Controllers/ChannelBindingsController.cs`
- [X] T011 [P] [US1] Update `PreferencesController.cs` with self-service logic and `[RequirePermission]` in `Maliev.NotificationService.Api/Controllers/PreferencesController.cs`

**Checkpoint**: User Story 1 functional - Users can securely manage their own data

---

## Phase 5: User Story 3 - Authorized Management Operations (Priority: P2)

**Goal**: Protect template management and global logs with specific permissions

**Independent Test**: Manager can create templates; User receives 403 when creating templates

### Tests for User Story 3

- [X] T012 [US3] Create integration test for template and log management permissions in `Maliev.NotificationService.Tests/Integration/AuthorizationTests.cs`

### Implementation for User Story 3

- [X] T013 [P] [US3] Add `[RequirePermission]` to all `TemplatesController.cs` endpoints in `Maliev.NotificationService.Api/Controllers/TemplatesController.cs`
- [X] T014 [P] [US3] Add `[RequirePermission]` to all `DeliveryLogsController.cs` endpoints in `Maliev.NotificationService.Api/Controllers/DeliveryLogsController.cs`

**Checkpoint**: User Story 3 functional - Administrative operations are protected

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Logging, documentation, and final validation

- [X] T015 [P] Implement `LegacyTokenFallbackMiddleware` to map tokens without claims to `roles.notification.user` role in `Maliev.NotificationService.Api/Middleware/LegacyTokenFallbackMiddleware.cs`
- [X] T016 [P] Add structured logging with 'Security' metadata for access denials in `Maliev.NotificationService.Api/Middleware/ExceptionHandlingMiddleware.cs`
- [X] T017 [P] Implement business metrics for authorization (e.g., `notification_auth_denials_total`) in `Maliev.NotificationService.Api/Metrics/NotificationMetrics.cs`
- [X] T018 [US2] Add integration test to verify permission revocation enforcement window (SC-006) in `Maliev.NotificationService.Tests/Integration/AuthorizationTests.cs`
- [X] T019 Update `README.md` with authorization documentation
- [X] T020 Final validation against `quickstart.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies.
- **Foundational (Phase 2)**: Depends on Setup completion.
- **User Story 2 (Phase 3)**: Depends on Foundational completion.
- **User Story 1 (Phase 4)**: Depends on Foundational completion.
- **User Story 3 (Phase 5)**: Depends on Foundational completion.
- **Polish (Phase 6)**: Depends on all user stories.

### User Story Dependencies

- **US2 (P1)**: Independent after Phase 2.
- **US1 (P1)**: Independent after Phase 2.
- **US3 (P2)**: Independent after Phase 2.

### Parallel Opportunities

- T001, T002, T003 can run in parallel.
- T004, T005 can run in parallel.
- Once Phase 2 is complete, US1, US2, and US3 implementation can proceed in parallel.
- T010, T011 can run in parallel.
- T013, T014 can run in parallel.

---

## Implementation Strategy

### MVP First (User Story 1 & 2)

1. Complete Setup and Foundational.
2. Complete US2 (Permission Registration) - Essential for IAM synchronization.
3. Complete US1 (Self-Service) - Core user value.

### Incremental Delivery

1. Setup + Foundation.
2. US2 (Registration) -> Verify with IAM.
3. US1 (Self-Service) -> Test Alice/Bob scenarios.
4. US3 (Admin ops) -> Test Manager/User scenarios.
5. Polish (Logs, documentation).