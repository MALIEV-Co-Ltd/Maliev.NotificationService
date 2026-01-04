using Maliev.NotificationService.Data;
using Maliev.Aspire.ServiceDefaults;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// (1) Load secrets from Google Secret Manager (must be first)
builder.AddGoogleSecretManagerVolume(); // Load secrets from /mnt/secrets if available

// (2) Add ServiceDefaults immediately after (includes OpenTelemetry, health checks, Redis, etc.)
builder.AddServiceDefaults();
builder.AddStandardMiddleware(options =>
{
    options.EnableRequestLogging = true;
});

builder.Services.AddMemoryCache();

// Add IAM Client
builder.Services.AddIAMClient(builder.Configuration, "notification");

// Add custom metrics meter
builder.AddServiceMeters("notifications-meter");

// (3) Add PostgreSQL DbContext
builder.AddPostgresDbContext<NotificationDbContext>(connectionName: "NotificationDbContext");

// (4) Add Redis connection (via ServiceDefaults)
builder.AddRedisDistributedCache(instanceName: "notification:");

// Add JWT Authentication and Permission Authorization
builder.AddJwtAuthentication();
builder.Services.AddPermissionAuthorization();

// (4a) Register notification services
builder.Services.AddScoped<Maliev.NotificationService.Api.Services.IDeduplicationService,
    Maliev.NotificationService.Api.Services.DeduplicationService>();
builder.Services.AddScoped<Maliev.NotificationService.Api.Services.INotificationRouter,
    Maliev.NotificationService.Api.Services.NotificationRouter>();
builder.Services.AddScoped<Maliev.NotificationService.Api.Services.IRetryService,
    Maliev.NotificationService.Api.Services.RetryService>();
builder.Services.AddSingleton<Maliev.NotificationService.Api.Services.ITemplateRenderer,
    Maliev.NotificationService.Api.Services.TemplateRenderer>();
builder.Services.AddSingleton<Maliev.NotificationService.Api.Services.IEncryptionService,
    Maliev.NotificationService.Api.Services.EncryptionService>();
builder.Services.AddScoped<Maliev.NotificationService.Api.Services.IAlertingService,
    Maliev.NotificationService.Api.Services.AlertingService>();

// (4aa) Register background services
builder.Services.AddHostedService<Maliev.NotificationService.Api.Services.DeliveryLogCleanupService>();
builder.Services.AddIAMRegistration<Maliev.NotificationService.Api.Authorization.NotificationIAMRegistration>();

// (4b) Register channel providers
builder.Services.AddScoped<Maliev.NotificationService.Api.Providers.EmailProvider>();
builder.Services.AddScoped<Maliev.NotificationService.Api.Providers.LineProvider>();
builder.Services.AddScoped<Maliev.NotificationService.Api.Providers.WhatsAppProvider>();
builder.Services.AddScoped<Maliev.NotificationService.Api.Providers.SmsProvider>();
builder.Services.AddScoped<Maliev.NotificationService.Api.Providers.SlackProvider>();
builder.Services.AddScoped<Maliev.NotificationService.Api.Providers.FacebookMessengerProvider>();
builder.Services.AddScoped<Maliev.NotificationService.Api.Providers.InstagramProvider>();

// (4c) Register channel provider factory
builder.Services.AddScoped<Maliev.NotificationService.Api.Services.ChannelProviderFactory>();

// (4d) Configure HttpClient for channel providers
builder.Services.AddHttpClient("WhatsApp", client =>
{
    client.BaseAddress = new Uri("https://graph.facebook.com/v18.0/");
    client.DefaultRequestHeaders.Add("User-Agent", "Maliev.NotificationService/1.0");
})
.AddStandardResilienceHandler(); // From ServiceDefaults - includes retry, timeout, circuit breaker

builder.Services.AddHttpClient("Facebook", client =>
{
    client.BaseAddress = new Uri("https://graph.facebook.com/v18.0/");
    client.DefaultRequestHeaders.Add("User-Agent", "Maliev.NotificationService/1.0");
})
.AddStandardResilienceHandler();

builder.Services.AddHttpClient("Instagram", client =>
{
    client.BaseAddress = new Uri("https://graph.facebook.com/v18.0/");
    client.DefaultRequestHeaders.Add("User-Agent", "Maliev.NotificationService/1.0");
})
.AddStandardResilienceHandler();

builder.Services.AddHttpClient("Alerting", client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.Add("User-Agent", "Maliev.NotificationService/1.0");
})
.AddStandardResilienceHandler();

