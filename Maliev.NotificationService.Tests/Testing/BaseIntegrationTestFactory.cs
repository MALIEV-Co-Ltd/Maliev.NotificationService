using System.IdentityModel.Tokens.Jwt;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using MassTransit;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;
using Xunit;

// Disable parallel execution to prevent race conditions on the shared singleton database
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Maliev.NotificationService.Tests.Testing;

/// <summary>
/// Base integration test factory for NotificationService.
/// Provides PostgreSQL, Redis, and RabbitMQ containers with parallel startup.
/// </summary>
/// <typeparam name="TProgram">The Program class of the service being tested</typeparam>
/// <typeparam name="TDbContext">The DbContext type for the service</typeparam>
public class BaseIntegrationTestFactory<TProgram, TDbContext> : WebApplicationFactory<TProgram>, IAsyncLifetime
    where TProgram : class
    where TDbContext : DbContext
{
    private static PostgreSqlContainer? _postgresContainer;
    private static RedisContainer? _redisContainer;
    private static RabbitMqContainer? _rabbitmqContainer;
    private static bool _containersStarted;
    private static readonly SemaphoreSlim _initLock = new(1, 1);

    private readonly RSA _testRsa;
    private readonly string _testJwtKey;
    private readonly string _testEncryptionKey;

    /// <summary>
    /// Override this property if your DbContext connection string has a different name.
    /// Defaults to the DbContext class name.
    /// </summary>
    protected virtual string DbConnectionStringName => typeof(TDbContext).Name;

    public BaseIntegrationTestFactory()
    {
        _testRsa = RSA.Create(2048);
        _testJwtKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        _testEncryptionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        // Set environment variable EARLY so Program.cs picks it up during WebApplication.CreateBuilder
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
        Environment.SetEnvironmentVariable("CORS__AllowedOrigins__0", "http://localhost:3000");
        Environment.SetEnvironmentVariable("CORS_ALLOWED_ORIGINS", "http://localhost:3000");
    }

    public async Task InitializeAsync()
    {
        await _initLock.WaitAsync();
        try
        {
            if (!_containersStarted)
            {
                _postgresContainer = new PostgreSqlBuilder().WithImage("postgres:18-alpine")
                    .Build();

                _redisContainer = new RedisBuilder().WithImage("redis:8.4-alpine")
                    .Build();

                _rabbitmqContainer = new RabbitMqBuilder().WithImage("rabbitmq:4.2-alpine")
                    .Build();



                // Start all containers in parallel

                await Task.WhenAll(

                    _postgresContainer.StartAsync(),

                    _redisContainer.StartAsync(),

                    _rabbitmqContainer.StartAsync()

                );



                // Ensure PostgreSQL is fully ready and accepting connections

                var postgresReady = false;

                var retryCount = 0;

                const int maxRetries = 60;

                while (!postgresReady && retryCount < maxRetries)

                {

                    try

                    {

                        await using var conn = new Npgsql.NpgsqlConnection(_postgresContainer.GetConnectionString());

                        await conn.OpenAsync();

                        await using var cmd = conn.CreateCommand();

                        cmd.CommandText = "SELECT 1";

                        await cmd.ExecuteScalarAsync();

                        postgresReady = true;

                    }

                    catch

                    {

                        retryCount++;

                        await Task.Delay(1000);

                    }

                }



                if (!postgresReady)

                {

                    throw new InvalidOperationException("PostgreSQL Testcontainer failed to become ready (Ping failed) after 60 seconds.");

                }



                // Wait for Redis to be ready

                using (var connection = await StackExchange.Redis.ConnectionMultiplexer.ConnectAsync(_redisContainer.GetConnectionString()))

                {

                    await connection.GetDatabase().PingAsync();

                }



                // Apply database migrations

                await ApplyMigrationsAsync();



                _containersStarted = true;

            }

        }

        finally

        {

            _initLock.Release();

        }



        // Set environment variables immediately after containers start

        Environment.SetEnvironmentVariable($"ConnectionStrings__{DbConnectionStringName}", _postgresContainer!.GetConnectionString());

        Environment.SetEnvironmentVariable("ConnectionStrings__redis", _redisContainer!.GetConnectionString());

        Environment.SetEnvironmentVariable("ConnectionStrings__rabbitmq", _rabbitmqContainer!.GetConnectionString());

    }



    public new async Task DisposeAsync()

    {

        // Explicitly stop MassTransit bus if it was started

        if (Services != null)

        {

            try

            {

                var busControl = Services.GetService<IBusControl>();

                if (busControl != null)

                {

                    await busControl.StopAsync();

                }

            }

            catch (Exception)

            {

                // Ignore errors during bus stop

            }

        }



        // Static containers are NOT disposed here to allow reuse across tests

        _testRsa.Dispose();

        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null); // Cleanup

        await base.DisposeAsync();

    }





    protected override IHost CreateHost(IHostBuilder builder)

    {

        // Ensure containers are started before creating host

        if (!_containersStarted)

        {

            InitializeAsync().GetAwaiter().GetResult();

        }



        // Export RSA public key for JWT validation in PEM format

        var publicKeyPem = _testRsa.ExportRSAPublicKeyPem();

        var publicKeyBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(publicKeyPem));

        Environment.SetEnvironmentVariable("Jwt__PublicKey", publicKeyBase64);

        Environment.SetEnvironmentVariable("Jwt:PublicKey", publicKeyBase64);



        // Allow derived classes to set environment variables if absolutely necessary

        ConfigureEnvironmentVariables();



        return base.CreateHost(builder);

    }



    protected override void ConfigureWebHost(IWebHostBuilder builder)

    {

        // Use UseSetting to provide configuration early enough for Program.cs

        builder.UseSetting($"ConnectionStrings:{DbConnectionStringName}", _postgresContainer!.GetConnectionString());

        builder.UseSetting("ConnectionStrings:redis", _redisContainer!.GetConnectionString());

        builder.UseSetting("ConnectionStrings:rabbitmq", _rabbitmqContainer!.GetConnectionString());

        builder.UseSetting("ASPNETCORE_ENVIRONMENT", "Testing");

        builder.UseSetting("IAM:BaseUrl", "http://localhost:8080");

        builder.UseSetting("Jwt:SecurityKey", _testJwtKey);

        builder.UseSetting("Encryption:DataProtectionKey", _testEncryptionKey);

        builder.UseSetting("Features:PermissionBasedAuthEnabled", "true"); // IMPORTANT: Enable permission based auth for tests

        builder.UseSetting("IAM:RegistrationDelaySeconds", "0");

        builder.UseSetting("Features:FailOpenOnIAMError", "true");

        builder.UseSetting("RateLimiting:PermitLimit", "10000");

        builder.UseSetting("RateLimiting:WindowMinutes", "1");

        builder.UseSetting("Logging:EventLog:LogLevel:Default", "None");



        builder.ConfigureTestServices(services =>

{

    // Configure JWT Bearer authentication with test RSA key

    services.PostConfigureAll<Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions>(options =>

    {

        // Disable claim type mapping to keep original claim names

        options.MapInboundClaims = false;



        options.TokenValidationParameters = new TokenValidationParameters

        {

            ValidateIssuer = true,

            ValidateAudience = true,

            ValidateLifetime = true,

            ValidateIssuerSigningKey = true,

            ValidIssuer = "test-issuer",

            ValidAudience = "test-audience",

            IssuerSigningKey = new RsaSecurityKey(_testRsa),

            ClockSkew = TimeSpan.Zero, // No clock skew for tests

            NameClaimType = JwtRegisteredClaimNames.Sub, // Use "sub" claim as name identifier

            RoleClaimType = "role" // Use "role" claim for roles

        };



        // Add event to transform claims after token validation

        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents

        {

            OnTokenValidated = context =>

            {

                if (context.Principal?.Identity is ClaimsIdentity identity)

                {

                    // Add ClaimTypes.NameIdentifier claim from "sub"

                    var subClaim = identity.FindFirst(JwtRegisteredClaimNames.Sub);

                    if (subClaim != null && !identity.HasClaim(c => c.Type == ClaimTypes.NameIdentifier))

                    {

                        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, subClaim.Value));

                    }



                    // Add ClaimTypes.Role claims from "role"

                    var roleClaims = identity.FindAll("role").ToList();

                    foreach (var roleClaim in roleClaims)

                    {

                        if (!identity.HasClaim(c => c.Type == ClaimTypes.Role && c.Value == roleClaim.Value))

                        {

                            identity.AddClaim(new Claim(ClaimTypes.Role, roleClaim.Value));

                        }

                    }

                }

                return Task.CompletedTask;

            }

        };

    });



    // Add MassTransit test harness for testing message publishing/consuming

    services.AddMassTransitTestHarness();



    // Mock IIamServiceClient to avoid network calls and retries during tests

    var mockIamClient = new Moq.Mock<Maliev.Aspire.ServiceDefaults.IAM.IIamServiceClient>();

    mockIamClient.Setup(x => x.CheckPermissionAsync(Moq.It.IsAny<string>(), Moq.It.IsAny<string>(), Moq.It.IsAny<string>(), Moq.It.IsAny<System.Threading.CancellationToken>()))

        .ReturnsAsync(false); // Fallback to claims

    services.AddScoped(_ => mockIamClient.Object);



    // Allow derived classes to add additional test services

    ConfigureAdditionalServices(services);

});

    }

    /// <summary>
    /// Override this method to set additional environment variables before host creation.
    /// Called after standard environment variables are set.
    /// </summary>
    protected virtual void ConfigureEnvironmentVariables()
    {
        // Override in derived class if needed
    }

    /// <summary>
    /// Override this method to add additional test services to the DI container.
    /// </summary>
    protected virtual void ConfigureAdditionalServices(IServiceCollection services)
    {
        // Override in derived class if needed
    }

    /// <summary>
    /// Gets the DbContext from the service provider for use in tests.
    /// </summary>
    public TDbContext GetDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<TDbContext>();
    }

    /// <summary>
    /// Creates a new DbContext instance for testing (not from DI container).
    /// </summary>
    public TDbContext CreateDbContext()
    {
        var connectionString = _postgresContainer!.GetConnectionString();
        var optionsBuilder = new DbContextOptionsBuilder<TDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        return (TDbContext)Activator.CreateInstance(typeof(TDbContext), optionsBuilder.Options)!;
    }

    /// <summary>
    /// Applies all pending migrations to the test database.
    /// </summary>
    private async Task ApplyMigrationsAsync()
    {
        await using var context = CreateDbContext();
        await context.Database.MigrateAsync();
    }

    /// <summary>
    /// Cleans all data from the database while preserving schema.
    /// Queries the database schema dynamically to get all tables.
    /// </summary>
    [SuppressMessage("Security", "EF1002:Gaps in SQL queries", Justification = "Table names are retrieved from information_schema and are safe.")]
    public async Task CleanDatabaseAsync()
    {
        await using var context = CreateDbContext();

        // Get all table names from information_schema
        var tableNames = await context.Database
            .SqlQueryRaw<string>(
                @"SELECT table_name
                  FROM information_schema.tables
                  WHERE table_schema = 'public'
                  AND table_type = 'BASE TABLE'
                  AND table_name != '__EFMigrationsHistory'
                  ORDER BY table_name")
            .ToListAsync();

        // Truncate all tables (CASCADE handles foreign keys)
        foreach (var tableName in tableNames)
        {
            try
            {
                await context.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE \"{tableName}\" RESTART IDENTITY CASCADE");
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "42P01")
            {
                // Table doesn't exist - ignore this error
            }
        }
    }

    /// <summary>
    /// Alias for CleanDatabaseAsync to support different naming conventions.
    /// </summary>
    public Task ResetDatabaseAsync() => CleanDatabaseAsync();

    /// <summary>
    /// Alias for CleanDatabaseAsync to support different naming conventions.
    /// </summary>
    public Task ClearDatabaseAsync() => CleanDatabaseAsync();

    /// <summary>
    /// Clears the in-memory cache.
    /// </summary>
    public void ClearCache()
    {
        // Get IMemoryCache from services and cast to MemoryCache to access Clear()
        var memoryCache = Services.GetService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
        if (memoryCache is Microsoft.Extensions.Caching.Memory.MemoryCache cache)
        {
            cache.Compact(1.0); // Compact 100% removes all entries
        }
    }

    /// <summary>
    /// Exposes the RSA signing credentials for JWT token creation in tests.
    /// </summary>
    public SigningCredentials SigningCredentials => new SigningCredentials(new RsaSecurityKey(_testRsa), SecurityAlgorithms.RsaSha256);

    /// <summary>
    /// Creates a test JWT token for authentication in integration tests.
    /// </summary>
    /// <param name="userId">User ID to include in token</param>
    /// <param name="roles">Roles to include in token claims</param>
    /// <param name="permissions">Permissions to include in token claims</param>
    /// <param name="additionalClaims">Additional claims to include</param>
    /// <returns>JWT token string</returns>
    public string CreateTestJwtToken(
        string userId = "test-user",
        string[]? roles = null,
        string[]? permissions = null,
        Dictionary<string, string>? additionalClaims = null)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        if (roles != null)
        {
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
        }

        if (permissions != null)
        {
            foreach (var permission in permissions)
            {
                claims.Add(new Claim("permissions", permission));
            }
        }

        if (additionalClaims != null)
        {
            foreach (var (key, value) in additionalClaims)
            {
                claims.Add(new Claim(key, value));
            }
        }

        var rsaSecurityKey = new RsaSecurityKey(_testRsa);
        var signingCredentials = new SigningCredentials(rsaSecurityKey, SecurityAlgorithms.RsaSha256);

        var token = new JwtSecurityToken(
            issuer: "test-issuer",
            audience: "test-audience",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: signingCredentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Simplified JWT token generator with role parameter.
    /// Alias for CreateTestJwtToken to support different naming conventions.
    /// </summary>
    public string GenerateTestToken(string userId = "test-user", string role = "admin")
    {
        return CreateTestJwtToken(userId, new[] { role });
    }

    /// <summary>
    /// Creates an HTTP client with authenticated user and specified roles.
    /// </summary>
    public HttpClient CreateAuthenticatedClient(
        string userId = "test-user",
        string[]? roles = null,
        string[]? permissions = null,
        Dictionary<string, string>? additionalClaims = null)
    {
        var token = CreateTestJwtToken(userId, roles, permissions, additionalClaims);
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        return client;
    }
}
