# NotificationService Implementation Plan - Permission-Based Authorization

## Total Effort: ~14 hours (~2 days)

## Phase 1: Define Permissions & Roles (2 hours)

### Step 1.1: Create NotificationPermissions.cs
**Location**: `Maliev.NotificationService.Api/Authorization/NotificationPermissions.cs`

```csharp
public static class NotificationPermissions
{
    // Template Operations
    public const string TemplatesCreate = "notification.templates.create";
    public const string TemplatesRead = "notification.templates.read";
    public const string TemplatesUpdate = "notification.templates.update";
    public const string TemplatesDelete = "notification.templates.delete";
    public const string TemplatesPublish = "notification.templates.publish";
    public const string TemplatesTest = "notification.templates.test";

    // Notification Operations
    public const string NotificationsSend = "notification.notifications.send";
    public const string NotificationsRead = "notification.notifications.read";
    public const string NotificationsRetry = "notification.notifications.retry";
    public const string NotificationsCancel = "notification.notifications.cancel";
    public const string NotificationsBulk = "notification.notifications.bulk";

    // Channel Binding Operations
    public const string BindingsCreate = "notification.bindings.create";
    public const string BindingsRead = "notification.bindings.read";
    public const string BindingsUpdate = "notification.bindings.update";
    public const string BindingsDelete = "notification.bindings.delete";
    public const string BindingsVerify = "notification.bindings.verify";
    public const string BindingsListUser = "notification.bindings.list-user";

    // Preference Operations
    public const string PreferencesRead = "notification.preferences.read";
    public const string PreferencesUpdate = "notification.preferences.update";
    public const string PreferencesDelete = "notification.preferences.delete";
    public const string PreferencesReadAny = "notification.preferences.read-any";

    // Delivery Log Operations
    public const string LogsRead = "notification.logs.read";
    public const string LogsReadUser = "notification.logs.read-user";
    public const string LogsExport = "notification.logs.export";
    public const string LogsPurge = "notification.logs.purge";

    // System Operations
    public const string SystemConfigure = "notification.system.configure";
    public const string SystemViewStats = "notification.system.view-stats";
    public const string SystemManageChannels = "notification.system.manage-channels";

    public static readonly string[] All = new[]
    {
        TemplatesCreate, TemplatesRead, TemplatesUpdate, TemplatesDelete, TemplatesPublish, TemplatesTest,
        NotificationsSend, NotificationsRead, NotificationsRetry, NotificationsCancel, NotificationsBulk,
        BindingsCreate, BindingsRead, BindingsUpdate, BindingsDelete, BindingsVerify, BindingsListUser,
        PreferencesRead, PreferencesUpdate, PreferencesDelete, PreferencesReadAny,
        LogsRead, LogsReadUser, LogsExport, LogsPurge,
        SystemConfigure, SystemViewStats, SystemManageChannels
    };
}
```

### Step 1.2: Create NotificationPredefinedRoles.cs
**Location**: `Maliev.NotificationService.Api/Authorization/NotificationPredefinedRoles.cs`

```csharp
public static class NotificationPredefinedRoles
{
    public static readonly RoleRegistration Admin = new()
    {
        RoleId = "notification-admin",
        RoleName = "Notification Administrator",
        Description = "Full control over notification system",
        Permissions = NotificationPermissions.All
    };

    public static readonly RoleRegistration Manager = new()
    {
        RoleId = "notification-manager",
        RoleName = "Notification Manager",
        Description = "Manage templates and view all logs",
        Permissions = new[]
        {
            NotificationPermissions.TemplatesCreate,
            NotificationPermissions.TemplatesRead,
            NotificationPermissions.TemplatesUpdate,
            NotificationPermissions.TemplatesDelete,
            NotificationPermissions.TemplatesPublish,
            NotificationPermissions.TemplatesTest,
            NotificationPermissions.NotificationsSend,
            NotificationPermissions.NotificationsRead,
            NotificationPermissions.NotificationsRetry,
            NotificationPermissions.LogsRead,
            NotificationPermissions.LogsExport,
            NotificationPermissions.SystemViewStats
        }
    };

    public static readonly RoleRegistration Sender = new()
    {
        RoleId = "notification-sender",
        RoleName = "Notification Sender",
        Description = "Send notifications and view delivery status",
        Permissions = new[]
        {
            NotificationPermissions.NotificationsSend,
            NotificationPermissions.NotificationsRead,
            NotificationPermissions.NotificationsRetry,
            NotificationPermissions.TemplatesRead,
            NotificationPermissions.LogsRead
        }
    };

    public static readonly RoleRegistration User = new()
    {
        RoleId = "notification-user",
        RoleName = "Notification User",
        Description = "Manage own preferences and channel bindings",
        Permissions = new[]
        {
            NotificationPermissions.BindingsCreate,
            NotificationPermissions.BindingsRead,
            NotificationPermissions.BindingsUpdate,
            NotificationPermissions.BindingsDelete,
            NotificationPermissions.BindingsVerify,
            NotificationPermissions.BindingsListUser,
            NotificationPermissions.PreferencesRead,
            NotificationPermissions.PreferencesUpdate,
            NotificationPermissions.LogsReadUser
        }
    };

    public static readonly RoleRegistration[] All = new[]
    {
        Admin, Manager, Sender, User
    };
}
```

