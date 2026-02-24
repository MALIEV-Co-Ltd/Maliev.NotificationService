# Feature Specification: Notification Service

**Feature Branch**: `001-notification-service`
**Created**: 2025-12-05
**Status**: Draft
**Input**: User description: "The Notification Service is responsible for delivering messages and alerts to users across all supported communication channels in the MALIEV microservices ecosystem. It acts as a centralized routing layer that abstracts away the complexities of multiple external messaging platforms (e.g., LINE Messaging API, Facebook Messenger API, Instagram DM API, Email SMTP, WhatsApp Business API, SMS gateways). Its core responsibility is to identify user-preferred channels, format messages per channel requirements, and deliver notifications reliably and consistently."

## Terminology

**Standardized Terms** (used consistently across all artifacts):

- **notificationType**: High-level category of notification (e.g., "OrderConfirmation", "PaymentFailure", "SystemAlert") - used in CloudEvents schema `data.notificationType` field
- **templateId** or **template_key**: Unique identifier for notification template (e.g., "order-confirmed", "payment-failed") in kebab-case format - used interchangeably, both refer to same concept
- **channelType**: Communication channel identifier (email, line, whatsapp, sms, slack, facebook, instagram) - used in ChannelType enum
- **priority**: Notification urgency level (critical, standard) - used in NotificationPriority enum, determines retry behavior
- **eventId**: Unique identifier for notification event (UUID v4) - used for deduplication and correlation

## Clarifications

### Session 2025-12-05

- Q: When a user has no configured notification preferences, what should the system do? → A: Send to default channels based on user type (customer=email, staff=email+Slack, admin=email+SMS)
- Q: For critical notifications, how many retry attempts should the system make before moving to dead-letter queue? → A: 3 retries with exponential backoff (1s, 2s, 4s)
- Q: When a channel provider returns a permanent failure (invalid/blocked recipient), what should the system do? → A: Mark channel binding as invalid, attempt fallback channel, log for user support follow-up
- Q: How should the system identify and handle duplicate notification events to ensure idempotency? → A: Use event ID + timestamp hash with 24-hour deduplication window
- Q: What key operational metrics should the system expose for monitoring delivery health? → A: Delivery success rate per channel, retry queue depth, average delivery latency, provider error rates, deduplication cache hit rate

## User Scenarios & Testing

### User Story 1 - Critical Business Notification Delivery (Priority: P1)

When a critical business event occurs (payment failure, system outage, order status change), the service automatically identifies affected users, resolves their preferred notification channels, and delivers urgent alerts through those channels with guaranteed delivery.

**Why this priority**: Core business operations depend on reliable notification delivery. Payment failures, order confirmations, and system outages directly impact revenue and customer satisfaction. This is the minimum viable product that delivers immediate business value.

**Independent Test**: Can be fully tested by triggering a payment failure event and verifying that the affected user receives a notification on their preferred channel (e.g., LINE or email) with delivery confirmation logged.

**Acceptance Scenarios**:

1. **Given** a payment failure event is published to RabbitMQ, **When** the service receives the event, **Then** the affected customer receives a notification on their primary preferred channel within 30 seconds
2. **Given** a user has LINE as primary channel and email as fallback, **When** LINE delivery fails, **Then** the notification is automatically sent via email
3. **Given** a system outage event occurs, **When** administrators are notified, **Then** all administrators receive SMS notifications within 1 minute
4. **Given** a notification delivery fails after all retries, **When** the failure is recorded, **Then** a dead-letter queue entry is created and monitoring alerts are triggered

---

### User Story 2 - User Channel Preference Management (Priority: P2)

Users can manage their notification preferences, including which channels they want to receive notifications on, the priority order of channels, and opt-in/opt-out settings for different notification types. The service maintains accurate, up-to-date records of user preferences and channel bindings.

**Why this priority**: User control over notifications reduces notification fatigue and improves engagement. This enables personalization but is secondary to basic delivery functionality. Can be tested independently by allowing users to update preferences and verifying those preferences are respected.

**Independent Test**: Can be fully tested by creating/updating a user's notification preferences through the API and verifying that subsequent notifications respect those preferences.

**Acceptance Scenarios**:

