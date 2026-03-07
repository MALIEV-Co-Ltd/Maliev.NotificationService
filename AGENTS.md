# Maliev.NotificationService Agent Guidelines

This document provides essential instructions for AI agents working on the `Maliev.NotificationService` repository. Follow these guidelines strictly to maintain code quality and architectural integrity.

## 1. Build, Test, and Lint Commands

The project uses .NET 10.0 and enforces strict quality gates.

- **Build**:
  ```bash
  dotnet build
  ```
  *Note: `TreatWarningsAsErrors` is enabled. All warnings must be resolved.*

- **Run All Tests**:
  ```bash
  dotnet test
  ```
  *Note: Integration tests use Testcontainers and require Docker.*

- **Run Single Test**:
  ```bash
  dotnet test --filter "FullyQualifiedName~Namespace.ClassName.MethodName"
  ```
  *Example*: `dotnet test --filter "FullyQualifiedName~Maliev.NotificationService.Tests.Integration.TemplatesControllerTests.CreateTemplate_ValidRequest_ReturnsCreated"`

- **Lint/Format**:
  ```bash
  dotnet format
  ```

## 2. Code Style & Conventions

### General
- **Framework**: .NET 10.0 (C# 13).
- **Nullable Reference Types**: Enabled globally. Handle nulls explicitly.
- **Async/Await**: Use `async Task` for I/O-bound operations. Always accept and pass `CancellationToken`.
- **Documentation**: Public methods and properties **must** have XML documentation (`///`).
- **Namespaces**: Use file-scoped namespaces (e.g., `namespace Maliev.NotificationService.Api.Services;`).

### Banned Libraries (Strict)
- ❌ **AutoMapper**: Use manual mapping (extension methods or `ToEntity`/`ToResponse` methods).
- ❌ **FluentValidation**: Use standard Data Annotations (`[Required]`, `[EmailAddress]`) on DTOs.
- ❌ **FluentAssertions**: Use standard xUnit `Assert` methods only.
- ❌ **In-memory EF Core DB**: Use **Testcontainers** with real PostgreSQL 18 for integration tests.

### Architecture Patterns
- **Dependency Injection**: Use constructor injection.
- **Logging**:
  - Inject `ILogger<T>`.
  - Use `[LoggerMessage]` source generator for high-performance logging in hot paths (see `Program.cs` for examples).
- **Configuration**: Inject configuration via `IOptions<T>`. Never access `IConfiguration` directly in services.
- **Secrets**:
  - Never hardcode secrets. Use environment variables in production.
  - For local development, use **.NET User Secrets**. See `README.md` for a comprehensive list of required secret keys for notification providers.
- **Permissions**: Use `[RequirePermission("notification.{resource}.{action}")]` attributes on controllers.

### Naming Conventions
- **Classes/Interfaces**: PascalCase (`DeduplicationService`, `IDeduplicationService`).
- **Methods**: PascalCase (`IsDuplicateAsync`).
- **Variables/Parameters**: camelCase (`eventId`, `dbContext`).
- **Private Fields**: `_camelCase` (`_logger`, `_dbContext`).
- **Constants**: PascalCase (`CacheTtlHours`).

### Error Handling
- Fail fast.
- Use global exception handling middleware for unhandled exceptions.
- For recoverable errors (e.g., external API failures), use `try/catch` and log specific errors.
- Use `Result` pattern or specific return types where appropriate, but standard Exceptions are acceptable for unexpected failures.

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

## 3. Testing Guidelines

- **Unit Tests**: Test logic in isolation. Mock dependencies using `Moq`.
- **Integration Tests**: Test the full stack (Controller -> DB). Use `WebApplicationFactory` and `Testcontainers`.
- **Test Naming**: `MethodName_StateUnderTest_ExpectedBehavior` (e.g., `CreateTemplate_DuplicateKey_ReturnsConflict`).

## 4. Database (EF Core)

- Database is **PostgreSQL 18**.
- Use migrations for schema changes: `dotnet ef migrations add <MigrationName> --project Maliev.NotificationService.Infrastructure --startup-project Maliev.NotificationService.Infrastructure`.
- **No logic in DbContext**. Keep it strictly for configuration.

## 5. Environment & Infrastructure

- **Redis**: Used for caching and deduplication.
- **RabbitMQ**: Used via MassTransit for messaging.


## Database & EF Core — Mandatory Rules

### EF Core Design Package
- ❌ `Microsoft.EntityFrameworkCore.Design` MUST NOT be in Api projects
- ✅ It belongs ONLY in the Infrastructure (or Data) project where migrations live
- Migration commands must target Infrastructure as both project and startup-project (since EF Core Design package is in Infrastructure):
  ```
  dotnet ef migrations add <Name> --project Maliev.<Domain>Service.Infrastructure --startup-project Maliev.<Domain>Service.Infrastructure
  ```

### PostgreSQL xmin Concurrency — Mandatory Pattern
Use shadow property ONLY. Never add a Xmin/xmin property to domain entities.
```csharp
entity.Property<uint>("xmin").HasColumnType("xid").IsRowVersion();
```
- ❌ Never use `UseXminAsConcurrencyToken()` (removed in Npgsql EF v7)
- ❌ Never use entity property `public uint Xmin { get; set; }` or `public uint xmin { get; set; }`
- ❌ Never use `.Ignore(e => e.Xmin)` — remove the entity property instead
