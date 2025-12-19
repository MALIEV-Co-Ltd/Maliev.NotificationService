# Data Model: Notification Service

**Feature**: Notification Service
**Date**: 2025-12-05
**Status**: Design Complete

## Overview

This document defines the database schema, entity relationships, and data validation rules for the Notification Service. All entities are stored in PostgreSQL with encryption at rest for sensitive data (channel bindings).

---

## Entity Relationship Diagram

```text
┌──────────────────────────────┐
│   UserNotificationPreference │
│   ────────────────────────── │
│   UserId (PK)                │
│   PrimaryChannelType         │
│   FallbackChannelTypes (JSON)│
│   OptOutCategories (JSON)    │
│   CreatedAt                  │
│   UpdatedAt                  │
└──────────┬───────────────────┘
           │
           │ 1:N
           │
           ▼
┌──────────────────────────────┐
│      ChannelBinding          │
│   ────────────────────────── │
│   Id (PK)                    │
│   UserId (FK) ───────────────┼──► User Service (external)
│   ChannelType                │
│   ChannelIdentifier (encrypted)│
│   IsValid                    │
│   InvalidatedAt              │
│   InvalidatedReason          │
│   CreatedAt                  │
│   UpdatedAt                  │
└──────────────────────────────┘

┌──────────────────────────────┐
│     NotificationTemplate     │
│   ────────────────────────── │
│   Id (PK)                    │
│   TemplateKey (unique)       │
│   Version                    │
│   Language                   │
│   ChannelType                │
│   ContentTemplate            │
│   Parameters (JSON)          │
│   CreatedAt                  │
│   CreatedBy                  │
└──────────────────────────────┘

┌──────────────────────────────┐
│        DeliveryLog           │
│   ────────────────────────── │
│   Id (PK)                    │
│   EventId (indexed)          │
│   UserId (indexed)           │
│   ChannelType                │
│   RecipientIdentifier        │
│   Status (enum)              │
│   MessageContent (truncated) │
│   ProviderResponse           │
│   ProviderMessageId          │
│   AttemptNumber              │
│   DeliveredAt                │
│   CreatedAt                  │
└──────────────────────────────┘

┌──────────────────────────────┐
│      RetryQueueEntry         │
│   ────────────────────────── │
│   Id (PK)                    │
│   EventId (indexed)          │
│   EventPayload (JSON)        │
│   AttemptNumber              │
│   ScheduledTime (indexed)    │
│   LastError                  │
│   CreatedAt                  │
└──────────────────────────────┘

┌──────────────────────────────┐
│      DeadLetterRecord        │
│   ────────────────────────── │
│   Id (PK)                    │
│   EventId (indexed)          │
│   EventPayload (JSON)        │
│   FailureReasons (JSON array)│
│   TotalAttempts              │
│   EscalationStatus           │
│   EscalatedAt                │
│   ResolvedAt                 │
│   ResolvedBy                 │
│   CreatedAt                  │
└──────────────────────────────┘
```

---

## Entity Definitions

### 1. UserNotificationPreference

Stores user-specific notification channel preferences and opt-out settings.

**Table**: `user_notification_preferences`

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| user_id | VARCHAR(100) | PRIMARY KEY | User identifier from User Service |
| primary_channel_type | VARCHAR(50) | NOT NULL | Primary notification channel (email, line, whatsapp, etc.) |
| fallback_channel_types | JSONB | NOT NULL DEFAULT '[]' | Ordered array of fallback channels |
| opt_out_categories | JSONB | NOT NULL DEFAULT '[]' | Notification categories user opted out of (e.g., ["marketing", "promotions"]) |
| created_at | TIMESTAMPTZ | NOT NULL DEFAULT NOW() | Record creation timestamp |
| updated_at | TIMESTAMPTZ | NOT NULL DEFAULT NOW() | Last update timestamp |

**Indexes**:
- PRIMARY KEY on `user_id`
- Index on `primary_channel_type` for analytics queries

**Validation Rules**:
- `primary_channel_type` must be one of: `email`, `line`, `whatsapp`, `sms`, `slack`, `facebook`, `instagram`
- `fallback_channel_types` must contain only valid channel types
- At least one channel must be configured (primary or fallback)

**Example JSON**:
```json
{
  "user_id": "user123",
  "primary_channel_type": "line",
  "fallback_channel_types": ["email", "sms"],
  "opt_out_categories": ["marketing"],
  "created_at": "2025-12-05T10:00:00Z",
  "updated_at": "2025-12-05T10:00:00Z"
}
```

