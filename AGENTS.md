# Maliev.NotificationService Agent Guidelines

This document provides essential instructions for AI agents working on the `Maliev.NotificationService` repository. Follow these guidelines strictly to maintain code quality and architectural integrity.

## 1. Build, Test & Lint Commands

All commands run from within this service directory (`B:\maliev\Maliev.NotificationService`).

```powershell
# Build (treats warnings as errors — all must be fixed)
dotnet build Maliev.NotificationService.slnx

# Run all tests
dotnet test Maliev.NotificationService.slnx --verbosity normal

# Run a single test method
dotnet test --filter "FullyQualifiedName~TemplatesControllerTests.CreateTemplate_ValidRequest_ReturnsCreated"

# Run all tests in a class
dotnet test --filter "FullyQualifiedName~TemplatesControllerTests"

# Run with code coverage
dotnet test Maliev.NotificationService.slnx --collect:"XPlat Code Coverage"

# Format check
dotnet format Maliev.NotificationService.slnx

# EF Core migrations (Infrastructure project only)
dotnet ef migrations add <Name> --project Maliev.NotificationService.Infrastructure --startup-project Maliev.NotificationService.Infrastructure
```

*Note: Integration tests use Testcontainers and require Docker.*

## 2. Code Style & Conventions

### Workspace Structure
```
Maliev.NotificationService/
├── Maliev.NotificationService.Api/           # Controllers, Consumers, Middleware
├── Maliev.NotificationService.Application/   # Use cases, DTOs, Interfaces, Handlers
├── Maliev.NotificationService.Domain/        # Entities, value objects, domain interfaces
├── Maliev.NotificationService.Infrastructure/ # EF Core DbContext, repositories, HTTP clients
├── Maliev.NotificationService.Tests/         # Unit + Integration tests (xUnit)
├── Directory.Build.props                     # Central package versioning
└── Maliev.NotificationService.slnx           # Solution file (.slnx preferred over .sln)
```

### C# Naming & Formatting
- **Namespaces**: File-scoped (`namespace Maliev.NotificationService.Api.Services;`)
- **Classes/Methods/Properties**: `PascalCase`
- **Private fields**: `_camelCase` (underscore prefix)
- **Parameters/locals**: `camelCase`
- **Async methods**: Suffix with `Async` (e.g., `ProcessAsync`)
- **Interfaces**: Prefix with `I` (e.g., `IDeduplicationService`)
- **Permissions**: GCP-style `{domain}.{plural-resource}.{action}` as `public const string` in a `Permissions` static class
  - Valid: `notification.templates.create`, `notification.channels.send`
  - Invalid: `notification.template.create` (singular), `notification.send` (missing resource)
- **XML docs**: Required on ALL public methods and properties
- **Nullable**: Enabled (`<Nullable>enable</Nullable>`). Use `?` explicitly
- **Imports**: System first, then third-party, then local. Alphabetize within groups. Remove unused `using`
- **Braces**: Allman style (new line) for methods and control structures. Expression-bodied for properties/accessors
- **Indentation**: 4 spaces, LF line endings, UTF-8, trim trailing whitespace

### C# Patterns
- **DI**: Constructor injection with `private readonly` fields
- **Controllers**: `[ApiController]`, `[ApiVersion("1")]`, `[Route("notification/v{version:apiVersion}")]`
- **Logging**: `ILogger<T>` with structured placeholders (never interpolate): `_logger.LogInformation("Processing {TemplateId}", templateId)`
  - Use `[LoggerMessage]` source generator for high-performance logging in hot paths (see `Program.cs` for examples).
- **Error handling**: Global exception middleware. Return `ProblemDetails` / `ErrorResponse` DTOs. Never expose stack traces
- **JSON**: Check existing conventions in this service for naming policy
- **Manual mapping**: Static extension methods (`ToDto()`, `ToEntity()`). AutoMapper is banned
- **Validation**: `System.ComponentModel.DataAnnotations` on DTOs. FluentValidation is banned
- **Configuration**: Inject configuration via `IOptions<T>`. Never access `IConfiguration` directly in services.
- **Secrets**: Never hardcode secrets. Use environment variables in production. For local development, use **.NET User Secrets**. See `README.md` for a comprehensive list of required secret keys for notification providers.

### Example Service Structure
```csharp
using Microsoft.Extensions.Caching.Distributed;

namespace Maliev.NotificationService.Api.Services;

/// <summary>
/// Service description here.
/// </summary>
public class ExampleService : IExampleService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<ExampleService> _logger;

    public ExampleService(IDistributedCache cache, ILogger<ExampleService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> ProcessAsync(string id, CancellationToken cancellationToken = default)
    {
        // Implementation
    }
}
```