## Phase 2: IAM Registration (2 hours)

### Step 2.1: Create NotificationIAMRegistrationService.cs
**Location**: `Maliev.NotificationService.Api/Services/NotificationIAMRegistrationService.cs`

```csharp
public class NotificationIAMRegistrationService : IHostedService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<NotificationIAMRegistrationService> _logger;
    private readonly IConfiguration _configuration;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var iamEnabled = _configuration.GetValue<bool>("Features:PermissionBasedAuthEnabled");
        if (!iamEnabled)
        {
            _logger.LogInformation("IAM registration skipped (PermissionBasedAuthEnabled=false)");
            return;
        }

        var client = _httpClientFactory.CreateClient("IAMService");

        // Register permissions
        await client.PostAsJsonAsync("/api/v1/permissions/register", new
        {
            ServiceName = "NotificationService",
            Permissions = NotificationPermissions.All.Select(p => new
            {
                PermissionId = p,
                Description = $"Permission: {p}"
            })
        }, cancellationToken);

        // Register roles
        await client.PostAsJsonAsync("/api/v1/roles/register", new
        {
            ServiceName = "NotificationService",
            Roles = NotificationPredefinedRoles.All
        }, cancellationToken);

        _logger.LogInformation("Registered {Count} permissions and {RoleCount} roles with IAM",
            NotificationPermissions.All.Length, NotificationPredefinedRoles.All.Length);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

### Step 2.2: Register Service in Program.cs
```csharp
builder.Services.AddHostedService<NotificationIAMRegistrationService>();
```

## Phase 3: Update Controllers (4 hours)

### Step 3.1: TemplatesController.cs
```csharp
[ApiController]
[Route("api/v1/[controller]")]
public class TemplatesController : ControllerBase
{
    [HttpGet]
    [RequirePermission(NotificationPermissions.TemplatesRead)]
    public async Task<IActionResult> GetTemplates() { }

    [HttpGet("{id}")]
    [RequirePermission(NotificationPermissions.TemplatesRead)]
    public async Task<IActionResult> GetTemplate(Guid id) { }

    [HttpPost]
    [RequirePermission(NotificationPermissions.TemplatesCreate)]
    public async Task<IActionResult> CreateTemplate([FromBody] CreateTemplateRequest request) { }

    [HttpPut("{id}")]
    [RequirePermission(NotificationPermissions.TemplatesUpdate)]
    public async Task<IActionResult> UpdateTemplate(Guid id, [FromBody] UpdateTemplateRequest request) { }

    [HttpDelete("{id}")]
    [RequirePermission(NotificationPermissions.TemplatesDelete)]
    public async Task<IActionResult> DeleteTemplate(Guid id) { }

    [HttpPost("{id}/publish")]
    [RequirePermission(NotificationPermissions.TemplatesPublish)]
    public async Task<IActionResult> PublishTemplate(Guid id) { }
}
```

### Step 3.2: ChannelBindingsController.cs
```csharp
[ApiController]
[Route("api/v1/[controller]")]
public class ChannelBindingsController : ControllerBase
{
    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetUserChannelBindings(Guid userId)
    {
        // Allow users to view their own bindings without explicit permission
        var principalId = User.FindFirst("sub")?.Value;
        if (principalId != userId.ToString())
        {
            // Viewing other users' bindings requires permission
            if (!HasPermission(NotificationPermissions.BindingsListUser))
                return Forbid();
        }
        // ... implementation
    }

