# Phase 0: Research & Technology Decisions

**Feature**: Notification Service
**Date**: 2025-12-05
**Status**: Completed

## Overview

This document captures technology decisions, design patterns, and implementation strategies for the Notification Service. All decisions are made in alignment with the MALIEV constitution and .NET 10 best practices.

---

## 1. Channel Provider SDK Selection

### Decision
Use official SDKs where available, HTTP clients for simpler APIs:
- **LINE**: LineBot SDK for .NET (`Line.Messaging`)
- **WhatsApp Business**: Official WhatsApp Business API via HttpClient (Facebook Graph API)
- **Email**: MailKit (SMTP client) or SendGrid SDK
- **SMS**: Twilio SDK for .NET
- **Slack**: Slack.Webhooks or official Slack SDK
- **Facebook Messenger**: Facebook Graph API via HttpClient
- **Instagram**: Facebook Graph API via HttpClient (Instagram Messaging API)

### Rationale
- Official SDKs provide type safety, automatic retries, and versioning support
- HttpClient with typed clients for REST APIs offers simplicity and control
- Avoid third-party wrappers that add unnecessary abstraction layers
- All SDKs compatible with .NET 10 and support async/await patterns

### Alternatives Considered
- **Unified messaging library (Vonage, Twilio multi-channel)**: Rejected due to vendor lock-in and limited channel support
- **Custom HTTP implementations for all channels**: Rejected due to maintenance overhead and lack of SDK benefits
- **RestSharp/Refit for all APIs**: Rejected in favor of native HttpClient with IHttpClientFactory for better integration with ServiceDefaults

### Implementation Notes
```csharp
// Typed HttpClient example for WhatsApp
public class WhatsAppProvider : IChannelProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WhatsAppProvider> _logger;

    public WhatsAppProvider(IHttpClientFactory factory, ILogger<WhatsAppProvider> logger)
    {
        _httpClient = factory.CreateClient("WhatsApp");
        _logger = logger;
    }

    public async Task<DeliveryResult> SendAsync(string recipientId, string message, CancellationToken ct)
    {
        var request = new { to = recipientId, text = new { body = message } };
        var response = await _httpClient.PostAsJsonAsync("/messages", request, ct);
        // Handle response, map to DeliveryResult
    }
}
```

Register in Program.cs:
```csharp
builder.Services.AddHttpClient("WhatsApp", client =>
{
    client.BaseAddress = new Uri("https://graph.facebook.com/v18.0/{phone-number-id}/");
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {whatsAppToken}");
})
.AddStandardResilienceHandler(); // From ServiceDefaults - includes retry, timeout, circuit breaker
```

---

## 2. Rate Limiting Patterns for External Provider APIs

### Decision
**Per-Provider Token Bucket Pattern** using `System.Threading.RateLimiting` (.NET 7+):
- Configure rate limits per provider based on their documented limits
- Use `TokenBucketRateLimiter` for burst allowance with sustained rate
- Implement graceful backoff when limits are approached

### Rationale
- Built-in .NET rate limiting is performant and well-tested
- Token bucket allows burst traffic while enforcing average rate
- Per-provider configuration enables different limits for each channel
- Integrates with ASP.NET Core middleware for unified approach

### Alternatives Considered
- **Redis-based distributed rate limiting**: Rejected for Phase 1 (horizontal scaling can use local limiters with queue-based distribution)
- **Polly rate limit policy**: Rejected in favor of newer built-in rate limiting primitives
- **Manual token bucket implementation**: Rejected due to reinventing the wheel

### Implementation Notes
```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("LineProvider", context =>
        RateLimitPartition.GetTokenBucketLimiter("line", _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = 1000,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            TokensPerPeriod = 10,
            AutoReplenishment = true
        }));

    options.AddPolicy("WhatsAppProvider", context =>
        RateLimitPartition.GetTokenBucketLimiter("whatsapp", _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = 80,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            TokensPerPeriod = 80,
            AutoReplenishment = true
        }));
});

// In provider implementation:
public class LineProvider : IChannelProvider
{
    private readonly RateLimiter _rateLimiter;

    public async Task<DeliveryResult> SendAsync(string recipientId, string message, CancellationToken ct)
    {
        using var lease = await _rateLimiter.AcquireAsync(permitCount: 1, ct);
        if (!lease.IsAcquired)
        {
            _logger.LogWarning("Rate limit exceeded for LINE provider");
            return DeliveryResult.RateLimited();
        }

        // Send message
    }
}
```