---

### 2. ChannelBinding

Maps users to their channel-specific identifiers (LINE ID, email, phone number, etc.).

**Table**: `channel_bindings`

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY DEFAULT gen_random_uuid() | Unique binding identifier |
| user_id | VARCHAR(100) | NOT NULL | User identifier from User Service |
| channel_type | VARCHAR(50) | NOT NULL | Channel type (email, line, whatsapp, etc.) |
| channel_identifier | TEXT | NOT NULL | Encrypted channel-specific identifier (email address, LINE user ID, etc.) |
| is_valid | BOOLEAN | NOT NULL DEFAULT TRUE | Whether this binding is currently valid |
| invalidated_at | TIMESTAMPTZ | NULL | Timestamp when binding was invalidated |
| invalidated_reason | TEXT | NULL (MUST be NOT NULL when is_valid=false) | Reason for invalidation (e.g., "User blocked bot", "Invalid recipient") |
| created_at | TIMESTAMPTZ | NOT NULL DEFAULT NOW() | Record creation timestamp |
| updated_at | TIMESTAMPTZ | NOT NULL DEFAULT NOW() | Last update timestamp |

**Indexes**:
- PRIMARY KEY on `id`
- UNIQUE index on `(user_id, channel_type)` - one binding per user per channel
- Index on `user_id` for user lookups
- Index on `is_valid` for filtering active bindings

**Validation Rules**:
- `channel_type` must be valid channel enum
- `channel_identifier` validated per channel type:
  - `email`: Valid email format
  - `sms`/`whatsapp`: Valid E.164 phone number format
  - `line`: LINE user ID format (U[0-9a-f]{32})
  - `facebook`/`instagram`: PSID format
  - `slack`: Slack user ID format
- `invalidated_reason` MUST be NOT NULL when `is_valid=false` (enforced by application logic and database check constraint)
- `invalidated_at` MUST be NOT NULL when `is_valid=false` (enforced by application logic)

**Encryption**:
- `channel_identifier` encrypted at rest using PostgreSQL pgcrypto or application-level encryption
- Encryption key stored in Google Secret Manager

**State Transitions**:
```
[Created] → is_valid=true
    ↓ (permanent failure detected)
[Invalidated] → is_valid=false, invalidated_at=NOW(), invalidated_reason set
    ↓ (user re-subscribes)
[Revalidated] → is_valid=true, invalidated_at=NULL, invalidated_reason=NULL
```

---

### 3. NotificationTemplate

Reusable message templates with parameter placeholders and multilingual support.

**Table**: `notification_templates`

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY DEFAULT gen_random_uuid() | Unique template identifier |
| template_key | VARCHAR(200) | NOT NULL | Human-readable template identifier (e.g., "order-confirmed") |
| version | INTEGER | NOT NULL DEFAULT 1 | Template version for backward compatibility |
| language | VARCHAR(10) | NOT NULL DEFAULT 'en' | Language code (ISO 639-1) |
| channel_type | VARCHAR(50) | NULL | Channel-specific template (null = all channels) |
| content_template | TEXT | NOT NULL | Template content with {{parameter}} placeholders |
| parameters | JSONB | NOT NULL DEFAULT '[]' | Array of required parameter names |
| created_at | TIMESTAMPTZ | NOT NULL DEFAULT NOW() | Record creation timestamp |
| created_by | VARCHAR(100) | NOT NULL | User/system that created the template |

**Indexes**:
- PRIMARY KEY on `id`
- UNIQUE index on `(template_key, version, language, channel_type)` - unique template per version/language/channel combo
- Index on `template_key` for lookups
- Index on `language` for multilingual queries

**Validation Rules**:
- `template_key` must be kebab-case (e.g., "order-confirmed", "payment-failed"), matching pattern `^[a-z0-9-]+$`
- `language` must be valid ISO 639-1 code (2-letter lowercase)
- `channel_type` must be valid channel enum or NULL
- `parameters` array must contain only valid parameter names matching pattern `^[a-zA-Z][a-zA-Z0-9]*$` (alphanumeric starting with letter, camelCase recommended)
- `content_template` must contain all parameters listed in `parameters` array using {{parameterName}} syntax
- Parameter names in `content_template` must exactly match entries in `parameters` array (case-sensitive)

**Versioning Strategy**:
- Increment `version` for breaking changes (different parameters, major content changes)
- Keep old versions for backward compatibility with in-flight events