## 3. Banned Libraries (Build Will Fail)

| Banned | Use Instead |
|--------|-------------|
| AutoMapper | Manual mapping extensions |
| FluentValidation | DataAnnotations or manual validation |
| FluentAssertions | Standard xUnit `Assert.*` |
| Swashbuckle/Swagger | Scalar (at `/notification/scalar`) |
| InMemoryDatabase (EF Core) | Testcontainers with real PostgreSQL |

## 4. Testing Rules

- **Framework**: xUnit with standard `Assert` (`Assert.Equal`, `Assert.NotNull`, etc.)
- **Naming**: `MethodName_StateUnderTest_ExpectedBehavior` or `HTTP_METHOD_Path_Scenario_ExpectedStatus`
- **Coverage**: Minimum 80% per service
- **Integration tests**: `BaseIntegrationTestFactory<TProgram, TDbContext>` with Testcontainers (PostgreSQL, Redis, RabbitMQ). Never InMemoryDatabase
- **System tests** (Tier 3): `AspireTestFixture` with `[Collection("AspireDomainTests")]` — shared AppHost, never one per class
- **Eventual consistency**: Use `TestHelpers.WaitForAsync`. Never `Task.Delay`
- **MassTransit consumers**: Must have consumer tests using `AddMassTransitTestHarness()`

### Testing Strategy (4-Tier Pyramid Context)

This service's tests cover **Tier 1 (Unit)** and **Tier 2 (Service Integration)** of the Maliev testing pyramid:

| Tier | What to Test | Infrastructure |
|------|-------------|---------------|
| **Unit** | Business logic, domain models, service methods with mocked dependencies | None (mocks only) |
| **Service Integration** | API endpoints, database persistence, permission enforcement, input validation | `BaseIntegrationTestFactory` + Testcontainers (Postgres/Redis/RabbitMQ) |

**Tier 3 (System Integration)** — cross-service workflows and event chains — is tested in `Maliev.Aspire.Tests/`.

> Full ecosystem test strategy: `Maliev.Aspire.Tests/TEST_PLAN.md`

## 5. Database (EF Core)

- Database is **PostgreSQL 18**.
- **No logic in DbContext**. Keep it strictly for configuration.

### EF Core Design Package
- `Microsoft.EntityFrameworkCore.Design` MUST NOT be in Api projects
- It belongs ONLY in the Infrastructure project where migrations live
- Migration commands target Infrastructure as both project and startup-project:
  ```
  dotnet ef migrations add <Name> --project Maliev.NotificationService.Infrastructure --startup-project Maliev.NotificationService.Infrastructure
  ```

### PostgreSQL xmin Concurrency — Mandatory Pattern
Use shadow property ONLY. Never add a Xmin/xmin property to domain entities.
```csharp
entity.Property<uint>("xmin").HasColumnType("xid").IsRowVersion();
```
- Never use `UseXminAsConcurrencyToken()` (removed in Npgsql EF v7)
- Never use entity property `public uint Xmin { get; set; }` or `public uint xmin { get; set; }`
- Never use `.Ignore(e => e.Xmin)` — remove the entity property instead

## 6. Environment & Infrastructure

- **Redis**: Used for caching and deduplication.
- **RabbitMQ**: Used via MassTransit for messaging.

## 7. Mandatory Rules

- **`TreatWarningsAsErrors = true`**: Zero warnings allowed. No suppression
- **`[RequirePermission("notification.{resources}.{action}")]`**: On all endpoints, not plain `[Authorize]`
- **API versioning**: All routes versioned (`v1/`)
- **Service prefix**: Routes prefixed with `/notification`
- **Scalar docs**: Configured at `/notification/scalar`
- **Secrets**: Never hardcoded. Use GCP Secret Manager or environment variables
- **Async/await**: All the way down. Pass `CancellationToken`
- **EF Core Design package**: Only in Infrastructure project, never in Api
- **PostgreSQL xmin**: Shadow property only — `entity.Property<uint>("xmin").HasColumnType("xid").IsRowVersion()`. Never add entity property
- **Temporary files**: Generate in `/temp` folder, clean up afterwards

## 8. Git Rules

- Each `Maliev.*` folder is an independent git repo. Work within this service directory for git commands
- **Commit early and often** after every meaningful unit of work. Do not accumulate changes
- **Never use `git checkout` to restore files** — commit first, then `git revert` or `git reset --soft`
- Feature branches merged to `develop` via PR. Do not push without being asked