---

## 3. RabbitMQ Event Schema Design and Versioning

### Decision
**CloudEvents-inspired schema with explicit versioning**:
- Use routing key pattern: `maliev.notification.v1.{event-type}.{priority}`
- Example: `maliev.notification.v1.order-confirmed.critical`
- Envelope pattern with metadata + payload
- Schema versioning in message headers

### Rationale
- CloudEvents is industry standard for event metadata
- Routing key versioning enables backward-compatible changes
- Priority in routing key allows queue separation for critical vs non-critical
- Explicit schema version in message enables consumer flexibility

### Event Schema Structure
```json
{
  "id": "uuid-v4",
  "source": "maliev.order.v1",
  "type": "order.confirmed",
  "time": "2025-12-05T10:30:00Z",
  "datacontenttype": "application/json",
  "specversion": "1.0",
  "data": {
    "notificationType": "OrderConfirmation",
    "priority": "critical",
    "targetUsers": [
      { "userId": "user123", "userType": "customer" }
    ],
    "templateId": "order-confirmed-template",
    "parameters": {
      "orderNumber": "ORD-12345",
      "customerName": "John Doe",
      "totalAmount": "1500.00 THB"
    },
    "metadata": {
      "language": "th",
      "source": "order-service"
    }
  }
}
```

### Routing Strategy
```csharp
// MassTransit configuration in Program.cs
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<NotificationEventConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration.GetConnectionString("rabbitmq"));

        cfg.ReceiveEndpoint("notification-critical", e =>
        {
            e.Bind("maliev.notifications", s =>
            {
                s.RoutingKey = "maliev.notification.v1.*.critical";
                s.ExchangeType = "topic";
            });
            e.ConfigureConsumer<NotificationEventConsumer>(context);
            e.PrefetchCount = 10; // Limit concurrent processing
        });

        cfg.ReceiveEndpoint("notification-standard", e =>
        {
            e.Bind("maliev.notifications", s =>
            {
                s.RoutingKey = "maliev.notification.v1.*.standard";
                s.ExchangeType = "topic";
            });
            e.ConfigureConsumer<NotificationEventConsumer>(context);
            e.PrefetchCount = 50; // Higher throughput for non-critical
        });
    });
});
```

### Alternatives Considered
- **Protobuf/Avro binary serialization**: Rejected for maintainability (JSON is human-readable for debugging)
- **Version in message body only**: Rejected (routing key versioning enables infrastructure-level routing)
- **Single queue for all priorities**: Rejected (critical messages could be blocked by marketing batch)

---

## 4. Redis Deduplication Cache Implementation

### Decision
**Redis with TTL-based expiration + hash-based keys**:
- Key format: `dedup:{hash(eventId+timestamp)}`
- Value: `1` (presence check only)
- TTL: 86400 seconds (24 hours)
- Use `SET NX` (SET if Not eXists) for atomic check-and-set

### Rationale
- Redis TTL automatically handles cache cleanup
- Hash-based keys prevent long key names
- Atomic `SET NX` prevents race conditions in distributed environment
- Lightweight value (no need to store full event data)

### Implementation Notes
```csharp
public class DeduplicationService : IDeduplicationService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<DeduplicationService> _logger;

    public DeduplicationService(IDistributedCache cache, ILogger<DeduplicationService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<bool> IsDuplicateAsync(string eventId, DateTimeOffset timestamp, CancellationToken ct)
    {
        var key = GenerateKey(eventId, timestamp);
        var existingValue = await _cache.GetStringAsync(key, ct);

        if (existingValue != null)
        {
            _logger.LogInformation("Duplicate event detected: {EventId}", eventId);
            return true; // Duplicate
        }

        await _cache.SetStringAsync(key, "1", new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
        }, ct);

        return false; // Not a duplicate
    }

    private static string GenerateKey(string eventId, DateTimeOffset timestamp)
    {
        var input = $"{eventId}:{timestamp.ToUnixTimeSeconds()}";
        var hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
        return $"dedup:{hash}";
    }
}
```