1. **Given** a customer has no notification preferences set, **When** they subscribe to LINE notifications, **Then** their LINE user ID is stored and set as their primary notification channel
2. **Given** a user has multiple channels configured, **When** they update their preference order, **Then** future notifications use the new priority order
3. **Given** an internal staff member, **When** they opt out of marketing notifications, **Then** they no longer receive marketing-category notifications but still receive critical system alerts
4. **Given** a user unsubscribes from a channel, **When** a notification is triggered, **Then** the service skips that channel and uses the next available preference

---

### User Story 3 - Multi-Template Multi-Language Notification Formatting (Priority: P3)

The service formats notification content using templates that support parameter insertion, multiple languages, and channel-specific formatting requirements. Marketing teams can create templates once and have them automatically adapted for LINE rich content, email HTML, SMS plain text, and other channel requirements.

**Why this priority**: Improves scalability and maintainability of notification content, but basic notifications can initially use simple text formatting. This adds operational efficiency but isn't required for minimum viable delivery.

**Independent Test**: Can be fully tested by creating a notification template with parameters and multiple language variants, then triggering a notification and verifying correct parameter substitution and channel-specific formatting.

**Acceptance Scenarios**:

1. **Given** a notification template with parameters (customer name, order number), **When** a notification is triggered, **Then** the message includes the actual customer name and order number
2. **Given** a template exists in English and Thai, **When** a Thai-speaking user receives a notification, **Then** the message is delivered in Thai
3. **Given** a rich content template for LINE, **When** the same notification is sent via SMS, **Then** the content is automatically simplified to plain text
4. **Given** a template with special formatting, **When** delivered across multiple channels, **Then** each channel receives properly formatted content according to its capabilities

---

### User Story 4 - Best-Effort Marketing Notifications (Priority: P4)

For non-critical notifications like marketing messages, promotions, and newsletters, the service delivers notifications asynchronously with relaxed retry constraints. These notifications are queued and processed in batch mode to optimize resource usage and respect rate limits.

**Why this priority**: Supports business growth and customer engagement, but failure to deliver marketing content doesn't impact core operations. Can be added after critical notification infrastructure is stable.

**Independent Test**: Can be fully tested by submitting a batch of marketing notifications and verifying they are delivered within acceptable timeframes without impacting critical notification performance.

**Acceptance Scenarios**:

1. **Given** a marketing campaign event, **When** 10,000 customers are targeted, **Then** all notifications are queued and delivered within 24 hours
2. **Given** a promotional notification, **When** delivery to a user fails, **Then** the system attempts one retry and logs the failure without triggering alerts
3. **Given** rate limits on an external provider, **When** marketing notifications are being sent, **Then** the service throttles delivery to stay within provider limits
4. **Given** critical and marketing notifications in the queue, **When** processing messages, **Then** critical notifications are prioritized over marketing notifications

---

### User Story 5 - New Channel Provider Integration (Priority: P5)

Developers can integrate new communication channels (e.g., Telegram, Discord, Microsoft Teams) by implementing a standardized provider adapter interface without modifying core service logic. This allows the service to expand to new channels as business requirements evolve.

**Why this priority**: Enables long-term flexibility and future-proofing, but not required for initial operations with known channels. This architectural capability can be validated once core delivery functionality is proven.

**Independent Test**: Can be fully tested by implementing a test provider adapter for a mock channel and verifying that notifications are routed correctly through the new provider without changes to core service code.

**Acceptance Scenarios**:

1. **Given** a new provider adapter is implemented, **When** the adapter is registered with the service, **Then** the channel becomes available for notification routing
2. **Given** a user has configured the new channel, **When** a notification is triggered, **Then** the message is formatted and delivered through the new provider
3. **Given** channel-specific requirements (authentication, rate limits), **When** the adapter handles these, **Then** the core service remains unaware of provider-specific details
4. **Given** multiple providers, **When** one provider has an outage, **Then** other providers continue operating normally

---

### Edge Cases

- What happens when all of a user's preferred channels fail to deliver?
- How does the system handle malformed event payloads from other microservices?
- **When a user has no notification preferences configured**: System uses default channels based on user type (customers receive via email, staff via email+Slack, administrators via email+SMS).
- How does the system handle external provider rate limiting during high-volume events?
- **When a channel provider returns a permanent failure** (invalid recipient, blocked user): System marks the channel binding as invalid in the database, attempts delivery via fallback channel, and logs the event for user support follow-up.
- **When duplicate notification events arrive from RabbitMQ**: System generates a hash from event ID + timestamp and checks against a deduplication cache with 24-hour retention. Duplicate events within the window are silently discarded and logged.
- How does the system handle very large batches of notifications that exceed memory limits?
- What happens when template rendering fails due to missing parameters?
- How does the system handle time zone differences for scheduled notifications?

