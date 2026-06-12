using Maliev.Aspire.ServiceDefaults;
using Maliev.NotificationService.Api.Configuration;
using Maliev.NotificationService.Api.Services;
using Maliev.NotificationService.Infrastructure.Persistence;
using Maliev.MessagingContracts.Contracts.Customers;
using Maliev.MessagingContracts.Contracts.Payments;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
// Initialize bootstrap logging
using var loggerFactory = LoggerFactory.Create(logBuilder => logBuilder.AddConsole());
var bootstrapLogger = loggerFactory.CreateLogger("Program");

try
{
    Log.StartingHost(bootstrapLogger, "Notification Service");

    var builder = WebApplication.CreateBuilder(args);

    // (1) Load secrets from Google Secret Manager (must be first)
    builder.AddGoogleSecretManagerVolume(); // Load secrets from /mnt/secrets if available

    // (2) Add ServiceDefaults immediately after (includes OpenTelemetry, health checks, Redis, etc.)
    builder.AddServiceDefaults();
    builder.AddDefaultApiVersioning();
    builder.AddStandardMiddleware(options =>
    {
        options.EnableRequestLogging = true;
    });

    builder.Services.AddMemoryCache(options =>
    {
        options.SizeLimit = 1024;
    });

    // Register options for external providers
    builder.Services.Configure<ExternalProvidersOptions>(
        builder.Configuration.GetSection(ExternalProvidersOptions.SectionName));

    // Add IAM Client
    builder.AddIAMServiceClient("notification");

    // Add custom metrics meter
    builder.AddServiceMeters("notifications-meter");

    // (3) Add PostgreSQL DbContext
    builder.AddPostgresDbContext<NotificationDbContext>(connectionName: "NotificationDbContext");

    // (4) Add Redis connection (via ServiceDefaults)
    builder.AddStandardCache("notification:"); // Redis + in-memory fallback, memory-optimized

    // Add JWT Authentication and Permission Authorization
    builder.AddJwtAuthentication();
    builder.Services.AddPermissionAuthorization();

    // Add Customer Service Client
    builder.AddNotificationCustomerServiceClient();

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
    builder.Services.AddHostedService<Maliev.NotificationService.Api.Services.RetryCleanupBackgroundService>();
    builder.Services.AddIAMRegistration<Maliev.NotificationService.Api.Authorization.NotificationIAMRegistration>("notification");

    // (4b) Register channel providers
    builder.Services.AddScoped<Maliev.NotificationService.Api.Providers.EmailProvider>();
    builder.Services.AddScoped<Maliev.NotificationService.Api.Providers.LineProvider>();
    builder.Services.AddScoped<Maliev.NotificationService.Api.Providers.WhatsAppProvider>();
    builder.Services.AddScoped<Maliev.NotificationService.Api.Providers.SmsProvider>();
    builder.Services.AddScoped<Maliev.NotificationService.Api.Providers.SlackProvider>();
    builder.Services.AddScoped<Maliev.NotificationService.Api.Providers.FacebookMessengerProvider>();
    builder.Services.AddScoped<Maliev.NotificationService.Api.Providers.InstagramProvider>();

    builder.Services.AddScoped<Maliev.NotificationService.Api.Providers.IChannelProvider>(sp => sp.GetRequiredService<Maliev.NotificationService.Api.Providers.EmailProvider>());
    builder.Services.AddScoped<Maliev.NotificationService.Api.Providers.IChannelProvider>(sp => sp.GetRequiredService<Maliev.NotificationService.Api.Providers.LineProvider>());
    builder.Services.AddScoped<Maliev.NotificationService.Api.Providers.IChannelProvider>(sp => sp.GetRequiredService<Maliev.NotificationService.Api.Providers.WhatsAppProvider>());
    builder.Services.AddScoped<Maliev.NotificationService.Api.Providers.IChannelProvider>(sp => sp.GetRequiredService<Maliev.NotificationService.Api.Providers.SmsProvider>());
    builder.Services.AddScoped<Maliev.NotificationService.Api.Providers.IChannelProvider>(sp => sp.GetRequiredService<Maliev.NotificationService.Api.Providers.SlackProvider>());
    builder.Services.AddScoped<Maliev.NotificationService.Api.Providers.IChannelProvider>(sp => sp.GetRequiredService<Maliev.NotificationService.Api.Providers.FacebookMessengerProvider>());
    builder.Services.AddScoped<Maliev.NotificationService.Api.Providers.IChannelProvider>(sp => sp.GetRequiredService<Maliev.NotificationService.Api.Providers.InstagramProvider>());

    // (4c) Register channel provider factory
    builder.Services.AddScoped<Maliev.NotificationService.Api.Services.ChannelProviderFactory>();

    // (4d) Configure HttpClient for channel providers
    builder.Services.AddHttpClient("WhatsApp", client =>
    {
        client.BaseAddress = new Uri("https://graph.facebook.com/v18.0/");
        client.DefaultRequestHeaders.Add("User-Agent", "Maliev.NotificationService/1.0");
    })
    .AddStandardResilienceHandler(); // From ServiceDefaults - includes retry, timeout, circuit breaker

    builder.Services.AddHttpClient("Line", client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["ApiBaseAddresses:LINE"] ?? "https://api.line.me/v2/bot");
    })
    .AddStandardResilienceHandler();

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
    builder.AddStandardRateLimiting(); // Memory-optimized for low-spec nodes
    // (5) Add MassTransit with RabbitMQ using ServiceDefaults
    builder.AddMassTransitWithRabbitMq(
        configure: x =>
        {
            x.AddConsumer<Maliev.NotificationService.Api.Consumers.NotificationEventConsumer>();
            x.AddConsumer<Maliev.NotificationService.Api.Consumers.PaymentCompletedEventConsumer>();
            x.AddConsumer<Maliev.NotificationService.Api.Consumers.PaymentFailedEventConsumer>();
            x.AddConsumer<Maliev.NotificationService.Api.Consumers.PaymentCancelledEventConsumer>();
            x.AddConsumer<Maliev.NotificationService.Api.Consumers.PaymentExpiredEventConsumer>();
            x.AddConsumer<Maliev.NotificationService.Api.Consumers.PaymentPendingEventConsumer>();
            x.AddConsumer<Maliev.NotificationService.Api.Consumers.CustomerCreatedEventConsumer>();
            x.AddConsumer<Maliev.NotificationService.Api.Consumers.CustomerUpdatedEventConsumer>();
            x.AddConsumer<Maliev.NotificationService.Api.Consumers.OrderShippedEventConsumer>();
            x.AddConsumer<Maliev.NotificationService.Api.Consumers.OrderCompletedEventConsumer>();
            x.AddConsumer<Maliev.NotificationService.Api.Consumers.JobStatusChangedEventConsumer>();
            x.AddConsumer<Maliev.NotificationService.Api.Consumers.CustomerRegisteredEventConsumer>();
            x.AddConsumer<Maliev.NotificationService.Api.Consumers.VerificationEmailRequestedEventConsumer>();
            x.AddConsumer<Maliev.NotificationService.Api.Consumers.EmailVerifiedEventConsumer>();

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
                e.ConfigureConsumer<Maliev.NotificationService.Api.Consumers.PaymentFailedEventConsumer>(context);
                e.ConfigureConsumer<Maliev.NotificationService.Api.Consumers.PaymentCancelledEventConsumer>(context);
                e.ConfigureConsumer<Maliev.NotificationService.Api.Consumers.PaymentExpiredEventConsumer>(context);
                e.ConfigureConsumer<Maliev.NotificationService.Api.Consumers.PaymentPendingEventConsumer>(context);
            });

            // Receive endpoint for Customer events
            cfg.ReceiveEndpoint("notification-customer-events", e =>
            {
                e.ConfigureConsumer<Maliev.NotificationService.Api.Consumers.CustomerCreatedEventConsumer>(context);
                e.ConfigureConsumer<Maliev.NotificationService.Api.Consumers.CustomerUpdatedEventConsumer>(context);
            });

            // Receive endpoint for Customer registration events
            cfg.ReceiveEndpoint("notification-customer-registered", e =>
            {
                e.ConfigureConsumer<Maliev.NotificationService.Api.Consumers.CustomerRegisteredEventConsumer>(context);
            });

            // Receive endpoint for verification email requests
            cfg.ReceiveEndpoint("notification-verification-email-requested", e =>
            {
                e.ConfigureConsumer<Maliev.NotificationService.Api.Consumers.VerificationEmailRequestedEventConsumer>(context);
            });

            // Receive endpoint for email verified events
            cfg.ReceiveEndpoint("notification-email-verified", e =>
            {
                e.ConfigureConsumer<Maliev.NotificationService.Api.Consumers.EmailVerifiedEventConsumer>(context);
            });

            // Receive endpoint for Order lifecycle events
            cfg.ReceiveEndpoint("notification-order-lifecycle", e =>
            {
                e.ConfigureConsumer<Maliev.NotificationService.Api.Consumers.OrderShippedEventConsumer>(context);
                e.ConfigureConsumer<Maliev.NotificationService.Api.Consumers.OrderCompletedEventConsumer>(context);
                e.ConfigureConsumer<Maliev.NotificationService.Api.Consumers.JobStatusChangedEventConsumer>(context);
            });

            // Critical notification queue — reserved for topic-routed critical priority events
            cfg.ReceiveEndpoint("notification-critical", e =>
            {
                e.Bind("maliev.notifications", s =>
                {
                    s.RoutingKey = "maliev.notification.v1.*.critical";
                    s.ExchangeType = "topic";
                });

                e.PrefetchCount = 10;
                e.ConcurrentMessageLimit = 10;

                // Retry policy: 3 attempts with exponential backoff (1s, 2s, 4s)
                e.UseMessageRetry(r => r.Intervals(1000, 2000, 4000));
            });

            // Standard notification queue — higher prefetch for batch efficiency
            cfg.ReceiveEndpoint("notification-standard", e =>
            {
                e.PrefetchCount = 50; // Higher prefetch for throughput
                e.ConcurrentMessageLimit = 50;

                e.ConfigureConsumer<Maliev.NotificationService.Api.Consumers.NotificationEventConsumer>(context);

                // Retry policy: 1 attempt with fixed 5s delay
                e.UseMessageRetry(r => r.Interval(1, 5000));
            });
        });

    // (6) Add controllers
    builder.Services.AddScoped<Maliev.NotificationService.Api.Consumers.NotificationEventConsumer>();
    builder.Services.AddControllers();

    // Add OpenAPI
    builder.AddStandardOpenApi(
        title: "MALIEV Notification Service API",
        description: "Centralized notification service for the Maliev platform. Handles multi-channel message delivery (Email, LINE, WhatsApp, SMS, Slack) with template management, priority routing, and automatic retry logic.");

    var app = builder.Build();
    var logger = app.Services.GetRequiredService<ILogger<Program>>();

    // Run database migrations and seeding asynchronously (non-blocking)
    await app.MigrateDatabaseAsync<NotificationDbContext>();

    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    var encryptionService = scope.ServiceProvider.GetRequiredService<IEncryptionService>();

    await SeedDefaultTemplatesAsync(dbContext, app.Configuration, encryptionService, logger);
    Log.DatabaseSeedingCompleted(logger);

    // Initialize metrics (non-blocking)
    InitializeMetrics(app.Services);

    // Middleware Pipeline
    app.UseStandardMiddleware();
    app.UseCors();
    app.UseRouting();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    // Map endpoints
    app.MapControllers();
    app.MapDefaultEndpoints(servicePrefix: "notification");
    app.MapApiDocumentation(servicePrefix: "notification");

    Log.ServiceStarted(logger, "Notification Service");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.HostTerminated(bootstrapLogger, ex, "Notification Service");
    throw;
}
finally
{
    loggerFactory.Dispose();
}