### Alternatives Considered
- **PostgreSQL-based deduplication**: Rejected (Redis is faster for high-throughput checks)
- **Bloom filter**: Rejected (false positives unacceptable for notifications)
- **Sliding window with database**: Rejected (adds latency to critical path)

---

## 5. MassTransit Consumer Configuration for Priority Processing

### Decision
**Separate queues with different prefetch counts**:
- Critical queue: Low prefetch (10), high priority, dedicated consumers
- Standard queue: High prefetch (50), normal priority, shared consumers
- Use RabbitMQ priority queues if needed (future optimization)

### Rationale
- Separate queues prevent head-of-line blocking
- Different prefetch counts optimize for latency vs throughput
- MassTransit handles consumer scaling automatically
- Routing key-based separation requires no consumer logic changes

### Configuration
```csharp
cfg.ReceiveEndpoint("notification-critical", e =>
{
    e.PrefetchCount = 10; // Lower prefetch for faster individual processing
    e.ConcurrentMessageLimit = 10; // Limit concurrent processing
    e.ConfigureConsumer<NotificationEventConsumer>(context);

    // Optional: Set consumer priority
    e.Consumer<NotificationEventConsumer>(c =>
        c.Options<ConsumerOptions>(o => o.SetPriority(100)));
});

cfg.ReceiveEndpoint("notification-standard", e =>
{
    e.PrefetchCount = 50; // Higher prefetch for batch efficiency
    e.ConcurrentMessageLimit = 50;
    e.ConfigureConsumer<NotificationEventConsumer>(context);
});
```

### Alternatives Considered
- **Single queue with message priority header**: Rejected (RabbitMQ priority queues have performance overhead)
- **Delayed exchange for retries**: Considered for future (Phase 2 optimization)
- **Separate RabbitMQ vhosts**: Rejected (overkill for simple priority separation)

---

## 6. Template Rendering Engine Selection

### Decision
**Lightweight custom renderer with Scriban as fallback**:
- Phase 1: Simple string interpolation with `{{parameter}}` placeholders
- Phase 2: Scriban template engine for complex logic (loops, conditionals)
- Store templates in PostgreSQL with versioning
- Cache compiled templates in memory

### Rationale
- Simple interpolation covers 80% of use cases (name, order number, amount)
- Scriban is .NET-native, fast, and security-focused (no code execution)
- Avoid Razor (too heavy, requires compilation)
- In-memory caching reduces database load

### Phase 1 Implementation (Simple Interpolation)
```csharp
public class TemplateRenderer : ITemplateRenderer
{
    private readonly IMemoryCache _cache;

    public string Render(string template, Dictionary<string, string> parameters)
    {
        var result = template;
        foreach (var (key, value) in parameters)
        {
            result = result.Replace($"{{{{{key}}}}}", value, StringComparison.OrdinalIgnoreCase);
        }
        return result;
    }
}

// Template example stored in database:
// "Hello {{customerName}}, your order {{orderNumber}} has been confirmed. Total: {{totalAmount}}"
```

### Phase 2 Enhancement (Scriban)
```csharp
public class ScribanTemplateRenderer : ITemplateRenderer
{
    private readonly IMemoryCache _cache;

    public string Render(string template, Dictionary<string, object> parameters)
    {
        var compiledTemplate = _cache.GetOrCreate($"template:{template.GetHashCode()}", _ =>
        {
            return Scriban.Template.Parse(template);
        });

        return compiledTemplate.Render(parameters);
    }
}

// Advanced template example:
// "Hello {{customer.name}}, {{if order.items.size > 1}}your {{order.items.size}} items{{else}}your item{{end}} shipped!"
```

### Alternatives Considered
- **Liquid templates**: Rejected (Scriban is more performant and better .NET integration)
- **Handlebars.NET**: Rejected (Scriban has better documentation and active maintenance)
- **Razor templates**: Rejected (too heavyweight, requires runtime compilation)
- **Mustache**: Rejected (Scriban superset with more features)