## Requirements

### Functional Requirements

- **FR-001**: System MUST consume and parse notification events from RabbitMQ queues published by other MALIEV microservices, extracting notification type, target users, content parameters, and priority level
- **FR-003**: System MUST resolve user notification preferences including preferred channels, fallback channels, and opt-out settings
- **FR-004**: System MUST apply default channels based on user type when no preferences are configured by creating multiple ChannelBinding entries: customers receive one binding (email), staff receive two bindings (email as primary, Slack as fallback), administrators receive two bindings (email as primary, SMS as fallback)
- **FR-005**: System MUST route notifications to the appropriate channel provider based on user preferences
- **FR-006**: System MUST format notification content according to channel-specific requirements (rich content vs. plain text)
- **FR-007**: System MUST support parameter substitution in notification templates using {{parameterName}} syntax
- **FR-007a**: System MUST fail template rendering gracefully when required parameters are missing from event data, route to dead-letter queue, and log descriptive error with missing parameter names
- **FR-008**: System MUST support multiple language variants for notification content
- **FR-009**: System MUST authenticate with external messaging providers using secure credential storage
- **FR-010**: System MUST implement priority-based retry logic differentiated by notification priority: critical notifications receive 3 retry attempts with exponential backoff intervals (1 second, 2 seconds, 4 seconds), non-critical notifications receive 1 retry attempt with fixed 5 second delay, all exhausted retries route to dead-letter queue
- **FR-011**: System MUST use fallback channels when primary channel delivery fails
- **FR-012**: System MUST detect permanent failures (invalid recipient, blocked user) and mark affected channel bindings as invalid in the database with InvalidatedAt timestamp and InvalidatedReason description
- **FR-013**: System MUST route failed messages to dead-letter queues after retry exhaustion
- **FR-015**: System MUST respect external provider rate limits using token bucket algorithm to avoid throttling, queue messages when rate limit approached, and emit rate_limited status in delivery logs with RetryAfter timestamp
- **FR-016**: System MUST store delivery logs including timestamp, status, provider response, and channel identifier
- **FR-017**: System MUST trigger monitoring alerts for critical notification delivery failures
- **FR-018**: System MUST provide API endpoints for managing user notification preferences
- **FR-019**: System MUST store user channel bindings (LINE user ID, Facebook PSID, email, phone number)
- **FR-020**: System MUST validate user identifiers for each channel type
- **FR-021**: System MUST expose operational metrics including delivery success rate per channel, retry queue depth, average delivery latency, provider error rates, and deduplication cache hit rate (measured as cache hits / total deduplication checks)
- **FR-022**: System MUST handle duplicate events idempotently using SHA256 hash of (event ID + timestamp) stored in Redis with 24-hour sliding TTL window from first event occurrence to prevent duplicate notifications
- **FR-023**: System MUST support horizontal scaling by maintaining stateless API operations (no session affinity required, in-memory caching permitted only for read-only data such as templates)
- **FR-024**: System MUST store notification templates with version control
- **FR-025**: System MUST allow new channel providers to be added via adapter pattern

### Non-Functional Requirements

- **NFR-001**: System MUST deliver critical notifications within 30 seconds of event receipt, measured as p95 latency from event timestamp in RabbitMQ message to DeliveryLog.CreatedAt timestamp for successful deliveries
- **NFR-002**: System MUST support at least 10,000 concurrent notification deliveries without performance degradation, measured by sustained throughput during load testing
- **NFR-003**: System MUST maintain 99.9% availability for critical notification processing, measured as (total uptime - downtime) / total uptime over rolling 30-day window, where uptime is defined by /health endpoint returning HTTP 200
- **NFR-004**: System MUST persist all delivery logs for at least 90 days, after which logs are permanently deleted (no archival to cold storage required)
- **NFR-005**: System MUST encrypt user channel binding data at rest
- **NFR-006**: System MUST operate in a horizontally scalable, containerized environment

### Key Entities

