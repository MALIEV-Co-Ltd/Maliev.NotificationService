# WhatsApp Integration with Twilio

This guide explains how to integrate WhatsApp Business messaging with the Maliev Notification Service using Twilio.

## Table of Contents
1. [Overview](#overview)
2. [Prerequisites](#prerequisites)
3. [Getting Started with Twilio WhatsApp](#getting-started-with-twilio-whatsapp)
4. [Configuration](#configuration)
5. [Testing the Integration](#testing-the-integration)
6. [Features](#features)
7. [Message Templates](#message-templates)
8. [Error Handling](#error-handling)
9. [Rate Limits](#rate-limits)
10. [Best Practices](#best-practices)
11. [Troubleshooting](#troubleshooting)

## Overview

WhatsApp Business API via Twilio allows you to send transactional messages to customers on WhatsApp. It's ideal for order confirmations, shipping updates, appointment reminders, and customer support.

**Key Benefits:**
- 98% open rate (vs 20% for email)
- Rich media support (images, videos, documents)
- Two-way conversations
- Read receipts and delivery status
- Global reach (2+ billion users)

## Prerequisites

- A Twilio account (same as SMS setup)
- WhatsApp Business Profile approval
- Message templates approved by WhatsApp/Meta
- .NET 10 runtime

## Getting Started with Twilio WhatsApp

### Step 1: Set Up Twilio Account

1. If you don't have one, create a Twilio account at [https://www.twilio.com/try-twilio](https://www.twilio.com/try-twilio)
2. Get your Account SID and Auth Token (see [SMS.md](./SMS.md) for detailed instructions)

### Step 2: Enable WhatsApp Sandbox (Development/Testing)

**For Testing Only - No Approval Required:**

1. Log into Twilio Console
2. Navigate to **Messaging** → **Try it out** → **Send a WhatsApp message**
3. You'll see your WhatsApp Sandbox number (e.g., `+1 415 523 8886`)
4. To receive messages, users must:
   - Save the Twilio sandbox number to their contacts
   - Send a join code (e.g., "join <your-code>") to that number
5. **Sandbox Number Format**: `whatsapp:+14155238886`

**Sandbox Limitations:**
- Only works with users who've joined via join code
- Twilio branding on messages
- Not suitable for production
- 24-hour session window

### Step 3: Request WhatsApp Business API Access (Production)

**For Production - Requires Approval:**

1. In Twilio Console, go to **Messaging** → **WhatsApp** → **Senders**
2. Click **"Request Access"** or **"Get Started"**
3. Complete WhatsApp Business Profile:
   - Business name
   - Business description
   - Business website
   - Business category
   - Profile picture
4. Submit for Meta/WhatsApp review (usually 1-3 days)
5. Once approved, you'll receive a dedicated WhatsApp number

### Step 4: Get Your WhatsApp-Enabled Number

**Development (Sandbox):**
```
WhatsApp Number: +14155238886 (or your sandbox number)
Format: whatsapp:+14155238886
```

**Production:**
```
1. Go to Messaging → WhatsApp → Senders
2. Your approved WhatsApp number will be listed
3. Format: whatsapp:+15551234567 (with whatsapp: prefix)
```

## Configuration

### Option 1: Using appsettings.json (Development)

⚠️ **Never commit credentials to source control!**

```json
{
  "Twilio": {
    "AccountSid": "<your-token-here>",
    "AuthToken": "<your-token-here>",
    "WhatsAppNumber": "+14155238886"  // Sandbox or your approved number
  }
}
```

### Option 2: Using Environment Variables (Recommended)

```bash
# Linux/Mac
export Twilio__AccountSid="<your-token-here>"
export Twilio__AuthToken="<your-token-here>"
export Twilio__WhatsAppNumber="+14155238886"

# Windows (PowerShell)
$env:Twilio__AccountSid="<your-token-here>"
$env:Twilio__AuthToken="<your-token-here>"
$env:Twilio__WhatsAppNumber="+14155238886"
```

### Option 3: Using Azure Key Vault

```csharp
// Store in Key Vault:
// - Twilio--AccountSid
// - Twilio--AuthToken
// - Twilio--WhatsAppNumber
```

## Testing the Integration

### 1. Join the Sandbox (Development Only)

1. Save Twilio's sandbox number to your phone: `+1 415 523 8886`
2. Send a WhatsApp message: `join <your-join-code>` (shown in Twilio console)
3. You'll receive a confirmation message

### 2. Send a Test Message

```bash
curl -X POST https://your-api-url/notifications/v1/send \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <your-token-here>" \
  -d '{
    "userId": "test-user",
    "channelType": "whatsapp",
    "recipientId": "+66812345678",
    "message": "Hello! This is a test WhatsApp message from Maliev."
  }'
```

**Important:**
- RecipientId must be in E.164 format: `+66812345678`
- Recipient must have joined sandbox (dev) or opted in (prod)
- Provider auto-adds `whatsapp:` prefix

### 3. Check Delivery Status

1. Check your WhatsApp - you should receive the message
2. In Twilio Console: **Monitor** → **Logs** → **Messaging**
3. View delivery status, timestamps, and any errors

## Features

### Rich Media Support

Send images, videos, PDFs:

```csharp
var metadata = new Dictionary<string, string>
{
    { "mediaUrl", "https://example.com/order-receipt.pdf" }
};

await SendAsync("+66812345678", "Your receipt is attached", metadata, ct);
```

**Supported Media Types:**
- Images: JPG, PNG, GIF (max 5MB)
- Videos: MP4, 3GPP (max 16MB)
- Audio: MP3, OGG (max 16MB)
- Documents: PDF, DOC, DOCX, XLS, XLSX (max 100MB)

### Message Length

- Plain text: Up to 4096 characters
- With media: Up to 1024 characters
- Auto-truncated by provider if exceeded

### Formatting

WhatsApp supports basic formatting:

```csharp
var message = @"
*Bold text*
_Italic text_
~Strikethrough~
```Monospace```

• Bullet points
1. Numbered lists
";
```

### Link Preview

URLs automatically generate previews:

```csharp
"Track your order: https://maliev.com/orders/12345"
// WhatsApp will show a preview card
```

## Message Templates

For production WhatsApp, you **must use approved message templates** to initiate conversations.

### Creating a Template

1. In Twilio Console: **Messaging** → **WhatsApp** → **Message Templates**
2. Click **"Create New Template"**
3. Fill in:
   - **Template Name**: `order_confirmation` (lowercase, underscores only)
   - **Category**: Transactional
   - **Language**: en (English)
   - **Content**:
     ```
     Hello {{1}},

     Your order #{{2}} has been confirmed for {{3}}.

     Thank you for your business!
     ```
4. Submit for approval (usually 24-48 hours)

### Using Templates

```csharp
// When template is approved, use it like this:
var message = "Hello John, Your order #12345 has been confirmed for $150.00. Thank you!";

// Twilio will match this to your approved template
await SendAsync("+66812345678", message, null, ct);
```

**Template Rules:**
- Must be pre-approved by WhatsApp/Meta
- Can only be changed by creating new version
- Variables marked with `{{1}}`, `{{2}}`, etc.
- No promotional content without opt-in

## Error Handling

### Permanent Errors (No Retry)
- **63007**: Recipient not on WhatsApp
- **63015**: Recipient opted out / blocked
- **21211**: Invalid phone number
- **63017**: Template not approved

### Transient Errors (Automatic Retry)
- **20429**: Rate limit exceeded
- **50000+**: Server errors
- **63016**: WhatsApp number not enabled (temporary)
- **63033**: Template message failed (retry may work)

### Example Error Response

```json
{
  "success": false,
  "errorType": "InvalidRecipient",
  "errorMessage": "Twilio WhatsApp error 63007: Recipient is not on WhatsApp",
  "isRetryable": false
}
```

## Rate Limits

### Sandbox
- **1 message/second** per number
- Fair use policy applies
- Suitable for testing only

### Production - Tier-Based

Tiers are determined by message quality and volume:

**Tier 1 (Initial):**
- 1,000 unique recipients per 24 hours
- Quality rating: High

**Tier 2:**
- 10,000 unique recipients per 24 hours
- Achieved after good quality history

**Tier 3:**
- 100,000 unique recipients per 24 hours
- Requires sustained high quality

**Tier 4 (Unlimited):**
- Contact WhatsApp for approval
- Enterprise-level volume

### Message Quality Score

WhatsApp tracks:
- Block rate (users blocking your number)
- Report rate (users reporting spam)
- Response rate to customer messages

**Maintain high quality:**
- ✅ Send only relevant messages
- ✅ Use approved templates
- ✅ Respect opt-outs immediately
- ❌ Don't spam users
- ❌ Don't use unapproved templates

## Best Practices

### 1. Respect the 24-Hour Window

WhatsApp allows free-form messages within 24 hours of:
- User's last message to you
- User's opt-in

Outside this window, you **must use approved templates**.

### 2. Always Get Opt-In

```csharp
// At registration or checkout
"Get order updates via WhatsApp? Reply YES to opt-in."

// Store opt-in status
await StoreWhatsAppOptIn(userId, phoneNumber);
```

### 3. Handle Opt-Outs Immediately

```csharp
// Set up webhook for incoming messages
// POST /webhooks/twilio/whatsapp
if (incomingMessage.Contains("STOP", StringComparison.OrdinalIgnoreCase))
{
    await OptOutUserAsync(phoneNumber);
    // Do not send any more messages
}
```

### 4. Keep Messages Concise

```csharp
// Good (clear, actionable)
"Your order #12345 shipped! Track: https://track.maliev.com/12345"

// Bad (too long, unclear)
"Dear valued customer, we are writing to inform you that your recent order..."
```

### 5. Use Rich Media Strategically

```csharp
// Enhance important messages
var metadata = new Dictionary<string, string>
{
    { "mediaUrl", "https://cdn.maliev.com/qr-codes/ticket-12345.png" }
};

await SendAsync(recipient, "Your event ticket (show QR code at entrance)", metadata, ct);
```

### 6. Monitor Quality Metrics

- Track block rate < 0.1%
- Track report rate < 0.1%
- Respond to customer queries quickly
- Use high-quality phone numbers

## Troubleshooting

### Issue: "Recipient not on WhatsApp" (63007)

**Cause:** Phone number is not registered with WhatsApp

**Solution:**
1. Verify number is correct
2. Ask user to register on WhatsApp
3. Fall back to SMS if available

### Issue: "Sandbox user needs to join" (Development)

**Cause:** Testing without joining sandbox

**Solution:**
1. Save Twilio sandbox number: `+1 415 523 8886`
2. Send: `join <your-code>` to that number
3. Wait for confirmation
4. Try sending again

### Issue: "Template not approved" (63017)

**Cause:** Using unapproved template or free-form message outside 24-hour window

**Solution:**
1. Check template status in Twilio Console
2. Wait for approval (24-48 hours)
3. Use only approved templates for session messages

### Issue: Messages Not Delivering in Production

**Causes:**
1. **Quality score too low**
   - Check WhatsApp Business Manager for quality rating
   - Reduce block/report rates
   - Send only to opted-in users

2. **Tier limit reached**
   - Check current tier in WhatsApp Manager
   - Wait for 24-hour reset
   - Request tier upgrade if needed

3. **Using wrong number format**
   - Provider adds `whatsapp:` prefix automatically
   - Use E.164 format: `+66812345678`
   - Don't manually add `whatsapp:` prefix

### Issue: High Costs

**WhatsApp Pricing (approximate):**
- Conversation-based, not per-message
- Business-initiated: $0.005 - $0.09 per conversation (varies by country)
- User-initiated: Free

**Optimization:**
1. Group related messages within 24-hour window
2. Encourage user-initiated conversations (free)
3. Use templates efficiently
4. Monitor conversation counts

## Comparison: WhatsApp vs SMS

| Feature | WhatsApp | SMS |
|---------|----------|-----|
| **Cost** | Conversation-based ($0.005-$0.09) | Per-message ($0.0075-$0.04) |
| **Open Rate** | 98% | 95% |
| **Rich Media** | ✅ Yes | ❌ No |
| **Read Receipts** | ✅ Yes | ❌ No |
| **Character Limit** | 4096 | 160 (per segment) |
| **Setup Complexity** | High (approval required) | Low (instant) |
| **Global Coverage** | Good (2B users) | Excellent (6B users) |
| **Best For** | Rich notifications, customer service | Simple alerts, OTPs |

## Additional Resources

- [Twilio WhatsApp Documentation](https://www.twilio.com/docs/whatsapp)
- [WhatsApp Business Policy](https://www.whatsapp.com/legal/business-policy)
- [WhatsApp Template Guidelines](https://developers.facebook.com/docs/whatsapp/message-templates/guidelines)
- [Twilio WhatsApp Pricing](https://www.twilio.com/whatsapp/pricing)
- [Twilio Status Page](https://status.twilio.com/)

## Support

### Twilio Support
- Console: https://console.twilio.com/
- Support: https://support.twilio.com/
- WhatsApp Docs: https://www.twilio.com/docs/whatsapp

### WhatsApp Business Support
- Business Manager: https://business.facebook.com/
- Policy Updates: https://developers.facebook.com/docs/whatsapp

### Maliev Support
- GitHub Issues: [Project Repository]
- Internal Wiki: [Link to internal documentation]