// <summary>
// Seeds default notification templates for common scenarios
// </summary>
static async Task SeedDefaultTemplatesAsync(
    NotificationDbContext dbContext,
    IConfiguration configuration,
    IEncryptionService encryptionService,
    ILogger logger)
{
    var strategy = dbContext.Database.CreateExecutionStrategy();

    await strategy.ExecuteAsync(async () =>
    {
        using var transaction = await dbContext.Database.BeginTransactionAsync();

        try
        {
            // order-confirmed template (English, Email)
            await SeedTemplateIfNotExistsAsync(dbContext, NotificationBootstrapData.CreateCustomerOrderConfirmedEmailTemplate());

            // order-confirmed template (Thai, Email)
            await SeedTemplateIfNotExistsAsync(dbContext, new Maliev.NotificationService.Domain.Entities.NotificationTemplate
            {
                TemplateKey = "order-confirmed",
                DisplayName = "ยืนยันคำสั่งซื้อ",
                Version = 1,
                Language = "th",
                ChannelType = "email",
                SubjectTemplate = "ยืนยันคำสั่งซื้อ #{{orderId}}",
                ContentTemplate = "สวัสดีค่ะ คุณ{{name}}\n\nคำสั่งซื้อหมายเลข #{{orderId}} ของคุณได้รับการยืนยันแล้ว!\n\nยอดรวม: {{amount}}\n\nขอบคุณที่ใช้บริการ",
                Parameters = new[] { "name", "orderId", "amount" }
            });

            // payment-failed template (English, Email)
            await SeedTemplateIfNotExistsAsync(dbContext, new Maliev.NotificationService.Domain.Entities.NotificationTemplate
            {
                TemplateKey = "payment-failed",
                DisplayName = "Payment failed",
                Version = 1,
                Language = "en",
                ChannelType = "email",
                SubjectTemplate = "Payment failed",
                ContentTemplate = "Hello {{name}},\n\nYour payment of {{amount}} has failed.\n\nReason: {{reason}}\n\nPlease update your payment method and try again.",
                Parameters = new[] { "name", "amount", "reason" }
            });

            // payment-failed template (Thai, Email)
            await SeedTemplateIfNotExistsAsync(dbContext, new Maliev.NotificationService.Domain.Entities.NotificationTemplate
            {
                TemplateKey = "payment-failed",
                DisplayName = "การชำระเงินล้มเหลว",
                Version = 1,
                Language = "th",
                ChannelType = "email",
                SubjectTemplate = "การชำระเงินล้มเหลว",
                ContentTemplate = "สวัสดีค่ะ คุณ{{name}}\n\nการชำระเงินจำนวน {{amount}} ของคุณล้มเหลว\n\nเหตุผล: {{reason}}\n\nกรุณาอัพเดทวิธีการชำระเงินและลองใหม่อีกครั้ง",
                Parameters = new[] { "name", "amount", "reason" }
            });

            await SeedTemplateIfNotExistsAsync(dbContext, NotificationBootstrapData.CreateCustomerPaymentCancelledEmailTemplate());
            await SeedTemplateIfNotExistsAsync(dbContext, NotificationBootstrapData.CreateCustomerPaymentExpiredEmailTemplate());

            // system-outage template (English, Email)
            await SeedTemplateIfNotExistsAsync(dbContext, new Maliev.NotificationService.Domain.Entities.NotificationTemplate
            {
                TemplateKey = "system-outage",
                DisplayName = "System outage alert",
                Version = 1,
                Language = "en",
                ChannelType = "email",
                SubjectTemplate = "System outage alert: {{service}}",
                ContentTemplate = "SYSTEM OUTAGE ALERT\n\nService: {{service}}\n\nStatus: {{status}}\n\nEstimated resolution time: {{eta}}\n\nWe apologize for any inconvenience.",
                Parameters = new[] { "service", "status", "eta" }
            });

            await SeedTemplateIfNotExistsAsync(dbContext, new Maliev.NotificationService.Domain.Entities.NotificationTemplate
            {
                TemplateKey = "customer-email-follow-up",
                DisplayName = "Customer follow-up",
                Version = 1,
                Language = "en",
                ChannelType = "email",
                SubjectTemplate = "Follow-up from MALIEV",
                ContentTemplate = "Hello {{customerName}},\n\nI wanted to follow up on your request with MALIEV. Please reply with any updates or questions and our team will help from there.\n\nBest regards,\nMALIEV",
                Parameters = new[] { "customerName" }
            });

            await SeedTemplateIfNotExistsAsync(dbContext, new Maliev.NotificationService.Domain.Entities.NotificationTemplate
            {
                TemplateKey = "customer-email-document-request",
                DisplayName = "Request missing customer details",
                Version = 1,
                Language = "en",
                ChannelType = "email",
                SubjectTemplate = "Documents needed for {{companyName}}",
                ContentTemplate = "Hello {{customerName}},\n\nTo complete the customer profile for {{companyName}}, please send the missing documents or details when convenient.\n\nBest regards,\nMALIEV",
                Parameters = new[] { "customerName", "companyName" }
            });

            await SeedTemplateIfNotExistsAsync(dbContext, new Maliev.NotificationService.Domain.Entities.NotificationTemplate
            {
                TemplateKey = "customer-email-quote-follow-up",
                DisplayName = "Quote follow-up",
                Version = 1,
                Language = "en",
                ChannelType = "email",
                SubjectTemplate = "Quote follow-up for {{companyName}}",
                ContentTemplate = "Hello {{customerName}},\n\nI am following up on your MALIEV quotation. Let us know if you would like us to adjust the scope, quantity, material, or delivery schedule.\n\nBest regards,\nMALIEV",
                Parameters = new[] { "customerName", "companyName" }
            });

            await SeedTemplateIfNotExistsAsync(dbContext, NotificationBootstrapData.CreateContactMessageSubmittedEmailTemplate());
            await SeedTemplateIfNotExistsAsync(dbContext, NotificationBootstrapData.CreateContactMessageCustomerCopyEmailTemplate());
            await SeedTemplateIfNotExistsAsync(dbContext, NotificationBootstrapData.CreateContactMessageEmployeeReplyEmailTemplate());
            await SeedTemplateIfNotExistsAsync(dbContext, NotificationBootstrapData.CreateCustomerWelcomeGoogleEmailTemplate());
            await SeedTemplateIfNotExistsAsync(dbContext, NotificationBootstrapData.CreateCustomerWelcomeEmailEmailTemplate());
            await SeedTemplateIfNotExistsAsync(dbContext, NotificationBootstrapData.CreateCustomerEmailVerifiedEmailTemplate());
            await SeedTemplateIfNotExistsAsync(dbContext, NotificationBootstrapData.CreateOperationsPaymentReceivedEmailTemplate());
            await SeedTemplateIfNotExistsAsync(dbContext, NotificationBootstrapData.CreateOperationsJobCompletedQcReadyEmailTemplate());
            await SeedTemplateIfNotExistsAsync(dbContext, NotificationBootstrapData.CreateCustomerOrderCompletedEmailTemplate());
            await SeedTemplateIfNotExistsAsync(dbContext, NotificationBootstrapData.CreateCustomerOrderCompletionFailedEmailTemplate());
            await SeedTemplateIfNotExistsAsync(dbContext, NotificationBootstrapData.CreateCustomerOrderShippedEmailTemplate());
            await SeedContactInboxAsync(dbContext, configuration, encryptionService);
            await SeedOperationsInboxAsync(dbContext, configuration, encryptionService);

            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            Log.TemplatesSeeded(logger);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Log.TemplatesSeededError(logger, ex);
            throw;
        }
    });
}

