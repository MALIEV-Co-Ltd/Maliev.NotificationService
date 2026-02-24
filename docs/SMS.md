# SMS Integration with Twilio

This guide explains how to integrate Twilio's SMS service with the Maliev Notification Service.

## Table of Contents
1. [Overview](#overview)
2. [Prerequisites](#prerequisites)
3. [Getting Your Twilio Credentials](#getting-your-twilio-credentials)
4. [Configuration](#configuration)
5. [Testing the Integration](#testing-the-integration)
6. [Features](#features)
7. [Error Handling](#error-handling)
8. [Rate Limits](#rate-limits)
9. [Best Practices](#best-practices)
10. [Troubleshooting](#troubleshooting)

## Overview

Twilio is the leading cloud communications platform for SMS, Voice, and Messaging. The Maliev Notification Service uses Twilio's API to send SMS messages globally.

**Key Benefits:**
- Global SMS delivery (over 180 countries)
- High deliverability rates (99.95% uptime SLA)
- Pay-as-you-go pricing
- Delivery status tracking
- Two-way messaging support

## Prerequisites

- A Twilio account (free trial available)
- A Twilio phone number (or short code)
- .NET 10 runtime

## Getting Your Twilio Credentials

### Step 1: Create a Twilio Account

1. Go to [https://www.twilio.com/try-twilio](https://www.twilio.com/try-twilio)
2. Click **"Sign up"**
3. Fill in your details:
   - Email
   - Password
   - Phone number (for verification)
4. Verify your phone number via SMS code

### Step 2: Get Account SID and Auth Token

1. After logging in, you'll land on the **Console Dashboard**
2. Your credentials are displayed prominently:
   - **Account SID**: Starts with `AC...` (e.g., `<your-token-here>`)
   - **Auth Token**: Click **"Show"** to reveal (e.g., `<your-token-here>`)
3. **⚠️ CRITICAL:** Copy both values securely - treat Auth Token like a password!

### Step 3: Get a Twilio Phone Number

#### Free Trial (Test Mode):
1. Navigate to **Phone Numbers** → **Manage** → **Buy a number**
2. Select your country
3. Choose a number with **SMS capability**
4. Click **"Buy"** (free during trial)

#### Production:
1. Upgrade your account (add payment method)
2. Buy a phone number ($1-$15/month depending on country)
3. Or purchase a **Short Code** ($1000/month, higher throughput)

**Important Notes:**
- Trial accounts can only send to verified numbers
- Trial messages include "Sent from your Twilio trial account" prefix
- Upgrade to remove restrictions

### Step 4: Verify Recipient Numbers (Trial Only)

1. Go to **Phone Numbers** → **Verified Caller IDs**
2. Click **"Add a new number"**
3. Enter the phone number (E.164 format: `+66812345678`)
4. Verify via SMS code

## Configuration

### Option 1: Using appsettings.json (Development Only)

⚠️ **Never commit credentials to source control!**

```json
{
  "Twilio": {
    "AccountSid": "<your-token-here>",
    "AuthToken": "<your-token-here>",
    "PhoneNumber": "+15551234567"
  }
}
```

### Option 2: Using Environment Variables (Recommended)

```bash
# Linux/Mac
export Twilio__AccountSid="<your-token-here>"
export Twilio__AuthToken="<your-token-here>"
export Twilio__PhoneNumber="+15551234567"

# Windows (PowerShell)
$env:Twilio__AccountSid="<your-token-here>"
$env:Twilio__AuthToken="<your-token-here>"
$env:Twilio__PhoneNumber="+15551234567"
```

### Option 3: Using Azure Key Vault

```csharp
// In Program.cs
builder.Configuration.AddAzureKeyVault(
    new Uri($"https://{keyVaultName}.vault.azure.net/"),
    new DefaultAzureCredential());

// Store in Key Vault as:
// - Twilio--AccountSid
// - Twilio--AuthToken
// - Twilio--PhoneNumber
```

## Testing the Integration

### 1. Quick Health Check

```bash
curl -X GET https://your-api-url/notificationservice/health
```

### 2. Send a Test SMS

```bash
curl -X POST https://your-api-url/notifications/v1/send \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <your-token-here>" \
  -d '{
    "userId": "test-user",
    "channelType": "sms",
    "recipientId": "+66812345678",
    "message": "Test SMS from Maliev Notification Service"
  }'
```

**Important:** Phone numbers must be in E.164 format:
- ✅ Correct: `+66812345678`, `+15551234567`
- ❌ Wrong: `0812345678`, `555-1234`, `(555) 123-4567`

### 3. Check Twilio Console

1. Log into Twilio Console
2. Go to **Monitor** → **Logs** → **Messaging**
3. View sent messages, delivery status, and errors

## Features

### Message Length Handling

SMS has a 160-character limit per segment. The provider automatically:
- Allows up to 1600 characters (10 segments)
- Truncates longer messages with "..."
- Twilio handles multi-segment delivery automatically

```csharp
// Short message (1 segment)
"Your order #12345 is confirmed."

// Long message (3 segments, 320 chars)
"Dear customer, your order #12345 has been confirmed and will be delivered...
[continues for 320 characters]"

// Too long (truncated to 1600 chars)
var veryLongMessage = new string('A', 2000); // Truncated to 1597 + "..."
```

### Unicode and Emoji Support

Twilio supports Unicode characters:
```csharp
"สวัสดี! Your order is ready 🎉"  // Thai + Emoji
"您的订单已确认"  // Chinese characters
```

**Note:** Unicode messages have a 70-character limit per segment

### Delivery Status Callbacks

Track message delivery in real-time:

```csharp
var metadata = new Dictionary<string, string>
{
    { "statusCallback", "https://your-api.com/webhooks/twilio/status" }
};
```

Twilio will POST to your webhook with delivery updates:
- `queued` - Message queued
- `sent` - Sent to carrier
- `delivered` - Delivered to recipient
- `failed` - Delivery failed

## Error Handling

### Permanent Errors (No Retry)
- **21211**: Invalid recipient number
- **21614**: Invalid sender number
- **21608**: Unverified number (trial account)
- **30007**: Message filtered (spam)
- **30008**: Unknown error

### Transient Errors (Automatic Retry)
- **20429**: Rate limit exceeded
- **20003**: Authentication temporarily unavailable
- **30003**: Unreachable carrier
- **30005**: Unknown destination carrier
- **50000+**: Internal server errors

### Example Error Response

```json
{
  "success": false,
  "errorType": "InvalidRecipient",
  "errorMessage": "Twilio error 21211: The 'To' number is not a valid phone number",
  "isRetryable": false
}
```

## Rate Limits

### Free Trial
- **No specific rate limit**
- Subject to fair use policy
- Can only send to verified numbers

### Standard Account
- **Default**: 1 message/second per phone number
- **Can request increase** to 100+ msg/sec
- Contact Twilio support for higher limits

### Short Codes
- **1000+ messages/second**
- Dedicated for high-volume sending
- Requires separate application process

### Handling Rate Limits

The provider automatically:
1. Detects 429 rate limit errors
2. Marks as retriable
3. Uses exponential backoff (1s, 2s, 4s)

## Best Practices

### 1. Use E.164 Format

Always format phone numbers in E.164:
```csharp
// Good
"+66812345678"  // Thailand
"+15551234567"  // USA
"+441234567890" // UK

// Bad
"0812345678"
"555-1234"
```

### 2. Keep Messages Concise

```csharp
// Good (clear, actionable)
"Your order #12345 is ready for pickup at Store A. Show this SMS at checkout."

// Bad (too long, unclear)
"Dear valued customer, we are pleased to inform you that your order..."
```

### 3. Include Opt-Out Instructions

For marketing messages:
```csharp
"Special offer! 20% off today. Reply STOP to unsubscribe."
```

### 4. Monitor Delivery Rates

- Track delivery success rate
- Investigate failed messages
- Remove invalid numbers from lists

### 5. Respect Time Zones

```csharp
// Don't send SMS at 3 AM local time!
var recipientTimezone = GetUserTimezone(userId);
var localTime = DateTimeOffset.UtcNow.ToOffset(recipientTimezone);

if (localTime.Hour >= 9 && localTime.Hour <= 21) // 9 AM - 9 PM
{
    await SendSmsAsync(...);
}
```

### 6. Handle Opt-Outs

Set up Twilio webhook to handle STOP replies:
```csharp
// POST /webhooks/twilio/incoming
if (request.Body.Contains("STOP", StringComparison.OrdinalIgnoreCase))
{
    await UnsubscribeUserAsync(phoneNumber);
}
```

## Troubleshooting

### Issue: "Twilio credentials not fully configured" Warning

**Cause:** Missing Account SID, Auth Token, or Phone Number

**Solution:**
1. Verify all three config values are set:
   - `Twilio:AccountSid`
   - `Twilio:AuthToken`
   - `Twilio:PhoneNumber`
2. Check for typos in environment variable names
3. Restart application

### Issue: "The 'To' number is not a valid phone number" (21211)

**Cause:** Invalid E.164 format

**Solution:**
```csharp
// Convert to E.164 before sending
var e164Number = ConvertToE164(userPhoneNumber, userCountryCode);

private string ConvertToE164(string number, string countryCode)
{
    // Remove all non-digits
    var digits = Regex.Replace(number, @"\D", "");

    // Add country code if not present
    if (!digits.StartsWith(countryCode))
    {
        digits = countryCode + digits.TrimStart('0');
    }

    return "+" + digits;
}
```

### Issue: "To number is not a valid mobile number" (Trial)

**Cause:** Trying to send to unverified number on trial account

**Solution:**
1. Verify the recipient number in Twilio Console
2. Or upgrade account to remove restriction

### Issue: Messages Not Delivering

**Causes:**
1. **Carrier filtering** - Message flagged as spam
   - Avoid spam trigger words
   - Don't send identical messages rapidly
   - Include company name

2. **Invalid phone number** - Number doesn't exist
   - Validate before sending
   - Use Twilio Lookup API

3. **Network issues** - Carrier problems
   - Check Twilio status page
   - Retry later

### Issue: High Costs

**Solution:**
1. **Use local phone numbers** - Cheaper than international
2. **Optimize message length** - Stay under 160 chars
3. **Set spending limits** - In Twilio Console
4. **Monitor usage** - Set up budget alerts

## Cost Optimization

### Message Pricing (USD, approximate)
- **USA/Canada**: $0.0075 per message
- **UK**: $0.04 per message
- **Thailand**: $0.02 per message
- **India**: $0.01 per message

### Tips:
1. **Concat messages** - Combine multiple notifications
2. **Use templates** - Avoid long messages
3. **Remove invalid numbers** - Don't waste sends
4. **Consider WhatsApp** - Often cheaper for international

## Additional Resources

- [Twilio SMS Documentation](https://www.twilio.com/docs/sms)
- [Twilio Error Codes](https://www.twilio.com/docs/api/errors)
- [E.164 Format Guide](https://www.twilio.com/docs/glossary/what-e164)
- [Twilio Status Page](https://status.twilio.com/)
- [Best Practices for SMS](https://www.twilio.com/docs/sms/best-practices)

## Support

### Twilio Support
- Console: https://console.twilio.com/
- Support: https://support.twilio.com/
- Community: https://www.twilio.com/community

### Maliev Support
- GitHub Issues: [Project Repository]
- Internal Wiki: [Link to internal documentation]