---

## 7. Background Job Processing for Retry Queue

### Decision
**MassTransit Scheduled Message Delivery**:
- Use `ScheduleMessage` for delayed retries (1s, 2s, 4s intervals)
- MassTransit handles scheduling via RabbitMQ delayed exchange plugin
- Retry state tracked in PostgreSQL RetryQueueEntry table
- Maximum 3 retries before moving to dead-letter queue

### Rationale
- MassTransit built-in scheduling eliminates need for Quartz.NET/Hangfire
- Durable scheduling survives service restarts
- Integrates seamlessly with existing RabbitMQ infrastructure
- Exponential backoff configured declaratively

### Implementation
```csharp
public class RetryService : IRetryService
{
    private readonly IBus _bus;
    private readonly NotificationDbContext _db;

    public async Task ScheduleRetryAsync(NotificationEvent evt, int attemptNumber, CancellationToken ct)
    {
        var delaySeconds = Math.Pow(2, attemptNumber - 1); // 1s, 2s, 4s
        var scheduledTime = DateTimeOffset.UtcNow.AddSeconds(delaySeconds);

        // Track retry in database
        var retryEntry = new RetryQueueEntry
        {
            EventId = evt.Id,
            AttemptNumber = attemptNumber,
            ScheduledTime = scheduledTime,
            EventPayload = JsonSerializer.Serialize(evt)
        };
        _db.RetryQueueEntries.Add(retryEntry);
        await _db.SaveChangesAsync(ct);

        // Schedule message delivery
        await _bus.SchedulePublish(scheduledTime, evt, ct);
    }
}

// Consumer handles retries transparently
public class NotificationEventConsumer : IConsumer<NotificationEvent>
{
    public async Task Consume(ConsumeContext<NotificationEvent> context)
    {
        var evt = context.Message;
        var result = await _notificationRouter.RouteAsync(evt);

        if (!result.Success && result.IsRetryable && evt.AttemptNumber < 3)
        {
            await _retryService.ScheduleRetryAsync(evt, evt.AttemptNumber + 1, context.CancellationToken);
        }
        else if (!result.Success && evt.AttemptNumber >= 3)
        {
            await _deadLetterService.MoveToDeadLetterAsync(evt, result.Error);
        }
    }
}
```

### Alternatives Considered
- **Hangfire**: Rejected (adds unnecessary dependency when MassTransit provides scheduling)
- **Quartz.NET**: Rejected (same reason as Hangfire)
- **Manual timer-based retry**: Rejected (not durable across restarts)
- **Database polling with background service**: Rejected (less efficient than message scheduling)

---

## 8. Dead-Letter Queue Patterns in MassTransit

### Decision
**MassTransit automatic dead-letter + custom dead-letter table**:
- Configure `UseMessageRetry` with retry policy
- Let MassTransit route exhausted messages to `{queue-name}_error` queue
- Also persist to PostgreSQL DeadLetterRecord table for audit and manual intervention
- Expose dead-letter query API for support team

### Rationale
- MassTransit's built-in DLQ prevents message loss
- Custom table provides queryable audit trail
- Support team can manually retry or investigate failures
- Automatic alerts on dead-letter queue growth

### Configuration
```csharp
cfg.ReceiveEndpoint("notification-critical", e =>
{
    e.UseMessageRetry(r => r.Intervals(1000, 2000, 4000)); // Exponential backoff
    e.UseInMemoryOutbox(); // Ensures exactly-once processing

    e.ConfigureConsumer<NotificationEventConsumer>(context);
});

// Dead-letter tracking middleware
public class DeadLetterTrackingFilter<T> : IFilter<ConsumeContext<T>> where T : class
{
    private readonly NotificationDbContext _db;

    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        try
        {
            await next.Send(context);
        }
        catch (Exception ex)
        {
            if (context.GetRetryAttempt() >= 3)
            {
                // Save to dead-letter table
                var deadLetter = new DeadLetterRecord
                {
                    EventId = (context.Message as NotificationEvent)?.Id,
                    FailureReason = ex.Message,
                    StackTrace = ex.StackTrace,
                    EventPayload = JsonSerializer.Serialize(context.Message),
                    CreatedAt = DateTimeOffset.UtcNow
                };
                _db.DeadLetterRecords.Add(deadLetter);
                await _db.SaveChangesAsync();
            }
            throw;
        }
    }
}
```