// <summary>
// Seeds the website contact inbox notification route if missing.
// </summary>
static async Task SeedContactInboxAsync(
    NotificationDbContext dbContext,
    IConfiguration configuration,
    IEncryptionService encryptionService)
{
    var preference = await dbContext.UserNotificationPreferences
        .FirstOrDefaultAsync(p => p.UserId == NotificationBootstrapData.ContactInboxUserId);

    if (preference == null)
    {
        dbContext.UserNotificationPreferences.Add(NotificationBootstrapData.CreateContactInboxPreference());
    }
    else
    {
        preference.PrimaryChannelType = "email";
        preference.FallbackChannelTypes = [];
        preference.UpdatedAt = DateTimeOffset.UtcNow;
    }

    var inboxEmail = NotificationBootstrapData.ResolveContactInboxEmail(configuration);
    var binding = await dbContext.ChannelBindings
        .FirstOrDefaultAsync(b =>
            b.UserId == NotificationBootstrapData.ContactInboxUserId &&
            b.ChannelType == "email");

    if (binding == null)
    {
        dbContext.ChannelBindings.Add(
            NotificationBootstrapData.CreateContactInboxEmailBinding(encryptionService.Encrypt(inboxEmail)));
        return;
    }

    if (ShouldUpdateContactInboxBinding(binding, inboxEmail, encryptionService))
    {
        binding.ChannelIdentifier = encryptionService.Encrypt(inboxEmail);
        binding.IsValid = true;
        binding.InvalidatedAt = null;
        binding.InvalidatedReason = null;
        binding.UpdatedAt = DateTimeOffset.UtcNow;
    }
}