- **User Notification Preference**: Represents a user's configured notification settings including primary channel, fallback channels, priority order, and opt-in/opt-out status for notification categories. Links a user ID to their notification configuration.

- **Channel Binding**: Represents the connection between a user and a specific communication channel, storing platform-specific identifiers (LINE user ID, email address, phone number, etc.) required to deliver messages through that channel.

- **Notification Template**: Represents reusable message content with placeholders for dynamic parameters, supporting multiple languages and channel-specific formatting variants. Each template has a unique identifier and version.

- **Delivery Log**: Represents a record of a notification delivery attempt, including timestamp, target user, channel used, delivery status (success/failure/pending), provider response codes, and message content reference. Used for audit trails and troubleshooting.

- **Notification Event**: Represents an incoming message from other microservices containing notification type, target users, content parameters, priority level, and event metadata. This is the input that triggers notification delivery.

- **Channel Provider**: Represents an external messaging platform integration (LINE, WhatsApp, Email, SMS, etc.) with its own authentication credentials, rate limits, and formatting requirements. Each provider has a standardized adapter interface.

- **Retry Queue Entry**: Represents a failed notification that requires retry, storing the notification payload, retry count, next retry timestamp, and failure reason. Used for guaranteed delivery scenarios.

- **Dead Letter Record**: Represents a notification that failed after all retry attempts, storing the original event, all failure reasons, timestamps, and escalation status. Used for manual intervention and monitoring.

## Success Criteria

### Measurable Outcomes

- **SC-001**: Critical notifications are delivered to users within 30 seconds of event creation 95% of the time
- **SC-002**: System successfully delivers 99% of all notifications on the first attempt without fallback
- **SC-003**: System handles 10,000 concurrent notification deliveries without performance degradation
- **SC-004**: Failed critical notifications trigger monitoring alerts within 2 minutes of failure
- **SC-005**: Users can update their notification preferences through the API with changes reflected in the next notification
- **SC-006**: System maintains complete delivery logs for 90 days for audit and compliance purposes
- **SC-007**: New communication channel providers can be integrated without modifying existing service code
- **SC-008**: System maintains 99.9% uptime for notification processing during business hours
- **SC-009**: Non-critical marketing notifications are delivered within 24 hours of event creation
- **SC-010**: System correctly routes notifications to user-preferred channels 98% of the time based on stored preferences

## Assumptions

1. **Event Schema Standardization**: All microservices publishing notification events follow a common event schema standard for notification payloads. If not, an event adapter layer may be required.

2. **User Identity Management**: A centralized user identity service exists that provides consistent user IDs across the MALIEV ecosystem. The Notification Service uses these user IDs to look up preferences and channel bindings.

3. **External Provider Accounts**: MALIEV organization has pre-established accounts and API credentials for all supported external messaging providers (LINE, WhatsApp, Facebook, etc.).

4. **Message Persistence**: Notification delivery logs and user preferences are stored in a persistent database (relational or NoSQL) that is backed up regularly.

5. **RabbitMQ Infrastructure**: A production-grade RabbitMQ cluster is available with appropriate queues, exchanges, and dead-letter queue configurations.

6. **Language Detection**: User language preferences are either stored in user profiles or can be inferred from user context. The service does not perform automatic language detection from content.

7. **Template Management**: An administrative interface or API exists for creating and managing notification templates, separate from the core delivery functionality.

8. **Monitoring Infrastructure**: A monitoring and alerting system (e.g., Prometheus, Grafana, or similar) is available to consume metrics and send alerts.

9. **Rate Limit Configuration**: External provider rate limits are known and can be configured per provider. The service respects these limits to avoid service disruption.

10. **Network Reliability**: The service operates in an environment with reliable network connectivity to external messaging providers. Transient network failures are handled by retry logic.

11. **Channel Subscription Process**: User channel subscriptions (e.g., adding a LINE user ID) are handled by other services or user-facing applications. The Notification Service provides APIs to store these bindings but does not handle the OAuth/authentication flows with external providers.

12. **Content Compliance**: Notification content provided by other microservices complies with legal and regulatory requirements (GDPR, spam laws, etc.). The Notification Service is responsible for delivery, not content compliance validation.

## Dependencies

### Internal Dependencies

