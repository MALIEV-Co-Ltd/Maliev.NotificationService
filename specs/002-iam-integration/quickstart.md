# Quickstart: Permission-Based Authorization

## Configuration

Enable the feature in `appsettings.json`:

```json
{
  "Features": {
    "PermissionBasedAuthEnabled": true
  },
  "ExternalServices": {
    "IAM": {
      "BaseUrl": "http://iam-service:8080",
      "ServiceAccountToken": "your-token"
    }
  }
}
```

## Verification

1. **Startup Logs**: Verify that permissions and roles are registered.
   ```text
   info: NotificationIAMRegistrationService[0]
         Registered 23 permissions and 4 roles with IAM
   ```

2. **Access Denied**: Try to access `/notification/v1/templates` without a valid token containing `notification.templates.read`.
   - **Expected**: `403 Forbidden`

3. **Self-Service**: Access `/notification/v1/preferences/user/{your-id}` with your own token.
   - **Expected**: `200 OK`

## Testing

Run integration tests:
```bash
dotnet test --filter Category=Authorization
```