### Alternatives Considered
- **Manual dead-letter queue management**: Rejected (MassTransit's built-in handling is robust)
- **Send to external logging service only**: Rejected (need queryable database for support team)
- **No dead-letter persistence**: Rejected (audit requirements mandate record-keeping)

---

## 9. Metrics Instrumentation Patterns for OpenTelemetry

### Decision
**OpenTelemetry Meter API with custom metrics class**:
- Use `System.Diagnostics.Metrics.Meter` (.NET standard)
- Expose metrics via ServiceDefaults' OpenTelemetry exporter
- Use tags for dimension slicing (channel, status, provider)
- Histogram for latency, Counter for events, Gauge for queue depth

### Rationale
- OpenTelemetry is cloud-native standard
- ServiceDefaults already configures OTLP exporter
- Built-in .NET metrics API is performant and well-integrated
- Tags enable rich querying in Prometheus/Grafana

### Implementation
```csharp
public class NotificationMetrics
{
    private static readonly Meter Meter = new("Maliev.NotificationService", "1.0");

    // Counters
    public static readonly Counter<long> NotificationsSent = Meter.CreateCounter<long>(
        "notification_sent_total",
        description: "Total number of notifications sent");

    public static readonly Counter<long> NotificationsFailed = Meter.CreateCounter<long>(
        "notification_failed_total",
        description: "Total number of failed notifications");

    public static readonly Counter<long> FallbackTriggered = Meter.CreateCounter<long>(
        "notification_fallback_total",
        description: "Total number of fallback channel activations");

    // Histogram for latency
    public static readonly Histogram<double> DeliveryLatency = Meter.CreateHistogram<double>(
        "notification_delivery_latency_ms",
        unit: "ms",
        description: "Notification delivery latency");

    // ObservableGauge for queue depth
    public static ObservableGauge<int> RetryQueueDepth { get; private set; }

    public static void Initialize(Func<int> getRetryQueueDepth)
    {
        RetryQueueDepth = Meter.CreateObservableGauge(
            "notification_retry_queue_depth",
            getRetryQueueDepth,
            description: "Current retry queue depth");
    }
}

// Usage in service
public async Task<DeliveryResult> SendAsync(NotificationEvent evt)
{
    var stopwatch = Stopwatch.StartNew();
    try
    {
        var result = await _provider.SendAsync(evt.RecipientId, evt.Message);

        NotificationMetrics.NotificationsSent.Add(1,
            new KeyValuePair<string, object>("channel", _provider.ChannelType),
            new KeyValuePair<string, object>("priority", evt.Priority));

        NotificationMetrics.DeliveryLatency.Record(stopwatch.ElapsedMilliseconds,
            new KeyValuePair<string, object>("channel", _provider.ChannelType),
            new KeyValuePair<string, object>("status", "success"));

        return result;
    }
    catch (Exception ex)
    {
        NotificationMetrics.NotificationsFailed.Add(1,
            new KeyValuePair<string, object>("channel", _provider.ChannelType),
            new KeyValuePair<string, object>("error_type", ex.GetType().Name));
        throw;
    }
}
```

### Program.cs Configuration
```csharp
// ServiceDefaults already configures OpenTelemetry, just register custom meter
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddMeter("Maliev.NotificationService");
    });

// Initialize observable metrics after service registration
var app = builder.Build();
using var scope = app.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
NotificationMetrics.Initialize(() => db.RetryQueueEntries.Count(e => e.ScheduledTime > DateTime.UtcNow));
```

### Alternatives Considered
- **prometheus-net**: Rejected (constitution forbids it, use OpenTelemetry)
- **Custom metrics endpoint**: Rejected (ServiceDefaults handles OTLP export)
- **Application Insights SDK**: Rejected (vendor lock-in, use OpenTelemetry instead)

---

## 10. Channel Provider Adapter Interface Design

### Decision
**Async interface with result pattern + explicit error types**:
```csharp
public interface IChannelProvider
{
    string ChannelType { get; }
    Task<DeliveryResult> SendAsync(string recipientId, string message, Dictionary<string, string> metadata, CancellationToken ct);
    Task<ValidationResult> ValidateRecipientAsync(string recipientId, CancellationToken ct);
    Task<ProviderHealthStatus> GetHealthAsync(CancellationToken ct);
}

public record DeliveryResult
{
    public bool Success { get; init; }
    public string? MessageId { get; init; }
    public DeliveryFailureType? FailureType { get; init; }
    public string? ErrorMessage { get; init; }
    public bool IsRetryable { get; init; }
    public TimeSpan? RetryAfter { get; init; }
}

public enum DeliveryFailureType
{
    Transient,           // Network issue, timeout (retryable)
    RateLimitExceeded,   // Provider rate limit (retryable with delay)
    InvalidRecipient,    // Permanent failure, mark binding invalid
    AuthenticationFailed,// Provider credentials issue
    ProviderError        // Provider-side error (check retryability)
}
```

### Rationale
- Explicit result types eliminate exceptions for expected failures
- `IsRetryable` flag simplifies retry logic
- `RetryAfter` respects provider rate limit headers (429 responses)
- Metadata dictionary allows channel-specific options (e.g., LINE flex messages)
- Health check enables provider circuit breaker patterns

### Provider Registration Pattern
```csharp
// Program.cs
builder.Services.AddScoped<IChannelProvider, EmailProvider>();
builder.Services.AddScoped<IChannelProvider, LineProvider>();
builder.Services.AddScoped<IChannelProvider, SmsProvider>();
// ... register all providers

// Provider factory/router
public class ChannelProviderFactory
{
    private readonly IEnumerable<IChannelProvider> _providers;

    public ChannelProviderFactory(IEnumerable<IChannelProvider> providers)
    {
        _providers = providers;
    }

    public IChannelProvider GetProvider(string channelType)
    {
        return _providers.FirstOrDefault(p => p.ChannelType == channelType)
            ?? throw new InvalidOperationException($"No provider registered for channel: {channelType}");
    }
}
```

### Alternatives Considered
- **Exception-based error handling**: Rejected (exceptions are expensive and obscure control flow)
- **Generic Result<T, E> pattern**: Rejected (overkill for this use case, explicit types are clearer)
- **Separate interfaces per provider**: Rejected (breaks adapter pattern, complicates routing)
- **Plugin system with DLL loading**: Rejected (YAGNI, providers are compiled into service)

---

## Summary of Key Decisions

| Area | Decision | Key Benefit |
|------|----------|-------------|
| Channel Providers | Official SDKs + typed HttpClient | Type safety, automatic retries, versioning |
| Rate Limiting | Token bucket per provider | Burst allowance, fair queuing, built-in .NET |
| Event Schema | CloudEvents + routing key versioning | Industry standard, backward compatibility |
| Deduplication | Redis with TTL + SHA256 hash keys | Automatic cleanup, atomic operations |
| Priority Processing | Separate RabbitMQ queues | Prevents head-of-line blocking |
| Templates | Simple interpolation → Scriban | Progressive enhancement, 80/20 rule |
| Retries | MassTransit scheduled delivery | Durable, survives restarts, no extra dependencies |
| Dead-Letter | MassTransit DLQ + PostgreSQL audit | Automatic + queryable audit trail |
| Metrics | OpenTelemetry Meter API | Cloud-native standard, rich dimensions |
| Provider Adapter | Async interface + result pattern | Explicit error handling, simplified routing |

---

## Next Steps

1. ✅ Research complete - all technology decisions documented
2. ⏭️ **Phase 1**: Generate data model, API contracts, and quickstart guide
3. ⏭️ **Phase 2**: Task decomposition and implementation planning

---

**Research Version**: 1.0
**Completed**: 2025-12-05
**Reviewed By**: Planning Agent