// <summary>
// Determines whether the seeded contact inbox binding needs repair or recipient refresh.
// </summary>
static bool ShouldUpdateContactInboxBinding(
    Maliev.NotificationService.Domain.Entities.ChannelBinding binding,
    string inboxEmail,
    IEncryptionService encryptionService)
{
    if (!binding.IsValid)
    {
        return true;
    }

    try
    {
        return !string.Equals(encryptionService.Decrypt(binding.ChannelIdentifier), inboxEmail, StringComparison.OrdinalIgnoreCase);
    }
    catch (InvalidOperationException)
    {
        return true;
    }
}

// <summary>
// Seeds the employee operations inbox notification route if missing.
// </summary>
static async Task SeedOperationsInboxAsync(
    NotificationDbContext dbContext,
    IConfiguration configuration,
    IEncryptionService encryptionService)
{
    var preference = await dbContext.UserNotificationPreferences
        .FirstOrDefaultAsync(p => p.UserId == NotificationBootstrapData.OperationsInboxUserId);

    if (preference == null)
    {
        dbContext.UserNotificationPreferences.Add(NotificationBootstrapData.CreateOperationsInboxPreference());
    }
    else
    {
        preference.PrimaryChannelType = "email";
        preference.FallbackChannelTypes = [];
        preference.UpdatedAt = DateTimeOffset.UtcNow;
    }

    var inboxEmail = NotificationBootstrapData.ResolveOperationsInboxEmail(configuration);
    var binding = await dbContext.ChannelBindings
        .FirstOrDefaultAsync(b =>
            b.UserId == NotificationBootstrapData.OperationsInboxUserId &&
            b.ChannelType == "email");

    if (binding == null)
    {
        dbContext.ChannelBindings.Add(
            NotificationBootstrapData.CreateOperationsInboxEmailBinding(encryptionService.Encrypt(inboxEmail)));
        return;
    }

    if (ShouldUpdateContactInboxBinding(binding, inboxEmail, encryptionService))
    {
        binding.ChannelIdentifier = encryptionService.Encrypt(inboxEmail);
        binding.IsValid = true;
        binding.InvalidatedAt = null;
        binding.InvalidatedReason = null;
        binding.UpdatedAt = DateTimeOffset.UtcNow;
    }
}