- **User Service**: Provides user profile information and user IDs for preference lookup
- **Authentication Service**: Validates API requests to preference management endpoints
- **Reporting/Analytics Service**: Consumes delivery logs for business intelligence and reporting
- **All Domain Services**: Publish notification events to RabbitMQ (Order Service, Invoice Service, Customer Service, etc.)

### External Dependencies

- **RabbitMQ**: Message broker for receiving notification events from other microservices
- **LINE Messaging API**: External service for delivering messages via LINE
- **Facebook Messenger API**: External service for delivering messages via Facebook Messenger
- **Instagram DM API**: External service for delivering direct messages via Instagram
- **WhatsApp Business API**: External service for delivering messages via WhatsApp
- **SMTP Email Service**: External email delivery service (SendGrid, AWS SES, etc.)
- **SMS Gateway**: External SMS delivery provider (Twilio, AWS SNS, etc.)
- **Slack API**: Internal communication channel for staff notifications
- **Persistent Database**: Storage for user preferences, channel bindings, and delivery logs
- **Monitoring System**: Infrastructure for metrics collection and alerting

## Out of Scope

- **User-facing preference UI**: The Notification Service provides APIs for preference management, but does not include a web or mobile UI for end users to manage preferences. This is the responsibility of customer-facing applications.

- **Content creation and approval workflows**: The service delivers notifications but does not provide tools for creating, approving, or scheduling marketing campaigns. This is handled by marketing automation or CRM systems.

- **External provider OAuth flows**: The service does not handle user authentication with external providers (e.g., LINE login, Facebook OAuth). Channel bindings are provided by other services that handle these flows.

- **Advanced analytics and segmentation**: While the service logs delivery outcomes, it does not provide analytics dashboards, A/B testing, or audience segmentation features. This is handled by the Reporting/Analytics service.

- **Template design tools**: The service uses templates but does not include a visual template designer or drag-and-drop editor. Templates are managed through code or administrative APIs.

- **Real-time two-way messaging**: The service sends notifications but does not handle inbound messages, chatbot conversations, or customer service interactions. This is handled by dedicated customer service systems.

- **Notification scheduling**: Initial scope focuses on event-driven immediate delivery. Scheduled or time-delayed notifications may be added in future iterations.

- **Push notification to mobile apps**: Initial scope focuses on external messaging platforms. Native mobile push notifications (iOS/Android) may be added as a future channel provider.

- **Voice calls or video messages**: The service is limited to text-based and rich content notifications. Voice or video delivery is out of scope.

## Risks & Mitigations

### Risk 1: External Provider Rate Limiting
**Impact**: High-volume events could trigger rate limits on external providers, causing delivery delays or failures.

**Mitigation**: Implement provider-specific rate limiting and queueing. Use exponential backoff and batch processing for non-critical notifications. Monitor provider usage metrics to stay within limits.

### Risk 2: Cascading Failures from Upstream Services
**Impact**: If multiple domain services simultaneously publish high volumes of events, the Notification Service could be overwhelmed.

**Mitigation**: Implement backpressure mechanisms, queue depth monitoring, and circuit breakers. Use separate queues for critical vs. non-critical notifications. Scale horizontally based on queue depth.

### Risk 3: Data Privacy and Compliance
**Impact**: Storing user contact information and message logs creates GDPR, PDPA, and privacy compliance obligations.

**Mitigation**: Encrypt sensitive data at rest and in transit. Implement data retention policies with automatic purging. Ensure delivery logs do not contain sensitive message content. Coordinate with legal/compliance teams.

### Risk 4: Single Point of Failure for All Notifications
**Impact**: As the centralized notification backbone, any outage in this service affects all customer and internal communications.

**Mitigation**: Design for high availability with redundant instances, health checks, and automatic failover. Implement comprehensive monitoring and alerting. Maintain detailed runbooks for incident response.

### Risk 5: Template Management Complexity
**Impact**: Managing templates across multiple channels and languages could become difficult to maintain and test.

**Mitigation**: Implement template versioning and rollback capabilities. Create automated testing for template rendering. Establish clear governance for template changes.

### Risk 6: External Provider API Changes
**Impact**: Third-party providers (LINE, WhatsApp, etc.) may change their APIs, breaking integrations.

**Mitigation**: Use provider adapter pattern to isolate provider-specific logic. Monitor provider change logs and deprecation notices. Maintain comprehensive integration tests.