// (4e) Add rate limiting for channel providers using TokenBucketRateLimiter
builder.Services.AddRateLimiter(options =>
{
    // LINE provider: 1000 token limit, 10 tokens per second
    options.AddPolicy("LineProvider", context =>
        RateLimitPartition.GetTokenBucketLimiter("line", _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = 1000,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            TokensPerPeriod = 10,
            AutoReplenishment = true
        }));

    // WhatsApp provider: 80 token limit, 80 tokens per second
    options.AddPolicy("WhatsAppProvider", context =>
        RateLimitPartition.GetTokenBucketLimiter("whatsapp", _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = 80,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            TokensPerPeriod = 80,
            AutoReplenishment = true
        }));

    // Email provider: 100 token limit, 100 tokens per second
    options.AddPolicy("EmailProvider", context =>
        RateLimitPartition.GetTokenBucketLimiter("email", _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = 100,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            TokensPerPeriod = 100,
            AutoReplenishment = true
        }));

    // SMS/Twilio provider: 100 token limit, 100 tokens per second
    options.AddPolicy("SmsProvider", context =>
        RateLimitPartition.GetTokenBucketLimiter("sms", _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = 100,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            TokensPerPeriod = 100,
            AutoReplenishment = true
        }));

    // Slack provider: 1 token limit, 1 token per second (Slack has strict rate limits)
    options.AddPolicy("SlackProvider", context =>
        RateLimitPartition.GetTokenBucketLimiter("slack", _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = 1,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            TokensPerPeriod = 1,
            AutoReplenishment = true
        }));

    // Facebook provider: 200 token limit, 200 tokens per second
    options.AddPolicy("FacebookProvider", context =>
        RateLimitPartition.GetTokenBucketLimiter("facebook", _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = 200,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            TokensPerPeriod = 200,
            AutoReplenishment = true
        }));

    // Instagram provider: 200 token limit, 200 tokens per second
    options.AddPolicy("InstagramProvider", context =>
        RateLimitPartition.GetTokenBucketLimiter("instagram", _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = 200,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            TokensPerPeriod = 200,
            AutoReplenishment = true
        }));
});

// (5) Add MassTransit with RabbitMQ using ServiceDefaults
builder.AddMassTransitWithRabbitMq(
    configure: x =>
    {
        x.AddConsumer<Maliev.NotificationService.Api.Consumers.NotificationEventConsumer>();
        x.AddConsumer<Maliev.NotificationService.Api.Consumers.PaymentCompletedEventConsumer>();

        // Add RabbitMQ message scheduler for delayed message delivery
        x.AddDelayedMessageScheduler();
    },
    configureRabbitMq: (context, cfg) =>
    {
        // Configure delayed message scheduler
        cfg.UseDelayedMessageScheduler();

        // Receive endpoint for PaymentCompletedEvent
        cfg.ReceiveEndpoint("notification-payment-completed", e =>
        {
            e.ConfigureConsumer<Maliev.NotificationService.Api.Consumers.PaymentCompletedEventConsumer>(context);
        });

        // Critical notification queue - low prefetch for fast individual processing
        cfg.ReceiveEndpoint("notification-critical", e =>
        {
            e.Bind("maliev.notifications", s =>
            {
                s.RoutingKey = "maliev.notification.v1.*.critical";
                s.ExchangeType = "topic";
            });

            e.PrefetchCount = 10; // Lower prefetch for faster processing
            e.ConcurrentMessageLimit = 10;

            e.ConfigureConsumer<Maliev.NotificationService.Api.Consumers.NotificationEventConsumer>(context);

            // Retry policy: 3 attempts with exponential backoff (1s, 2s, 4s)
            e.UseMessageRetry(r => r.Intervals(1000, 2000, 4000));
        });

        // Standard notification queue - higher prefetch for batch efficiency
        cfg.ReceiveEndpoint("notification-standard", e =>
        {
            e.Bind("maliev.notifications", s =>
            {
                s.RoutingKey = "maliev.notification.v1.*.standard";
                s.ExchangeType = "topic";
            });

            e.PrefetchCount = 50; // Higher prefetch for throughput
            e.ConcurrentMessageLimit = 50;

            e.ConfigureConsumer<Maliev.NotificationService.Api.Consumers.NotificationEventConsumer>(context);

            // Retry policy: 1 attempt with fixed 5s delay
            e.UseMessageRetry(r => r.Interval(1, 5000));
        });
    });

// (6) Add API versioning
builder.AddDefaultApiVersioning();

// (7) Add controllers
builder.Services.AddControllers();

// Add OpenAPI
builder.AddStandardOpenApi(
    title: "MALIEV Notification Service API",
    description: "Centralized notification service for the Maliev platform. Handles multi-channel message delivery (Email, LINE, WhatsApp, SMS, Slack) with template management, priority routing, and automatic retry logic.");

// NOTE: ServiceDefaults already configures:
// - OpenAPI/Scalar via AddServiceDefaults()
// - Health checks for PostgreSQL, Redis (via AddPostgresDbContext, AddRedisDistributedCache)
// - OpenTelemetry metrics, tracing, logging
// - Standard resilience patterns for HttpClients

var app = builder.Build();

// Add standard middleware
app.UseStandardMiddleware();
app.UseCors();

// Enable Authentication and Authorization
app.UseAuthentication();
app.UseAuthorization();

// Map ServiceDefaults endpoints (/health, /liveness, /readiness, /metrics)
app.MapDefaultEndpoints("notification");

// Map OpenAPI and Scalar documentation (dev/staging only)
app.MapApiDocumentation(servicePrefix: "notification");

app.MapControllers();

