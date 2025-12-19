# Slack Integration with Slack Web API

This guide explains how to integrate Slack messaging with the Maliev Notification Service using Slack Web API and chat.postMessage.

## Table of Contents
1. [Overview](#overview)
2. [Prerequisites](#prerequisites)
3. [Getting Started with Slack API](#getting-started-with-slack-api)
4. [Configuration](#configuration)
5. [Testing the Integration](#testing-the-integration)
6. [Features](#features)
7. [Message Formatting](#message-formatting)
8. [Error Handling](#error-handling)
9. [Rate Limits](#rate-limits)
10. [Best Practices](#best-practices)
11. [Troubleshooting](#troubleshooting)

## Overview

Slack Web API allows you to send messages to Slack users and channels via bot integration. Slack is the dominant workplace communication platform with over 20 million daily active users globally.

**Key Benefits:**
- Workplace standard for team communication
- Rich message formatting (blocks, attachments, markdown)
- Interactive components (buttons, menus, modals)
- Thread support for organized conversations
- File sharing and integrations
- Free and unlimited messaging

## Prerequisites

- Slack workspace (free or paid)
- Admin permissions to install apps
- Slack App with Bot Token
- .NET 10 runtime

## Getting Started with Slack API

### Step 1: Create a Slack App

1. Go to [Slack API Apps](https://api.slack.com/apps)
2. Click **Create New App**
3. Choose **From scratch**
4. Fill in app information:
   - **App Name**: Your app name (e.g., "Maliev Notifications")
   - **Pick a workspace**: Select your workspace
5. Click **Create App**

### Step 2: Configure Bot Token Scopes

1. In your app settings, go to **OAuth & Permissions** (left sidebar)
2. Scroll down to **Scopes** section
3. Under **Bot Token Scopes**, click **Add an OAuth Scope**
4. Add the following scopes:

**Required Scopes:**
- `chat:write` - Send messages to channels/users
- `chat:write.public` - Send messages to public channels without joining

**Optional but Recommended:**
- `channels:read` - View basic channel information
- `users:read` - View user information
- `im:write` - Send DMs to users
- `groups:write` - Send messages to private channels

**Example Scopes Setup:**
```
Bot Token Scopes:
✓ chat:write          - Post messages in approved channels & conversations
✓ chat:write.public   - Send messages to channels without joining
✓ users:read          - View people in a workspace
✓ im:write            - Start direct messages with people
```

### Step 3: Install App to Workspace

1. Still in **OAuth & Permissions**
2. Scroll to top and click **Install to Workspace**
3. Review permissions and click **Allow**
4. After installation, you'll see **Bot User OAuth Token**
5. Copy the token (starts with `xoxb-`)

**Example Bot Token Format:**
```
<your-token-here>
```

**Important:**
- Bot tokens start with `xoxb-`
- User tokens start with `xoxp-` (don't use these for bots)
- Keep tokens secret and secure
- Tokens don't expire unless you reinstall the app

### Step 4: Add Bot to Channels

For the bot to send messages to channels, it must be added:

**Method 1: Invite Bot to Channel (Recommended)**
1. In Slack, go to the channel
2. Type `/invite @YourBotName` in the message box
3. Press Enter
4. Bot is now a member and can send messages

**Method 2: Use chat:write.public Scope**
- With `chat:write.public` scope, bot can send to public channels without joining
- Only works for public channels, not private channels or DMs
- Simpler for broadcast notifications

**Method 3: Have Bot Join via API**
```bash
# Join channel programmatically
curl -X POST https://slack.com/api/conversations.join \
  -H "Authorization: Bearer <your-token-here>" \
  -H "Content-Type: application/json" \
  -d '{"channel":"C1234567890"}'
```

### Step 5: Get Channel IDs and User IDs

**Get Channel ID:**
1. Right-click on channel name in Slack
2. Select **View channel details**
3. Scroll down to see **Channel ID** (starts with `C`)
4. Or use channel name: `#general`, `#notifications`

**Channel ID Format:** `C` followed by alphanumeric (e.g., `C1234567890`)

**Get User ID for DMs:**
1. Right-click on user's profile picture
2. Select **View profile**
3. Click **More** (three dots) → **Copy member ID**
4. Or use API:
```bash
curl -X GET "https://slack.com/api/users.list" \
  -H "Authorization: Bearer <your-token-here>"
```

**User ID Format:** `U` followed by alphanumeric (e.g., `U1234567890`)

### Step 6: Test Your Bot

Test with curl to verify setup:

```bash
# Send to channel by ID
curl -X POST https://slack.com/api/chat.postMessage \
  -H "Authorization: Bearer <your-token-here>" \
  -H "Content-Type: application/json" \
  -d '{
    "channel": "C1234567890",
    "text": "Hello from Maliev!"
  }'

# Send to channel by name
curl -X POST https://slack.com/api/chat.postMessage \
  -H "Authorization: Bearer <your-token-here>" \
  -H "Content-Type: application/json" \
  -d '{
    "channel": "#general",
    "text": "Hello from Maliev!"
  }'

# Send DM to user
curl -X POST https://slack.com/api/chat.postMessage \
  -H "Authorization: Bearer <your-token-here>" \
  -H "Content-Type: application/json" \
  -d '{
    "channel": "U1234567890",
    "text": "Hello! This is a direct message."
  }'
```

**Expected Response:**
```json
{
  "ok": true,
  "channel": "C1234567890",
  "ts": "1503435956.000247",
  "message": {
    "text": "Hello from Maliev!",
    "username": "YourBotName",
    "bot_id": "B1234567890",
    "type": "message",
    "subtype": "bot_message",
    "ts": "1503435956.000247"
  }
}
```

## Configuration

### Option 1: Using appsettings.json (Development)

⚠️ **Never commit credentials to source control!**

```json
{
  "Slack": {
    "BotToken": "<your-token-here>"
  }
}
```

### Option 2: Using Environment Variables (Recommended)

```bash
# Linux/Mac
export Slack__BotToken="<your-token-here>"

# Windows (PowerShell)
$env:Slack__BotToken="<your-token-here>"

# Windows (Command Prompt)
set Slack__BotToken=<your-token-here>
```

### Option 3: Using Azure Key Vault

```csharp
// Store in Key Vault:
// - Slack--BotToken

// Configured automatically via Aspire ServiceDefaults
```

### Option 4: Using Docker Compose

```yaml
services:
  notification-api:
    environment:
      - Slack__BotToken=<your-token-here>
```

## Testing the Integration

### 1. Send to Channel (by ID)

```bash
curl -X POST https://your-api-url/notifications/v1/send \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <your-token-here>" \
  -d '{
    "userId": "test-user",
    "channelType": "slack",
    "recipientId": "C1234567890",
    "message": "Hello! This is a test Slack message from Maliev."
  }'
```

### 2. Send to Channel (by name)

```bash
curl -X POST https://your-api-url/notifications/v1/send \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <your-token-here>" \
  -d '{
    "userId": "test-user",
    "channelType": "slack",
    "recipientId": "#notifications",
    "message": "Deployment completed successfully! 🎉"
  }'
```

### 3. Send DM to User

```bash
curl -X POST https://your-api-url/notifications/v1/send \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <your-token-here>" \
  -d '{
    "userId": "test-user",
    "channelType": "slack",
    "recipientId": "U1234567890",
    "message": "Your report is ready for review."
  }'
```

### 4. Check Delivery

1. Check the Slack channel or user's DMs
2. Check application logs for delivery status
3. Message timestamp (ts) is returned as message ID

## Features

### Text Messages

Standard text messages with markdown formatting:

```csharp
await SendAsync(
    recipientId: "C1234567890",
    message: "Deployment to production completed successfully!",
    metadata: null,
    ct);
```

### Message Length

- Maximum: **40,000 characters** per message
- Provider truncates to 4000 chars + "..." for safety
- For longer content, consider using message threads or files

### Markdown Formatting

Slack supports markdown-like formatting:

```csharp
var message = @"*Deployment Summary*
_Status:_ ✅ Success
*Environment:* Production
`Version:` 2.1.0

• Database migrations: Complete
• Cache warmed: Yes
• Health checks: Passing

View logs: https://logs.maliev.com/deploy/12345";

await SendAsync("C1234567890", message, null, ct);
```

**Formatting Options:**
- `*bold*` → **bold**
- `_italic_` → _italic_
- `~strikethrough~` → ~~strikethrough~~
- `` `code` `` → `code`
- ``` ```code block``` ``` → code block
- `<URL|link text>` → hyperlink
- Emoji: `:tada:` → 🎉

### Channel Types

**Public Channels:**
```csharp
recipientId = "C1234567890";  // Channel ID
recipientId = "#general";     // Channel name
```

**Private Channels:**
```csharp
recipientId = "G1234567890";  // Private channel ID (starts with G)
// Bot must be invited to private channels
```

**Direct Messages (DMs):**
```csharp
recipientId = "U1234567890";  // User ID
// Opens DM automatically
```

**Multi-Person DMs / Group DMs:**
```csharp
recipientId = "D1234567890";  // DM channel ID (starts with D)
```

## Message Formatting

### Basic Text with Markdown

```csharp
var message = @"Hello! Here's your *daily summary*:

• Tasks completed: 12
• Tasks pending: 3
• Blockers: 0

_Great work today!_ :star:";

await SendAsync("#team-updates", message, null, ct);
```

### With Links

```csharp
var message = "Check out <https://maliev.com|our website> for more info!";
await SendAsync("U1234567890", message, null, ct);
```

### Mentions

```csharp
// Mention user
var message = "Hey <@U1234567890>, your report is ready!";

// Mention channel
var message = "<!channel> Deployment starting in 5 minutes";

// Mention everyone
var message = "<!everyone> Important security update";

// Mention here (active users only)
var message = "<!here> Meeting starting now";

await SendAsync("C1234567890", message, null, ct);
```

### Emoji

```csharp
var message = "Build succeeded! :white_check_mark: :tada:";
await SendAsync("#builds", message, null, ct);
```

### Code Blocks

```csharp
var message = @"Error in deployment:

```
Error: Connection timeout
at Database.Connect (line 42)
at App.Start (line 15)
```

Please investigate.";

await SendAsync("#errors", message, null, ct);
```

### Advanced: Block Kit (Extend Implementation)

For rich interactive messages, extend the provider to support Block Kit:

```json
{
  "channel": "C1234567890",
  "blocks": [
    {
      "type": "header",
      "text": {
        "type": "plain_text",
        "text": "Order #12345 Confirmed"
      }
    },
    {
      "type": "section",
      "text": {
        "type": "mrkdwn",
        "text": "*Total:* $150.00\n*Status:* Processing"
      }
    },
    {
      "type": "actions",
      "elements": [
        {
          "type": "button",
          "text": {
            "type": "plain_text",
            "text": "View Order"
          },
          "url": "https://example.com/orders/12345"
        }
      ]
    }
  ]
}
```

**Use Block Kit Builder:** https://app.slack.com/block-kit-builder

**To support Block Kit in SlackProvider:**

```csharp
// Check metadata for blocks
if (metadata?.ContainsKey("blocks") == true)
{
    var blocks = JsonSerializer.Deserialize<JsonElement>(metadata["blocks"]);
    payload = new
    {
        channel = recipientId,
        blocks = blocks,
        text = slackMessage // Fallback text for notifications
    };
}
else
{
    // Standard text message
    payload = new
    {
        channel = recipientId,
        text = slackMessage,
        mrkdwn = true
    };
}
```

## Error Handling

### Slack API Error Codes

**Permanent Errors (No Retry):**

**Authentication Errors:**
- `invalid_auth`: Bot token is invalid
  - Verify token starts with `xoxb-`
  - Check token is copied correctly
- `token_revoked`: Token was revoked (app reinstalled)
  - Get new token from OAuth & Permissions
- `account_inactive`: Workspace is inactive or suspended

**Recipient Errors:**
- `channel_not_found`: Channel ID doesn't exist
  - Verify channel ID is correct
  - Check channel wasn't deleted
- `not_in_channel`: Bot not in channel
  - Invite bot: `/invite @BotName`
  - Or use `chat:write.public` scope for public channels
- `user_not_found`: User ID doesn't exist
  - Verify user ID is correct
  - Check user is still in workspace
- `is_archived`: Channel is archived
  - Unarchive channel or use different channel

**Transient Errors (Automatic Retry):**
- `rate_limited`: Rate limit exceeded
  - Implement exponential backoff
  - Reduce message frequency
- `fatal_error`: Slack internal error
  - Automatic retry recommended

### Example Error Response

```json
{
  "ok": false,
  "error": "not_in_channel"
}
```

### Mapped to DeliveryResult

```csharp
{
  "success": false,
  "errorType": "InvalidRecipient",
  "errorMessage": "Slack API error: not_in_channel",
  "isRetryable": false
}
```

## Rate Limits

### Slack Rate Limits

**Tier 1 (Most methods including chat.postMessage):**
- **1 request per second** per channel
- Bursts allowed up to 50 messages
- Applies per channel/user, not globally

**Example:**
- Send to 10 different channels: 10 msg/sec OK
- Send to same channel: 1 msg/sec limit

**Tier 2 (General API methods):**
- Approximately **20+ requests per minute** per token
- Varies by method and workspace

**Tier 3 (Auth and team info):**
- Higher limits for non-message methods

**Tier 4 (Special methods):**
- Very high limits for real-time features

### Rate Limit Response

When rate limited, Slack returns:
```json
{
  "ok": false,
  "error": "rate_limited"
}
```

**Headers:**
```
Retry-After: 30
```

### Handling Rate Limits

**Built-in Retry:**
- Provider marks rate limit errors as retryable
- Application retries with exponential backoff

**Manual Throttling:**
```csharp
// Implement per-channel rate limiting
var rateLimiter = new Dictionary<string, DateTime>();

if (rateLimiter.TryGetValue(channelId, out var lastSent))
{
    var elapsed = DateTime.UtcNow - lastSent;
    if (elapsed < TimeSpan.FromSeconds(1))
    {
        await Task.Delay(TimeSpan.FromSeconds(1) - elapsed);
    }
}

await SendAsync(channelId, message, null, ct);
rateLimiter[channelId] = DateTime.UtcNow;
```

## Best Practices

### 1. Use Appropriate Channels

```csharp
// Good - Specific notification channel
await SendAsync("#deployment-alerts", "Production deployment started", null, ct);

// Bad - Spamming general channel
await SendAsync("#general", "Debug log: Processing record 1234", null, ct);
```

### 2. Format Messages Clearly

```csharp
// Good - Clear, structured, actionable
var message = @"🚀 *Deployment Complete*

*Environment:* Production
*Version:* v2.1.0
*Status:* ✅ Success
*Duration:* 5m 23s

<https://dashboard.maliev.com/deploys/123|View Details>";

// Bad - Unclear, unformatted
var message = "deploy done v2.1.0 success 323s";
```

### 3. Use Threads for Related Messages

Extend provider to support threads:

```csharp
// Initial message
var result = await SendAsync("C1234567890", "Starting deployment...", null, ct);
var threadTs = result.MessageId; // Message timestamp

// Reply in thread
var metadata = new Dictionary<string, string>
{
    { "thread_ts", threadTs }
};
await SendAsync("C1234567890", "Step 1: Building...", metadata, ct);
await SendAsync("C1234567890", "Step 2: Testing...", metadata, ct);
await SendAsync("C1234567890", "✅ Deployment complete!", metadata, ct);
```

**Modify SlackProvider to support threads:**
```csharp
var payload = new
{
    channel = recipientId,
    text = slackMessage,
    mrkdwn = true,
    thread_ts = metadata?.GetValueOrDefault("thread_ts") // Reply in thread
};
```

### 4. Use Appropriate Mentions Sparingly

```csharp
// Good - Targeted mention when urgent
"<@U1234567890> Urgent: Production database backup failed"

// Bad - Unnecessary @channel for routine updates
"<!channel> Daily backup completed successfully" // Don't spam
```

### 5. Customize Bot Appearance

In Slack App settings:

1. **Display Information**:
   - Upload bot icon (512x512 PNG)
   - Set bot display name
   - Add description

2. **App Home**:
   - Customize bot profile
   - Add "About" tab with instructions

### 6. Monitor Delivery

```csharp
var result = await SendAsync("C1234567890", message, null, ct);

if (result.Success)
{
    _logger.LogInformation("Message sent to Slack: {MessageId}", result.MessageId);

    // Store message timestamp for updates/deletion
    await StoreMessageTimestamp(result.MessageId);
}
else
{
    _logger.LogError("Slack delivery failed: {Error}", result.ErrorMessage);

    // Implement fallback channel
    await SendViaEmail(user.Email, message);
}
```

### 7. Implement Message Updates and Deletion

Extend provider for message management:

```csharp
// Update existing message
await UpdateMessage(channelId, messageTs, "Updated content");

// Delete message
await DeleteMessage(channelId, messageTs);
```

**Implementation:**
```csharp
public async Task<bool> UpdateMessage(string channel, string ts, string newText)
{
    var payload = new { channel, ts, text = newText };
    var response = await PostAsync("https://slack.com/api/chat.update", payload);
    return IsSuccessResponse(response);
}

public async Task<bool> DeleteMessage(string channel, string ts)
{
    var payload = new { channel, ts };
    var response = await PostAsync("https://slack.com/api/chat.delete", payload);
    return IsSuccessResponse(response);
}
```

**Required Scopes:**
- `chat:write` (already have this)

## Troubleshooting

### Issue: "not_in_channel" Error

**Cause:** Bot is not a member of the channel

**Solution:**
1. In Slack, go to the channel
2. Type `/invite @YourBotName`
3. Press Enter

**Or** add `chat:write.public` scope to send to public channels without joining (reinstall app after adding scope).

### Issue: "invalid_auth" Error

**Cause:** Bot token is incorrect or invalid

**Solution:**
1. Go to [Slack Apps](https://api.slack.com/apps)
2. Select your app
3. Go to **OAuth & Permissions**
4. Copy **Bot User OAuth Token** (starts with `xoxb-`)
5. Update configuration with correct token

**Common Mistakes:**
- Using User token (`xoxp-`) instead of Bot token (`xoxb-`)
- Extra spaces when copying token
- Token from wrong workspace

### Issue: "channel_not_found" Error

**Cause:** Channel ID is incorrect or channel was deleted

**Solution:**

**Get Correct Channel ID:**
1. Right-click channel name → **View channel details**
2. Scroll to bottom → Copy **Channel ID**
3. Should start with `C` (public) or `G` (private)

**Or use channel name:**
```csharp
recipientId = "#general"; // Works for public channels
```

### Issue: Messages Not Formatting Correctly

**Cause:** Markdown not enabled or wrong syntax

**Solution:**

**Ensure `mrkdwn` is enabled:**
```csharp
var payload = new
{
    channel = recipientId,
    text = message,
    mrkdwn = true  // Enable markdown
};
```

**Use correct Slack markdown:**
```
*bold* not **bold**
_italic_ not *italic*
~strike~ not ~~strike~~
`code` works
<URL|text> for links
```

### Issue: "rate_limited" Error

**Cause:** Sending too many messages too quickly to same channel

**Solution:**

1. **Implement throttling:**
```csharp
// Max 1 message per second per channel
await Task.Delay(1000);
```

2. **Batch notifications:**
```csharp
// Instead of 10 messages:
for (var i = 0; i < 10; i++)
    await SendAsync(channel, $"Update {i}", null, ct);

// Send one combined message:
var batched = string.Join("\n", updates);
await SendAsync(channel, batched, null, ct);
```

3. **Use threads:**
- Reply in thread instead of separate messages
- Reduces channel noise and rate limit impact

### Issue: Bot Token Keeps Getting Revoked

**Cause:** App is being reinstalled, invalidating old tokens

**Solution:**
1. Don't reinstall app unless necessary
2. When adding new scopes, reinstallation is required
3. Update configuration with new token after reinstall
4. Consider using token rotation strategy for production

### Issue: DMs Not Working

**Cause:** Missing `im:write` scope

**Solution:**
1. Go to **OAuth & Permissions**
2. Add **Bot Token Scope**: `im:write`
3. Reinstall app to workspace
4. Update Bot Token in configuration

### Issue: Can't Send to Private Channels

**Cause:** Bot not invited to private channel

**Solution:**
1. Private channels require explicit invitation
2. In private channel, type `/invite @YourBotName`
3. Bot must be member to send messages
4. `chat:write.public` scope doesn't work for private channels

## Advanced Features

### Interactive Components

Add buttons, menus, and interactive elements using Block Kit:

**Example: Approval Request**
```json
{
  "channel": "C1234567890",
  "text": "Deployment approval needed",
  "blocks": [
    {
      "type": "section",
      "text": {
        "type": "mrkdwn",
        "text": "Approve deployment to production?"
      }
    },
    {
      "type": "actions",
      "elements": [
        {
          "type": "button",
          "text": { "type": "plain_text", "text": "Approve" },
          "style": "primary",
          "action_id": "approve_deploy"
        },
        {
          "type": "button",
          "text": { "type": "plain_text", "text": "Reject" },
          "style": "danger",
          "action_id": "reject_deploy"
        }
      ]
    }
  ]
}
```

**Requires:**
- Interactivity enabled in app settings
- Request URL configured for interaction payloads
- Endpoint to handle button clicks

### Scheduled Messages

Schedule messages for future delivery:

```bash
curl -X POST https://slack.com/api/chat.scheduleMessage \
  -H "Authorization: Bearer <your-token-here>" \
  -d '{
    "channel": "C1234567890",
    "text": "Reminder: Standup in 5 minutes",
    "post_at": 1562180400
  }'
```

**Required Scope:** `chat:write`

### File Uploads

Share files and images:

```bash
curl -X POST https://slack.com/api/files.upload \
  -H "Authorization: Bearer <your-token-here>" \
  -F "channels=C1234567890" \
  -F "file=@report.pdf" \
  -F "initial_comment=Here's the quarterly report"
```

**Required Scope:** `files:write`

### Webhooks for Events

Receive events when users interact:

1. Enable **Event Subscriptions** in app settings
2. Set **Request URL** to your endpoint
3. Subscribe to events:
   - `message.channels` - Messages in channels
   - `message.im` - DMs to bot
   - `app_mention` - Bot mentions

**Example Webhook Payload:**
```json
{
  "type": "event_callback",
  "event": {
    "type": "message",
    "channel": "C1234567890",
    "user": "U1234567890",
    "text": "Hello bot!",
    "ts": "1234567890.123456"
  }
}
```

## Comparison: Slack vs Discord vs Microsoft Teams

| Feature | Slack | Discord | Microsoft Teams |
|---------|-------|---------|-----------------|
| **Primary Use** | Workplace | Gaming/Community | Enterprise |
| **Cost** | Free + Paid | Free + Nitro | Microsoft 365 |
| **Message Limit** | 40,000 chars | 2,000 chars | ~28,000 chars |
| **API Complexity** | Medium | Low | High |
| **Rate Limits** | 1/sec/channel | 5/sec/bot | Varies |
| **Rich Formatting** | ✅ Block Kit | ✅ Embeds | ✅ Cards |
| **Best For** | Team notifications | Community alerts | Office 365 orgs |

## Additional Resources

### Official Documentation
- [Slack API Documentation](https://api.slack.com/)
- [Web API Reference](https://api.slack.com/web)
- [Block Kit Builder](https://app.slack.com/block-kit-builder)
- [Slack App Management](https://api.slack.com/apps)

### Useful Endpoints
- Send message: `POST https://slack.com/api/chat.postMessage`
- Update message: `POST https://slack.com/api/chat.update`
- Delete message: `POST https://slack.com/api/chat.delete`
- Test auth: `GET https://slack.com/api/auth.test`
- List channels: `GET https://slack.com/api/conversations.list`
- List users: `GET https://slack.com/api/users.list`

### Testing Tools
- [Slack API Tester](https://api.slack.com/methods/chat.postMessage/test) - Test API calls in browser
- [Block Kit Builder](https://app.slack.com/block-kit-builder) - Design rich messages
- Postman Collection - Available in Slack API docs

## Support

### Slack Support
- API Documentation: https://api.slack.com/
- Community Forum: https://api.slack.com/community
- Help Center: https://slack.com/help

### Maliev Support
- GitHub Issues: [Project Repository]
- Internal Wiki: [Link to internal documentation]