// <summary>
// Seeds a template if it doesn't already exist
// </summary>
static async Task SeedTemplateIfNotExistsAsync(
    NotificationDbContext dbContext,
    Maliev.NotificationService.Domain.Entities.NotificationTemplate template)
{
    var exists = await dbContext.NotificationTemplates.AnyAsync(t =>
        t.TemplateKey == template.TemplateKey &&
        t.Version == template.Version &&
        t.Language == template.Language &&
        t.ChannelType == template.ChannelType);

    if (!exists)
    {
        try
        {
            dbContext.NotificationTemplates.Add(template);
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Ignore duplicate key errors from concurrent seeding
        }
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

            // Count all pending retry queue entries using AsNoTracking for efficiency
            var depth = dbContext.RetryQueueEntries.AsNoTracking().Count();

            return depth;
        }
        catch
        {
            // Return 0 if query fails (e.g., during startup before DB is ready)
            return 0;
        }
    });
}

public partial class Program
{
    internal static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Starting {ServiceName} host")]
        public static partial void StartingHost(ILogger logger, string serviceName);

        [LoggerMessage(Level = LogLevel.Critical, Message = "{ServiceName} host terminated unexpectedly during startup")]
        public static partial void HostTerminated(ILogger logger, Exception ex, string serviceName);

        [LoggerMessage(Level = LogLevel.Information, Message = "{ServiceName} started successfully")]
        public static partial void ServiceStarted(ILogger logger, string serviceName);

        [LoggerMessage(Level = LogLevel.Information, Message = "Database seeding completed successfully")]
        public static partial void DatabaseSeedingCompleted(ILogger logger);

        [LoggerMessage(Level = LogLevel.Information, Message = "Default templates seeded successfully")]
        public static partial void TemplatesSeeded(ILogger logger);

        [LoggerMessage(Level = LogLevel.Error, Message = "Error seeding default templates")]
        public static partial void TemplatesSeededError(ILogger logger, Exception ex);
    }
}