    [HttpPost]
    [RequirePermission(NotificationPermissions.BindingsCreate)]
    public async Task<IActionResult> CreateChannelBinding([FromBody] CreateBindingRequest request) { }

    [HttpPut("{id}")]
    [RequirePermission(NotificationPermissions.BindingsUpdate)]
    public async Task<IActionResult> UpdateChannelBinding(Guid id, [FromBody] UpdateBindingRequest request) { }

    [HttpDelete("{id}")]
    [RequirePermission(NotificationPermissions.BindingsDelete)]
    public async Task<IActionResult> DeleteChannelBinding(Guid id) { }
}
```

### Step 3.3: PreferencesController.cs
```csharp
[HttpGet("user/{userId}")]
public async Task<IActionResult> GetPreferences(Guid userId)
{
    var principalId = User.FindFirst("sub")?.Value;
    if (principalId != userId.ToString())
    {
        // Reading other users' preferences requires admin permission
        if (!HasPermission(NotificationPermissions.PreferencesReadAny))
            return Forbid();
    }
    // ... implementation
}
```

### Step 3.4: DeliveryLogsController.cs
```csharp
[HttpGet]
[RequirePermission(NotificationPermissions.LogsRead)]
public async Task<IActionResult> GetDeliveryLogs([FromQuery] LogQueryParameters query) { }

[HttpGet("export")]
[RequirePermission(NotificationPermissions.LogsExport)]
public async Task<IActionResult> ExportDeliveryLogs([FromQuery] ExportParameters parameters) { }
```

## Phase 4: Update Tests (4 hours)

### Step 4.1: Update Integration Tests
```csharp
[Fact]
public async Task CreateTemplate_WithPermission_ReturnsCreated()
{
    var response = await _client
        .WithTestAuth(NotificationPermissions.TemplatesCreate)
        .PostAsJsonAsync("/api/v1/templates", new CreateTemplateRequest { ... });

    response.StatusCode.Should().Be(HttpStatusCode.Created);
}

[Fact]
public async Task CreateTemplate_WithoutPermission_ReturnsForbidden()
{
    var response = await _client
        .WithTestAuth() // No permissions
        .PostAsJsonAsync("/api/v1/templates", new CreateTemplateRequest { ... });

    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
}

[Fact]
public async Task GetUserBindings_OwnUser_Succeeds()
{
    var userId = Guid.NewGuid();
    var response = await _client
        .WithTestAuth(principalId: userId) // No specific permission needed
        .GetAsync($"/api/v1/channelbindings/user/{userId}");

    response.StatusCode.Should().Be(HttpStatusCode.OK);
}
```

## Phase 5: Deploy & Verify (2 hours)

### Step 5.1: Deploy to Dev
1. Deploy NotificationService with `PermissionBasedAuthEnabled=false`
2. Verify service starts successfully
3. Verify IAM registration skipped

### Step 5.2: Enable IAM
1. Set `PermissionBasedAuthEnabled=true`
2. Restart service
3. Verify permissions registered with IAM

### Step 5.3: Smoke Tests
- Test template creation with admin role
- Test self-service operations (own bindings/preferences)
- Test permission denial for unauthorized operations

### Step 5.4: Monitor
- Watch for authorization errors in logs
- Check IAM service for permission resolution performance
- Verify no authorization bypass

## Critical Files

- `Maliev.NotificationService.Api/Authorization/NotificationPermissions.cs`
- `Maliev.NotificationService.Api/Authorization/NotificationPredefinedRoles.cs`
- `Maliev.NotificationService.Api/Services/NotificationIAMRegistrationService.cs`
- `Maliev.NotificationService.Api/Controllers/TemplatesController.cs`
- `Maliev.NotificationService.Api/Controllers/ChannelBindingsController.cs`
- `Maliev.NotificationService.Api/Controllers/PreferencesController.cs`
- `Maliev.NotificationService.Api/Controllers/DeliveryLogsController.cs`

## Success Checklist

- [ ] NotificationPermissions.cs created with 23 permissions
- [ ] NotificationPredefinedRoles.cs created with 4 roles
- [ ] NotificationIAMRegistrationService.cs implemented
- [ ] All 4 controllers updated with [RequirePermission]
- [ ] Self-service operations allow own-user access
- [ ] Integration tests updated and passing
- [ ] Feature flag configuration added
- [ ] Deployed to dev and verified
- [ ] IAM registration successful
- [ ] No authorization bypass vulnerabilities
