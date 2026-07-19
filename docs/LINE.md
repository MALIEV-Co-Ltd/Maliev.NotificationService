# LINE Integration with LINE Messaging API

This guide explains how to integrate LINE Official Account messaging with the Maliev Notification Service using LINE Messaging API.

## Table of Contents
1. [Overview](#overview)
2. [Prerequisites](#prerequisites)
3. [Getting Started with LINE Messaging API](#getting-started-with-line-messaging-api)
4. [Configuration](#configuration)
5. [Testing the Integration](#testing-the-integration)
6. [Features](#features)
7. [Message Types](#message-types)
8. [Error Handling](#error-handling)
9. [Rate Limits](#rate-limits)
10. [Best Practices](#best-practices)
11. [Troubleshooting](#troubleshooting)

## Overview

LINE Messaging API allows you to send messages to LINE users via LINE Official Accounts. It's extremely popular in Japan, Taiwan, Thailand, and other Asian markets with over 200 million active users.

**Key Benefits:**
- Dominant messaging app in Asian markets
- Rich message types (text, images, templates, Flex Messages)
- Two-way conversations with webhooks
- Free to send messages (pay for premium features)
- Interactive UI components (buttons, carousels, quick replies)

## Prerequisites

- A LINE Official Account (free or premium)
- LINE Developers account
- Channel Access Token from LINE Developers Console
- .NET 10 runtime

## Getting Started with LINE Messaging API

### Step 1: Create LINE Official Account

1. Go to [LINE Official Account Manager](https://manager.line.biz/)
2. Click **Create Account** or **Create Official Account**
3. Fill in account information:
   - **Account name**: Your business/service name
   - **Category**: Select appropriate category
   - **Description**: Describe your service
   - **Profile image**: Upload your logo/icon
4. Choose account type:
   - **Unverified Account** (Free): Suitable for most use cases
   - **Verified Account** (Premium): Requires business verification, better branding
5. Click **Create**

### Step 2: Create a Messaging API Channel

1. In LINE Official Account Manager, select your account
2. Go to **Settings** → **Messaging API**
3. Scroll down and click **Enable Messaging API** (if not already enabled)
4. You'll be redirected to **LINE Developers Console**
5. Or go directly to [LINE Developers Console](https://developers.line.biz/console/)

### Step 3: Get Channel Access Token

1. In LINE Developers Console, select your Provider and Channel
2. Go to **Messaging API** tab
3. Scroll to **Channel access token**
4. Click **Issue** button to generate a long-lived channel access token
5. Copy the token (starts with something like `eyJhbGciOiJI...` or similar)
   - This token does NOT expire unless you reissue it
   - Keep it secret and secure

**Example Channel Access Token Format:**
```
<your-token-here>
```

### Step 4: Get Your LINE User ID (for Testing)

To test sending messages, you need a LINE user ID:

1. Add your official account as a friend in LINE app:
   - Scan the QR code shown in LINE Official Account Manager
   - Or search by Official Account ID (starts with @)
2. Send a message from your LINE app to the official account
3. Use LINE Bot Designer or webhook to capture the user ID:

**Method 1: Using LINE Bot Designer (Easiest)**
1. Go to LINE Developers Console → **Messaging API** tab
2. Enable **Use webhooks**
3. Set **Webhook URL** to a temporary service like https://webhook.site/
4. Click **Verify** to test the webhook
5. Send a message from your LINE app to the official account
6. Check webhook.site - you'll see the user ID in the request

**Method 2: Using Webhook Events**
The webhook will receive events like this when a user sends a message:
```json
{
  "events": [{
    "type": "message",
    "source": {
      "type": "user",
      "userId": "Udeadbeefcafe1234567890abcdef12"
    },
    "message": {
      "type": "text",
      "text": "Hello"
    }
  }]
}
```

**LINE User ID Format:** Starts with `U` followed by 32 hexadecimal characters
- Example: `Udeadbeefcafe1234567890abcdef12`
- Example: `U4af4980629...` (actual IDs are longer)

### Step 5: Configure Messaging API Settings

1. In LINE Developers Console → **Messaging API** tab:
   - **Allow bot to join group chats**: Enable if needed
   - **Auto-reply messages**: **Disable** (unless you want LINE's default replies)
   - **Greeting messages**: Configure or disable as needed
   - **Webhooks**: Enable if you want to receive user messages

2. Important: **Disable Auto-Reply** to avoid duplicate responses:
   - Go to LINE Official Account Manager → **Settings** → **Response settings**
   - Set **Auto-response** to **Off**
   - Set **Greeting message** as needed

## Configuration

### Option 1: Using appsettings.json (Development)

⚠️ **Never commit credentials to source control!**

```json
{
  "LINE": {
    "ChannelAccessToken": "<your-token-here>"
  }
}
```

### Option 2: Using Environment Variables (Recommended)

```bash
# Linux/Mac
export LINE__ChannelAccessToken="<your-token-here>"

# Windows (PowerShell)
$env:LINE__ChannelAccessToken="<your-token-here>"

# Windows (Command Prompt)
set LINE__ChannelAccessToken=<your-token-here>
```

### Option 3: Using Azure Key Vault

```csharp
// Store in Key Vault:
// - LINE--ChannelAccessToken

// Configured automatically via Aspire ServiceDefaults
```

### Option 4: Using Docker Compose

```yaml
services:
  notification-api:
    environment:
      - LINE__ChannelAccessToken=<your-token-here>
```

## Testing the Integration

### 1. Get Your LINE User ID

Follow **Step 4** above to get your LINE user ID from webhook events.

### 2. Send a Test Message

```bash
curl -X POST https://your-api-url/notifications/v1/send \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <your-token-here>" \
  -d '{
    "userId": "test-user",
    "channelType": "line",
    "recipientId": "Udeadbeefcafe1234567890abcdef12",
    "message": "Hello! This is a test LINE message from Maliev."
  }'
```

**Important:**
- RecipientId must be a valid LINE user ID (starts with `U` + 32 hex chars)
- User must have added your official account as a friend
- Channel Access Token must be valid

### 3. Check Delivery

1. Check your LINE app - you should receive the message from your official account
2. Check application logs for delivery status
3. In LINE Developers Console: **Messaging API** → **Statistics** to see message counts

## Features

### Text Messages

Standard text messages with up to 5000 characters:

```csharp
await SendAsync(
    recipientId: "Udeadbeefcafe1234567890abcdef12",
    message: "Your order #12345 has been confirmed!",
    metadata: null,
    ct);
```

### Message Length

- Maximum: **5000 characters** per message
- Provider automatically truncates to 4997 chars + "..." if exceeded
- For longer content, split into multiple messages

### Emoji Support

LINE supports Unicode emoji:

```csharp
var message = "Your order is confirmed! 🎉\nThank you for shopping with us! 😊";
await SendAsync(recipientId, message, null, ct);
```

### LINE Emoji

Use LINE's built-in emoji via special format:
```
$ followed by emoji code
Example: "Hello $(100)!"
```
(Note: This requires using template/flex messages for full support)

## Message Types

LINE Messaging API supports multiple message types. This implementation currently supports **text messages**, but you can extend it:

### 1. Text Message (Currently Implemented)

```json
{
  "type": "text",
  "text": "Hello, World!"
}
```

### 2. Image Message (Extend Implementation)

```json
{
  "type": "image",
  "originalContentUrl": "https://example.com/image.jpg",
  "previewImageUrl": "https://example.com/preview.jpg"
}
```

### 3. Template Message (Buttons, Confirm, Carousel)

```json
{
  "type": "template",
  "altText": "This is a button template",
  "template": {
    "type": "buttons",
    "text": "Please select",
    "actions": [
      {
        "type": "message",
        "label": "Yes",
        "text": "Yes"
      },
      {
        "type": "message",
        "label": "No",
        "text": "No"
      }
    ]
  }
}
```

### 4. Flex Message (Custom UI Layouts)

Rich, customizable message layouts:
- Use [LINE Flex Message Simulator](https://developers.line.biz/flex-simulator/) to design
- JSON-based layout system
- Support for complex UI elements

```json
{
  "type": "flex",
  "altText": "Order confirmation",
  "contents": {
    "type": "bubble",
    "body": {
      "type": "box",
      "layout": "vertical",
      "contents": [
        {
          "type": "text",
          "text": "Order Confirmed",
          "weight": "bold",
          "size": "xl"
        }
      ]
    }
  }
}
```

### Extending for Rich Messages

To send image/template/flex messages, modify `LineProvider.SendAsync`:

```csharp
// Instead of hardcoded text message:
var pushMessageRequest = new
{
    to = recipientId,
    messages = new[]
    {
        new
        {
            type = "text",
            text = lineMessage
        }
    }
};

// Support custom message types from metadata:
object[] messages;
if (metadata?.ContainsKey("messageType") == true && metadata["messageType"] == "flex")
{
    // Parse Flex Message JSON from metadata["flexContent"]
    messages = JsonSerializer.Deserialize<object[]>(metadata["flexContent"]);
}
else
{
    // Default text message
    messages = new[]
    {
        new
        {
            type = "text",
            text = lineMessage
        }
    };
}

var pushMessageRequest = new
{
    to = recipientId,
    messages = messages
};
```

## Error Handling

### HTTP Status Codes

**Permanent Errors (No Retry):**
- **400 Bad Request**: Invalid user ID or message format
  - Check recipientId format (must be `U` + 32 hex chars)
  - Check message JSON structure
- **401 Unauthorized**: Invalid Channel Access Token
  - Verify token is correct
  - Check if token was reissued (old token invalidated)
- **403 Forbidden**: Account suspended or restricted
  - Check LINE Official Account status
- **404 Not Found**: User not found or hasn't added your account
  - User must add your official account as a friend

**Transient Errors (Automatic Retry):**
- **429 Too Many Requests**: Rate limit exceeded
  - Implement exponential backoff
  - Reduce message sending rate
- **500+ Server Errors**: LINE API temporary issues
  - Automatic retry recommended
  - Check [LINE Status Page](https://status.line.me/)

### Example Error Response

```json
{
  "success": false,
  "errorType": "InvalidRecipient",
  "errorMessage": "LINE API error 404: User not found",
  "isRetryable": false
}
```

### Common Error Messages

**"The user hasn't added this account as a friend"**
- User must add your official account before receiving messages
- Share your QR code or Official Account ID (@youraccountid)

**"Invalid channel access token"**
- Token is incorrect or expired
- Reissue token in LINE Developers Console

**"Invalid reply token"**
- Reply tokens are only valid for replying to user messages
- Use push messages for proactive notifications

## Rate Limits

### Free Account Limits

**Push Message Quota:**
- **500 messages/month** for free accounts
- Resets on the 1st of each month
- Quota visible in LINE Official Account Manager → **Settings** → **Messaging API**

**Rate Limits:**
- No official rate limit published
- Recommended: **100 messages/second** maximum
- Implement exponential backoff for 429 errors

### Paid Account Limits

**Premium ID Plans** (paid):
- Higher message quotas (varies by plan)
- Starting from ~$50/month for higher quotas

### Checking Your Quota

1. Go to LINE Official Account Manager
2. Select your account
3. **Settings** → **Messaging API**
4. View **Push message remaining** count

### Optimizing Message Usage

**Use Reply Messages When Possible:**
- Reply messages are **free** (don't count toward quota)
- Only work when replying to user messages within webhook events
- Use push messages only for proactive notifications

```csharp
// Reply message (free, requires reply token from webhook):
POST https://api.line.me/v2/bot/message/reply
{
  "replyToken": "received_from_webhook_event",
  "messages": [{ "type": "text", "text": "Thanks for your message!" }]
}

// Push message (counts toward quota):
POST https://api.line.me/v2/bot/message/push
{
  "to": "Udeadbeefcafe1234567890abcdef12",
  "messages": [{ "type": "text", "text": "Your order shipped!" }]
}
```

## Best Practices

### 1. Verify User Friendship

Before sending push messages, ensure users have added your account:

```csharp
// Check if user is a friend (requires additional API call)
// GET https://api.line.me/v2/bot/profile/{userId}
// If 404, user hasn't added your account
```

### 2. Use Rich Messages for Engagement

Plain text is functional, but Flex Messages drive higher engagement:

```csharp
// Good (plain text - functional)
"Your order #12345 has shipped. Track: https://track.maliev.com/12345"

// Better (Flex Message - engaging)
// Use Flex Message with order image, status badge, track button
```

### 3. Handle Webhooks for Two-Way Conversations

Implement webhook endpoint to receive user messages:

```csharp
[HttpPost("webhooks/line")]
public async Task<IActionResult> LineWebhook([FromBody] LineWebhookRequest request)
{
    foreach (var evt in request.Events)
    {
        if (evt.Type == "message" && evt.Message?.Type == "text")
        {
            var userId = evt.Source.UserId;
            var userMessage = evt.Message.Text;

            // Process user message
            await ProcessUserMessage(userId, userMessage);

            // Reply (free - doesn't count toward quota)
            await ReplyMessage(evt.ReplyToken, "Thanks for your message!");
        }
    }

    return Ok();
}
```

### 4. Keep Messages Concise and Actionable

```csharp
// Good (clear, actionable)
"🎉 Order #12345 confirmed!\n💰 Total: $150.00\n📦 Ships in 2-3 days"

// Bad (too long, unclear)
"Dear valued customer, we are pleased to inform you that your recent order..."
```

### 5. Use Alt Text for Rich Messages

All non-text messages require `altText` for push notifications:

```csharp
{
  "type": "flex",
  "altText": "Order #12345 confirmed - $150.00", // Shows in push notification
  "contents": { /* flex message JSON */ }
}
```

### 6. Monitor Quota Usage

Track your push message quota:
- Check LINE Official Account Manager regularly
- Implement quota monitoring in your application
- Alert when quota is running low
- Consider upgrading to paid plan if needed

### 7. Implement Fallback Channels

If LINE quota is exhausted, fall back to other channels:

```csharp
if (lineQuotaExhausted)
{
    // Fall back to SMS or Email
    await SendViaSms(phoneNumber, message);
}
```

## Troubleshooting

### Issue: "User hasn't added this account as a friend"

**Cause:** Trying to send messages to users who haven't added your official account

**Solution:**
1. User must add your official account in LINE app
2. Share QR code or Official Account ID
3. Consider using LINE Login to automatically add users as friends
4. Implement friend verification before sending messages

### Issue: "Invalid channel access token" (401)

**Cause:** Channel Access Token is incorrect, expired, or reissued

**Solution:**
1. Go to LINE Developers Console → **Messaging API**
2. Check **Channel access token** section
3. If needed, click **Issue** to generate new token
4. Update your configuration with new token
5. Note: Reissuing invalidates the old token

### Issue: "Exceeded push message quota"

**Cause:** Free account has sent 500+ push messages this month

**Solution:**
1. Wait for quota reset (1st of next month)
2. Use reply messages instead of push messages when possible
3. Upgrade to paid plan for higher quota
4. Implement fallback to other channels (SMS, Email)

### Issue: Messages Not Delivering

**Debugging Steps:**

1. **Check user ID format:**
   ```bash
   # Valid: U followed by 32 hex chars
   Udeadbeefcafe1234567890abcdef12

   # Invalid: Missing U, wrong length, non-hex characters
   deadbeefcafe1234567890abcdef12  # Missing U
   U123  # Too short
   ```

2. **Verify token configuration:**
   ```bash
   # Test API directly
   curl -X POST https://api.line.me/v2/bot/message/push \
     -H "Authorization: Bearer <your-token-here>" \
     -H "Content-Type: application/json" \
     -d '{
       "to": "Udeadbeefcafe1234567890abcdef12",
       "messages": [{"type": "text", "text": "Test"}]
     }'
   ```

3. **Check LINE Developers Console logs:**
   - Go to **Messaging API** → **Error logs**
   - Review recent errors and status codes

4. **Verify webhook settings (if using):**
   - Webhook URL must be HTTPS
   - Webhook must return 200 OK quickly
   - Check **Webhook statistics** in console

### Issue: "Invalid request body" (400)

**Cause:** Malformed JSON or invalid message structure

**Solution:**
1. Validate JSON structure matches LINE API spec
2. Check message type is supported (text, image, template, flex)
3. Verify required fields are present
4. Use [LINE API Reference](https://developers.line.biz/en/reference/messaging-api/) for correct format

### Issue: High Costs (for Paid Plans)

**LINE Pricing (approximate):**
- Free: 500 push messages/month
- Additional messages: Varies by plan (~$0.01-0.05 per message)

**Optimization:**
1. Use reply messages (free) instead of push when possible
2. Batch notifications within same conversation
3. Implement user preferences to reduce unnecessary messages
4. Monitor and analyze message effectiveness

## Advanced Features

### LINE Login Integration

Integrate LINE Login to automatically add users as friends:

1. Enable LINE Login in Developers Console
2. Implement OAuth flow
3. Get user's LINE user ID during login
4. Store mapping: your_user_id → line_user_id

### Rich Menus

Create persistent menu at bottom of chat:
- Configure in LINE Official Account Manager
- Tap areas link to actions (messages, URLs)
- Different menus for different user segments

### Account Link

Link your service accounts with LINE accounts:
1. Generate account link token
2. User approves linking in LINE app
3. Receive webhook event with linked account

### Broadcast Messages

Send messages to all friends (not via API):
- Use LINE Official Account Manager → **Broadcast**
- Select target audience
- Schedule or send immediately
- Free for reply messages within 24 hours

## Comparison: LINE vs WhatsApp vs SMS

| Feature | LINE | WhatsApp | SMS |
|---------|------|----------|-----|
| **Cost** | 500 free/mo, then paid | Conversation-based | Per message |
| **Market** | Asia (Japan, Taiwan, Thailand) | Global (2B users) | Universal (6B users) |
| **Rich Content** | ✅ Flex Messages, Templates | ✅ Media, buttons | ❌ Text only |
| **Character Limit** | 5000 | 4096 | 160 |
| **Setup Complexity** | Medium (official account) | High (approval) | Low (instant) |
| **Two-Way Chat** | ✅ Webhooks | ✅ Yes | ⚠️ Limited |
| **Best For** | Asian market engagement | Global rich notifications | Universal alerts |

## Additional Resources

- [LINE Messaging API Documentation](https://developers.line.biz/en/docs/messaging-api/)
- [LINE API Reference](https://developers.line.biz/en/reference/messaging-api/)
- [LINE Developers Console](https://developers.line.biz/console/)
- [LINE Official Account Manager](https://manager.line.biz/)
- [Flex Message Simulator](https://developers.line.biz/flex-simulator/)
- [LINE Status Page](https://status.line.me/)

## Support

### LINE Developer Support
- Developers Console: https://developers.line.biz/console/
- Documentation: https://developers.line.biz/en/docs/
- Community: https://www.line-community.me/

### LINE Official Account Support
- Manager: https://manager.line.biz/
- Help Center: https://help.line.me/

### Maliev Support
- GitHub Issues: [Project Repository]
- Internal Wiki: [Link to internal documentation]
