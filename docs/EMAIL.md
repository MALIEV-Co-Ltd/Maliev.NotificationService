# Email Integration with Brevo (formerly Sendinblue)

This guide explains how to integrate Brevo's email service with the Maliev Notification Service.

## Table of Contents
1. [Overview](#overview)
2. [Prerequisites](#prerequisites)
3. [Getting Your Brevo API Key](#getting-your-brevo-api-key)
4. [Configuration](#configuration)
5. [Testing the Integration](#testing-the-integration)
6. [Features](#features)
7. [Error Handling](#error-handling)
8. [Rate Limits](#rate-limits)
9. [Best Practices](#best-practices)
10. [Troubleshooting](#troubleshooting)

## Overview

Brevo (formerly Sendinblue) is a powerful email marketing and transactional email service provider. The Maliev Notification Service uses Brevo's API to send transactional emails reliably.

**Key Benefits:**
- Free tier with 300 emails/day
- High deliverability rates
- Built-in email templates
- Detailed analytics and tracking
- SMTP and API sending options

## Prerequisites

- A Brevo account (free or paid)
- .NET 10 runtime
- Valid sender email address (verified in Brevo)

## Getting Your Brevo API Key

### Step 1: Create a Brevo Account

1. Go to [https://www.brevo.com/](https://www.brevo.com/)
2. Click **"Sign up free"**
3. Fill in your details:
   - Email address
   - Company name
   - Password
4. Verify your email address

### Step 2: Verify Your Sender Email/Domain

1. Log into your Brevo account
2. Navigate to **Settings** → **Senders & IP**
3. Click **"Add a sender"**
4. Enter your sender email address (e.g., `noreply@your-domain.com`)
5. Complete the verification process:
   - For single emails: Click the verification link sent to that email
   - For domains: Add the provided DNS records to your domain

### Step 3: Generate an API Key

1. In the Brevo dashboard, go to **Settings** → **SMTP & API**
2. Navigate to the **API Keys** tab
3. Click **"Generate a new API key"**
4. Give it a descriptive name (e.g., "Maliev Notification Service - Production")
5. Click **"Generate"**
6. **⚠️ IMPORTANT:** Copy the API key immediately - you won't be able to see it again!
7. Store it securely (never commit it to version control)

### Step 4: Note Your Verified Sender

- Go to **Settings** → **Senders & IP**
- Note the verified sender email address
- This will be used as your `SenderEmail` in configuration

## Configuration

### Option 1: Using appsettings.json (Development Only)

⚠️ **Never commit API keys to source control!**

```json
{
  "Brevo": {
    "ApiKey": "<your-token-here>",
    "SenderEmail": "noreply@your-domain.com",
    "SenderName": "Maliev Notifications"
  }
}
```

### Option 2: Using Environment Variables (Recommended for Production)

```bash
# Linux/Mac
export Brevo__ApiKey="<your-token-here>"
export Brevo__SenderEmail="noreply@your-domain.com"
export Brevo__SenderName="Maliev Notifications"

# Windows (PowerShell)
$env:Brevo__ApiKey="<your-token-here>"
$env:Brevo__SenderEmail="noreply@your-domain.com"
$env:Brevo__SenderName="Maliev Notifications"

# Windows (Command Prompt)
set Brevo__ApiKey=<your-token-here>
set Brevo__SenderEmail=noreply@your-domain.com
set Brevo__SenderName=Maliev Notifications
```

### Option 3: Using Azure Key Vault / AWS Secrets Manager

```csharp
// In Program.cs - Example for Azure Key Vault
builder.Configuration.AddAzureKeyVault(
    new Uri($"https://{keyVaultName}.vault.azure.net/"),
    new DefaultAzureCredential());

// Store secrets in Key Vault as:
// - Brevo--ApiKey
// - Brevo--SenderEmail
// - Brevo--SenderName
```

### Option 4: Using Docker Secrets

```yaml
# docker-compose.yml
services:
  notification-service:
    image: maliev/notification-service
    environment:
      - Brevo__ApiKey=${BREVO_API_KEY}
      - Brevo__SenderEmail=${BREVO_SENDER_EMAIL}
      - Brevo__SenderName=${BREVO_SENDER_NAME}
```

## Testing the Integration

### 1. Quick Health Check

```bash
curl -X GET https://your-api-url/notificationservice/health
```

Look for the email provider health status in the response.

### 2. Send a Test Email

```bash
curl -X POST https://your-api-url/notifications/v1/send \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <your-token-here>" \
  -d '{
    "userId": "test-user",
    "channelType": "email",
    "recipientId": "recipient@example.com",
    "subject": "Test Email from Maliev",
    "message": "<h1>Hello!</h1><p>This is a test email.</p>",
    "metadata": {
      "subject": "Test Email",
      "recipientName": "Test User"
    }
  }'
```

### 3. Check Brevo Dashboard

1. Log into your Brevo account
2. Go to **Campaigns** → **Transactional**
3. You should see your test email in the list
4. Check delivery status, opens, and clicks

## Features

### HTML Email Support

```csharp
var message = @"
<!DOCTYPE html>
<html>
<head>
    <style>
        .header { background-color: #4CAF50; color: white; padding: 20px; }
        .content { padding: 20px; }
    </style>
</head>
<body>
    <div class='header'>
        <h1>Welcome to Maliev!</h1>
    </div>
    <div class='content'>
        <p>Your order #{{orderNumber}} has been confirmed.</p>
    </div>
</body>
</html>";
```

### CC and BCC Support

```csharp
var metadata = new Dictionary<string, string>
{
    { "subject", "Order Confirmation" },
    { "cc", "manager@example.com, supervisor@example.com" },
    { "bcc", "archive@example.com" }
};
```

### Custom Sender Name

```csharp
// Configure in appsettings.json
{
  "Brevo": {
    "SenderName": "Maliev Order Team"  // Appears as the sender name
  }
}
```

## Error Handling

The Email Provider handles various error scenarios:

### Permanent Errors (No Retry)
- Invalid email format
- Blocked recipient
- Invalid API key
- Sender email not verified

### Transient Errors (Automatic Retry)
- Network timeouts
- Rate limiting (429)
- Server errors (5xx)
- Temporary service unavailability

### Example Error Response

```json
{
  "success": false,
  "errorType": "InvalidRecipient",
  "errorMessage": "Invalid email address format",
  "isRetryable": false
}
```

## Rate Limits

### Brevo Free Tier
- **300 emails per day**
- **No hourly limit**
- Rate limiting: 429 error when exceeded

### Paid Plans
- **Unlimited emails** (based on plan)
- **Higher sending rate**
- Dedicated IP available

### Handling Rate Limits

The provider automatically:
1. Detects 429 rate limit errors
2. Marks delivery as retriable
3. Uses exponential backoff for retries

## Best Practices

### 1. Email Validation

Always validate email addresses before sending:

```csharp
var validator = new EmailProvider(logger, configuration);
var validation = await validator.ValidateRecipientAsync(email, ct);

if (!validation.IsValid)
{
    // Handle invalid email
}
```

### 2. Use HTML Templates

Create reusable HTML templates for consistent branding:

```html
<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
</head>
<body style="font-family: Arial, sans-serif;">
    <!-- Your email content -->
</body>
</html>
```

### 3. Monitor Delivery

- Check Brevo dashboard regularly
- Set up webhooks for delivery events
- Monitor bounce rates and spam reports

### 4. Warm Up Your IP

If using a dedicated IP:
- Start with low volume
- Gradually increase over 2-4 weeks
- Monitor reputation scores

### 5. Handle Bounces

```csharp
// Configure webhooks in Brevo dashboard
// Settings → Webhooks → Add webhook
// URL: https://your-api/webhooks/brevo/bounce
```

## Troubleshooting

### Issue: "API key not configured" Warning

**Cause:** Brevo API key is missing or not loaded

**Solution:**
1. Check your configuration source (appsettings.json, environment variables, etc.)
2. Verify the configuration key is exactly: `Brevo:ApiKey`
3. Restart the application after adding the key

### Issue: Emails Not Sending

**Possible Causes:**
1. **Invalid API key**
   - Verify the key is correct
   - Check it hasn't been revoked in Brevo dashboard

2. **Sender not verified**
   - Go to Settings → Senders & IP
   - Verify your sender email/domain

3. **Daily limit exceeded**
   - Check your Brevo dashboard for usage stats
   - Upgrade plan if needed

4. **Recipient email invalid**
   - Validate email format
   - Check for typos

### Issue: Rate Limiting (429 Error)

**Solution:**
- Upgrade to a paid plan
- Implement request queuing
- Spread sends over time

### Issue: Low Deliverability

**Causes:**
- Poor email content (spam triggers)
- No SPF/DKIM records
- High bounce rate
- Spam complaints

**Solution:**
1. Add SPF and DKIM records to your domain
2. Use double opt-in for recipients
3. Avoid spam trigger words
4. Maintain clean recipient lists
5. Use Brevo's deliverability tools

### Issue: HTML Not Rendering

**Solution:**
- Test HTML with Brevo's preview tool
- Use inline CSS (many email clients strip `<style>` tags)
- Avoid JavaScript (not supported in emails)
- Test across multiple email clients

## Additional Resources

- [Brevo API Documentation](https://developers.brevo.com/)
- [Brevo Email Best Practices](https://help.brevo.com/hc/en-us/articles/209467485)
- [Email HTML Best Practices](https://www.campaignmonitor.com/dev-resources/guides/html-email/)
- [Brevo Status Page](https://status.brevo.com/)

## Support

### Brevo Support
- Email: support@brevo.com
- Chat: Available in dashboard (paid plans)
- Help Center: https://help.brevo.com/

### Maliev Support
- GitHub Issues: [Project Repository]
- Internal Wiki: [Link to internal documentation]