// Run database migrations and seeding asynchronously (non-blocking)
// Create logger instance within this scope
var logger = app.Services.GetRequiredService<ILogger<Program>>(); // Get logger from app services

await app.MigrateDatabaseAsync<NotificationDbContext>();

using var scope = app.Services.CreateScope();
var dbContext = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

await SeedDefaultTemplatesAsync(dbContext, logger);
logger.LogInformation("Database seeding completed successfully");

// Initialize metrics (non-blocking)
InitializeMetrics(app.Services);

await app.RunAsync();

// <summary>
// Seeds default notification templates for common scenarios
// </summary>
static async Task SeedDefaultTemplatesAsync(NotificationDbContext dbContext, ILogger logger)
{
    var strategy = dbContext.Database.CreateExecutionStrategy();

    await strategy.ExecuteAsync(async () =>
    {
        using var transaction = await dbContext.Database.BeginTransactionAsync();

        try
        {
            // order-confirmed template (English, Email)
            await SeedTemplateIfNotExistsAsync(dbContext, new Maliev.NotificationService.Data.Entities.NotificationTemplate
            {
                TemplateKey = "order-confirmed",
                Version = 1,
                Language = "en",
                ChannelType = "email",
                ContentTemplate = "Hello {{name}},\n\nYour order #{{orderId}} has been confirmed!\n\nOrder total: {{amount}}\n\nThank you for your business.",
                Parameters = new[] { "name", "orderId", "amount" }
            });

            // order-confirmed template (Thai, Email)
            await SeedTemplateIfNotExistsAsync(dbContext, new Maliev.NotificationService.Data.Entities.NotificationTemplate
            {
                TemplateKey = "order-confirmed",
                Version = 1,
                Language = "th",
                ChannelType = "email",
                ContentTemplate = "สวัสดีค่ะ คุณ{{name}}\n\nคำสั่งซื้อหมายเลข #{{orderId}} ของคุณได้รับการยืนยันแล้ว!\n\nยอดรวม: {{amount}}\n\nขอบคุณที่ใช้บริการ",
                Parameters = new[] { "name", "orderId", "amount" }
            });

            // payment-failed template (English, Email)
            await SeedTemplateIfNotExistsAsync(dbContext, new Maliev.NotificationService.Data.Entities.NotificationTemplate
            {
                TemplateKey = "payment-failed",
                Version = 1,
                Language = "en",
                ChannelType = "email",
                ContentTemplate = "Hello {{name}},\n\nYour payment of {{amount}} has failed.\n\nReason: {{reason}}\n\nPlease update your payment method and try again.",
                Parameters = new[] { "name", "amount", "reason" }
            });

            // payment-failed template (Thai, Email)
            await SeedTemplateIfNotExistsAsync(dbContext, new Maliev.NotificationService.Data.Entities.NotificationTemplate
            {
                TemplateKey = "payment-failed",
                Version = 1,
                Language = "th",
                ChannelType = "email",
                ContentTemplate = "สวัสดีค่ะ คุณ{{name}}\n\nการชำระเงินจำนวน {{amount}} ของคุณล้มเหลว\n\nเหตุผล: {{reason}}\n\nกรุณาอัพเดทวิธีการชำระเงินและลองใหม่อีกครั้ง",
                Parameters = new[] { "name", "amount", "reason" }
            });

            // system-outage template (English, Email)
            await SeedTemplateIfNotExistsAsync(dbContext, new Maliev.NotificationService.Data.Entities.NotificationTemplate
            {
                TemplateKey = "system-outage",
                Version = 1,
                Language = "en",
                ChannelType = "email",
                ContentTemplate = "SYSTEM OUTAGE ALERT\n\nService: {{service}}\n\nStatus: {{status}}\n\nEstimated resolution time: {{eta}}\n\nWe apologize for any inconvenience.",
                Parameters = new[] { "service", "status", "eta" }
            });

            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            logger.LogInformation("Default templates seeded successfully");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            logger.LogError(ex, "Error seeding default templates");
            throw;
        }
    });
}

// <summary>
// Seeds a template if it doesn't already exist
// </summary>
static async Task SeedTemplateIfNotExistsAsync(
    NotificationDbContext dbContext,
    Maliev.NotificationService.Data.Entities.NotificationTemplate template)
{
    var exists = await dbContext.NotificationTemplates.AnyAsync(t =>
        t.TemplateKey == template.TemplateKey &&
        t.Version == template.Version &&
        t.Language == template.Language &&
        t.ChannelType == template.ChannelType);

    if (!exists)
    {
        dbContext.NotificationTemplates.Add(template);
    }
}

// <summary>
// Initializes OpenTelemetry metrics with observable gauges for queue monitoring
// </summary>
static void InitializeMetrics(IServiceProvider services)
{
    // Initialize retry queue depth gauge with database query lambda
    Maliev.NotificationService.Api.Metrics.NotificationMetrics.Initialize(() =>
    {
        try
        {
            using var scope = services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

            // Count all pending retry queue entries
            var depth = dbContext.RetryQueueEntries.Count();

            return depth;
        }
        catch
        {
            // Return 0 if query fails (e.g., during startup before DB is ready)
            return 0;
        }
    });
}