**Example**:
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "template_key": "order-confirmed",
  "version": 1,
  "language": "th",
  "channel_type": "line",
  "content_template": "สวัสดีค่ะ {{customerName}} 🎉\n\nคำสั่งซื้อ #{{orderNumber}} ของคุณได้รับการยืนยันแล้ว\nยอดรวม: {{totalAmount}}\n\nขอบคุณที่ใช้บริการค่ะ!",
  "parameters": ["customerName", "orderNumber", "totalAmount"],
  "created_at": "2025-12-05T10:00:00Z",
  "created_by": "admin@maliev.com"
}
```

---

### 4. DeliveryLog

Audit trail of all notification delivery attempts.

**Table**: `delivery_logs`

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY DEFAULT gen_random_uuid() | Unique log entry identifier |
| event_id | VARCHAR(100) | NOT NULL | Original notification event ID |
| user_id | VARCHAR(100) | NOT NULL | Target user ID |
| channel_type | VARCHAR(50) | NOT NULL | Channel used for delivery |
| recipient_identifier | VARCHAR(500) | NOT NULL | Obfuscated recipient identifier (for audit) |
| status | VARCHAR(50) | NOT NULL | Delivery status (sent, failed, pending, rate_limited) |
| message_content | TEXT | NULL | Truncated message content (first 500 chars) |
| provider_response | TEXT | NULL | Full provider API response |
| provider_message_id | VARCHAR(200) | NULL | Provider's message tracking ID |
| attempt_number | INTEGER | NOT NULL DEFAULT 1 | Attempt number (1-4 for critical, 1-2 for non-critical) |
| delivered_at | TIMESTAMPTZ | NULL | Timestamp of successful delivery |
| created_at | TIMESTAMPTZ | NOT NULL DEFAULT NOW() | Log entry creation timestamp |

**Indexes**:
- PRIMARY KEY on `id`
- Index on `event_id` for event-based queries
- Index on `user_id` for user-based queries
- Index on `created_at` for time-range queries (partition key for time-series data)
- Index on `status` for filtering by delivery status

**Data Retention**:
- Retain for 90 days per NFR-004
- Implement automatic archival/deletion via scheduled job

**Privacy Considerations**:
- `recipient_identifier` stored in obfuscated form (e.g., "j***@example.com", "+66****1234")
- `message_content` truncated to 500 characters to avoid storing full sensitive content

**Status Enum**:
- `sent`: Successfully sent to provider
- `delivered`: Provider confirmed delivery (if supported)
- `failed`: Delivery failed (transient or permanent)
- `pending`: Queued for delivery
- `rate_limited`: Delayed due to rate limiting

**Example**:
```json
{
  "id": "660e8400-e29b-41d4-a716-446655440000",
  "event_id": "evt_order_12345",
  "user_id": "user123",
  "channel_type": "line",
  "recipient_identifier": "U***********1234",
  "status": "sent",
  "message_content": "Hello John Doe, your order #ORD-12345...",
  "provider_response": "{\"message_id\": \"line_msg_abc123\"}",
  "provider_message_id": "line_msg_abc123",
  "attempt_number": 1,
  "delivered_at": "2025-12-05T10:05:30Z",
  "created_at": "2025-12-05T10:05:29Z"
}
```

---

### 5. RetryQueueEntry

Tracks failed notifications pending retry.

**Table**: `retry_queue_entries`

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY DEFAULT gen_random_uuid() | Unique retry entry identifier |
| event_id | VARCHAR(100) | NOT NULL | Original notification event ID |
| event_payload | JSONB | NOT NULL | Full event payload for retry |
| attempt_number | INTEGER | NOT NULL | Current attempt number (1-3 for critical) |
| scheduled_time | TIMESTAMPTZ | NOT NULL | When to retry this notification |
| last_error | TEXT | NULL | Last error message from failed attempt |
| created_at | TIMESTAMPTZ | NOT NULL DEFAULT NOW() | Entry creation timestamp |

**Indexes**:
- PRIMARY KEY on `id`
- Index on `event_id` for deduplication
- Index on `scheduled_time` for picking due retries (most important!)

**Lifecycle**:
1. Created when delivery fails with retryable error
2. Scheduled for retry after exponential backoff delay
3. Picked up by retry processor when `scheduled_time <= NOW()`
4. Deleted when successfully retried OR moved to dead-letter after max attempts

**Cleanup Strategy**:
- Delete entries after successful retry
- Delete entries older than 24 hours (stale entries)
- Move to dead-letter queue after 3 failed attempts

---

### 6. DeadLetterRecord

Records notifications that failed all retry attempts.

**Table**: `dead_letter_records`

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY DEFAULT gen_random_uuid() | Unique dead-letter identifier |
| event_id | VARCHAR(100) | NOT NULL | Original notification event ID |
| event_payload | JSONB | NOT NULL | Full event payload for manual retry |
| failure_reasons | JSONB | NOT NULL | Array of error messages from all attempts |
| total_attempts | INTEGER | NOT NULL | Total number of delivery attempts |
| escalation_status | VARCHAR(50) | NOT NULL DEFAULT 'pending' | Escalation status (pending, escalated, resolved) |
| escalated_at | TIMESTAMPTZ | NULL | When escalated to support team |
| resolved_at | TIMESTAMPTZ | NULL | When manually resolved |
| resolved_by | VARCHAR(100) | NULL | User who resolved the issue |
| created_at | TIMESTAMPTZ | NOT NULL DEFAULT NOW() | Record creation timestamp |

**Indexes**:
- PRIMARY KEY on `id`
- Index on `event_id`
- Index on `escalation_status` for support dashboard queries
- Index on `created_at` for time-based filtering

**Escalation Status Enum**:
- `pending`: Awaiting manual review
- `escalated`: Sent to support team for investigation
- `resolved`: Issue resolved, notification re-sent or dismissed

**Support Workflow**:
1. Dead-letter created when retries exhausted
2. Monitoring alert triggered
3. Support team queries dead-letter API
4. Manual investigation and resolution
5. Status updated to `resolved`, `resolved_by` set

**Example**:
```json
{
  "id": "770e8400-e29b-41d4-a716-446655440000",
  "event_id": "evt_payment_fail_67890",
  "event_payload": { ... },
  "failure_reasons": [
    "Attempt 1: LINE API returned 400: Invalid user ID",
    "Attempt 2: LINE API returned 400: Invalid user ID",
    "Attempt 3: LINE API returned 400: Invalid user ID"
  ],
  "total_attempts": 3,
  "escalation_status": "pending",
  "escalated_at": null,
  "resolved_at": null,
  "resolved_by": null,
  "created_at": "2025-12-05T10:10:00Z"
}
```

---

## Database Migrations Strategy

### Initial Migration (001_CreateInitialSchema.cs)
- Create all tables with indexes
- Set up pgcrypto extension for encryption
- Create ENUM types for status fields

### Migration Naming Convention
- `{number}_{Description}.cs` (e.g., `002_AddTemplateVersioning.cs`)
- Use EF Core migrations: `dotnet ef migrations add {Name}`

### Data Seeding
- Seed default templates for core notification types:
  - Order confirmed
  - Payment failed
  - System outage alert
- Seed default channel type enums

---

## Performance Considerations

### Partitioning Strategy
- **delivery_logs**: Partition by `created_at` (monthly partitions)
  - Improves query performance for time-range analytics
  - Facilitates 90-day retention cleanup

### Query Optimization
- Use covering indexes for common query patterns:
  - Get user preferences: `(user_id)`
  - Get active bindings: `(user_id, is_valid)`
  - Get templates by key: `(template_key, language)`
  - Get due retries: `(scheduled_time) WHERE scheduled_time <= NOW()`

### Connection Pooling
- Configure PostgreSQL connection pool via ServiceDefaults
- Default: Min=10, Max=100 connections
- Monitor connection usage with metrics

---

## Data Validation Rules Summary

| Entity | Validation Rules |
|--------|-----------------|
| UserNotificationPreference | Valid channel types, at least one channel configured |
| ChannelBinding | Channel-specific identifier format validation, unique per user+channel |
| NotificationTemplate | Valid template_key format, all parameters present in content |
| DeliveryLog | Valid status enum, recipient_identifier obfuscated |
| RetryQueueEntry | scheduled_time in future, attempt_number ≤ 3 |
| DeadLetterRecord | Valid escalation_status enum, total_attempts > 0 |

---

## Next Steps

1. ✅ Data model defined
2. ⏭️ Generate EF Core entity classes
3. ⏭️ Generate initial migration
4. ⏭️ Create seed data for default templates

---

**Data Model Version**: 1.0
**Last Updated**: 2025-12-05
**Reviewed By**: Planning Agent
