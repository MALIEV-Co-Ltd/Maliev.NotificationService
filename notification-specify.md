# NotificationService Specification - Permission-Based Authorization Migration

## Overview
NotificationService manages notification delivery across multiple channels (email, SMS, push), user notification preferences, notification templates, channel bindings, and delivery tracking.

## Permissions to Define

### Template Operations
```
notification.templates.create       - Create notification templates
notification.templates.read         - Read template details
notification.templates.update       - Update notification templates
notification.templates.delete       - Delete templates
notification.templates.publish      - Publish templates for use
notification.templates.test         - Test template rendering
```

### Notification Operations
```
notification.notifications.send     - Send notifications to users
notification.notifications.read     - Read notification history
notification.notifications.retry    - Retry failed notifications
notification.notifications.cancel   - Cancel pending notifications
notification.notifications.bulk     - Send bulk notifications
```

### Channel Binding Operations
```
notification.bindings.create        - Create channel bindings (email, SMS, push tokens)
notification.bindings.read          - Read channel binding details
notification.bindings.update        - Update channel bindings
notification.bindings.delete        - Delete channel bindings
notification.bindings.verify        - Verify channel bindings (email verification, etc.)
notification.bindings.list-user     - List user's channel bindings
```

### Preference Operations
```
notification.preferences.read       - Read user notification preferences
notification.preferences.update     - Update notification preferences
notification.preferences.delete     - Delete preferences (reset to default)
notification.preferences.read-any   - Read any user's preferences (admin)
```

### Delivery Log Operations
```
notification.logs.read              - Read delivery logs
notification.logs.read-user         - Read user's own delivery logs
notification.logs.export            - Export delivery logs
notification.logs.purge             - Purge old delivery logs (admin)
```

### System Operations
```
notification.system.configure       - Configure notification system settings
notification.system.view-stats      - View notification statistics
notification.system.manage-channels - Manage notification channels (enable/disable)
```

## Predefined Roles

### notification-admin
**Description**: Full control over notification system
**Permissions**: All notification.* permissions

### notification-manager
**Description**: Manage templates and view all logs
**Permissions**:
- templates.* (all template operations)
- notifications.send, notifications.read, notifications.retry
- logs.read, logs.export
- system.view-stats

### notification-sender
**Description**: Send notifications and view delivery status
**Permissions**:
- notifications.send
- notifications.read
- notifications.retry
- templates.read
- logs.read

### notification-user
**Description**: Manage own preferences and channel bindings
**Permissions**:
- bindings.create, bindings.read, bindings.update, bindings.delete, bindings.verify, bindings.list-user
- preferences.read, preferences.update
- logs.read-user

## Authorization Rules

### Self-Service Operations
Users can always manage their own:
- Channel bindings (bindings.list-user without permission check if requesting own)
- Preferences (if requesting own user_id)
- Delivery logs (logs.read-user for own notifications)

### Admin-Only Operations
- Template management (create, update, delete, publish)
- System configuration
- Viewing other users' preferences
- Purging delivery logs

### Cross-Service Integration
- Services can send notifications with `notification.notifications.send` permission via service accounts
- Users receive notifications based on their preferences (no permission check)

## Current Authorization Issues

The NotificationService currently has authorization gaps:
1. **No granular template permissions** - Any authenticated user can manage templates
2. **No preference protection** - Users can read/modify other users' preferences
3. **No delivery log access control** - All logs are accessible to authenticated users
4. **No admin-only operations** - System configuration and channel management lack protection

## Migration Strategy

### Phase 1: Define Permissions & Roles (2 hours)
Create:
- `NotificationPermissions.cs` - Define all 23 permissions
- `NotificationPredefinedRoles.cs` - Define 4 predefined roles

### Phase 2: IAM Registration (2 hours)
- Create `NotificationIAMRegistrationService.cs` (IHostedService)
- Register permissions and roles on startup

### Phase 3: Update Controllers (4 hours)
- **TemplatesController**: Add `[RequirePermission]` to all endpoints
- **ChannelBindingsController**: Add permission checks, allow self-service for own bindings
- **PreferencesController**: Add permission checks, allow self-service for own preferences
- **DeliveryLogsController**: Add permission checks for admin operations

### Phase 4: Update Tests (4 hours)
- Update integration tests to use `.WithTestAuth(NotificationPermissions.X)`
- Add tests for permission enforcement
- Add tests for self-service operations

### Phase 5: Deploy & Verify (2 hours)
- Deploy with feature flag `PermissionBasedAuthEnabled=false`
- Run smoke tests
- Enable feature flag
- Monitor for authorization errors

## Feature Flag Configuration

```json
{
  "Features": {
    "PermissionBasedAuthEnabled": false
  },
  "ExternalServices": {
    "IAM": {
      "BaseUrl": "http://iam-service:8080",
      "ServiceAccountToken": "<secret-from-vault>",
      "Timeout": 5000
    }
  }
}
```

## Success Criteria

- [ ] 23 permissions registered with IAM
- [ ] 4 predefined roles registered
- [ ] All controllers use `[RequirePermission]` attribute
- [ ] Self-service operations work without admin permissions
- [ ] Integration tests updated and passing
- [ ] Zero authorization bypass vulnerabilities
- [ ] Service registers permissions on startup

## Rollback Plan

1. Set `PermissionBasedAuthEnabled=false` in configuration
2. Restart service
3. Old authorization (if any) takes effect immediately
4. No database changes required

## Estimated Effort

**Total**: ~14 hours (~2 days)
- Phase 1: 2 hours
- Phase 2: 2 hours
- Phase 3: 4 hours
- Phase 4: 4 hours
- Phase 5: 2 hours

## Dependencies

- IAM Service deployed and operational
- ServiceDefaults with RequirePermissionAttribute available
- JWT tokens include permissions claim
