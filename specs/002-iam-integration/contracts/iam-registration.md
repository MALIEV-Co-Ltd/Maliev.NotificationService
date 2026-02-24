# Contract: IAM Registration API

This document describes the external contract between NotificationService and the IAM Service for permission registration.

## Permission Registration

**Endpoint**: `POST /iam/v1/permissions/register`

### Request Body
```json
{
  "ServiceName": "notification",
  "Permissions": [
    {
      "PermissionId": "notification.templates.create",
      "Description": "Permission: notification.templates.create"
    }
  ]
}
```

## Role Registration

**Endpoint**: `POST /iam/v1/roles/register`

### Request Body
```json
{
  "ServiceName": "notification",
  "Roles": [
    {
      "RoleId": "roles.notification.admin",
      "Description": "Full control over notification system",
      "PermissionIds": ["notification.templates.create", ...],
      "IsCustom": false
    }
  ]
}
```