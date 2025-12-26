# Data Model: Authorization Entities

## Permissions

The system defines 23 granular permissions categorized by domain:

| Domain | Permission ID | Description |
|--------|---------------|-------------|
| Templates | `notification.templates.create` | Create templates |
| Templates | `notification.templates.read` | Read template details |
| Templates | `notification.templates.update` | Update templates |
| Templates | `notification.templates.delete` | Delete templates |
| Templates | `notification.templates.publish` | Publish templates |
| Templates | `notification.templates.test` | Test rendering |
| Notifications | `notification.notifications.send` | Send notifications |
| Notifications | `notification.notifications.read` | Read history |
| Notifications | `notification.notifications.retry` | Retry failed |
| Notifications | `notification.notifications.cancel` | Cancel pending |
| Notifications | `notification.notifications.bulk` | Send bulk |
| Bindings | `notification.bindings.create` | Create bindings |
| Bindings | `notification.bindings.read` | Read binding details |
| Bindings | `notification.bindings.update` | Update bindings |
| Bindings | `notification.bindings.delete` | Delete bindings |
| Bindings | `notification.bindings.verify` | Verify bindings |
| Bindings | `notification.bindings.list-user` | List user's bindings |
| Preferences | `notification.preferences.read` | Read preferences |
| Preferences | `notification.preferences.update` | Update preferences |
| Preferences | `notification.preferences.delete` | Reset to default |
| Preferences | `notification.preferences.read-any` | Read any user's (admin) |
| Logs | `notification.logs.read` | Read global logs |
| Logs | `notification.logs.read-user` | Read own logs |
| Logs | `notification.logs.export` | Export logs |
| Logs | `notification.logs.purge` | Purge old logs |
| System | `notification.system.configure` | System settings |
| System | `notification.system.view-stats` | View statistics |
| System | `notification.system.manage-channels` | Enable/disable channels |

## Predefined Roles

### `roles.notification.admin`
- **Description**: Full control over notification system.
- **Permissions**: All `notification.*`

### `roles.notification.manager`
- **Description**: Manage templates and view all logs.
- **Permissions**: Templates.*, Notifications.send/read/retry, Logs.read/export, System.view-stats.

### `roles.notification.sender`
- **Description**: Send notifications and view delivery status.
- **Permissions**: Notifications.send/read/retry, Templates.read, Logs.read.

### `roles.notification.user`
- **Description**: Manage own preferences and channel bindings.
- **Permissions**: Bindings.*, Preferences.read/update, Logs.read-user.
