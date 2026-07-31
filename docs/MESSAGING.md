---
title: Messaging
nav_order: 8
---

# Messaging

Talon sends voucher codes, redemption confirmations and cancellations to beneficiaries and vendors.
This document covers how that works now and what it replaced.

## What it replaced

Outbound messaging was a single ~100-line `SendAsyncMessage` method inside
`VoucherWorkflowController`: a hardcoded if/else over three gateways, called from ten places, with
no interface and no tests. Three things about it mattered more than its age.

- **Every failure was swallowed.** Three bare `catch { }` blocks meant a beneficiary who never
  received their voucher code left no trace anywhere.
- **It was fire-and-forget.** `Task.Factory.StartNew(...)`, never awaited, so a restart dropped
  whatever was in flight.
- **There was no outbound record at all.** `MessageLog` stores only *inbound* messages, so there
  was nothing to reconcile a distribution against.

There was also a latent bug: `SmsBackendType` declared `RescueSMS = 1, Twilio = 2, CustomRest = 3`
while the dispatch compared `0` = RescueSMS, `1` = TurboSMS, `2` = Clickatell. The two never
agreed, so a country configured through the UI was routed to a gateway other than the one it named,
and Twilio appeared in the picker without existing anywhere in the code.

## How it works now

```
workflow ──enqueue──▶ OutboundMessages table ──▶ OutboundMessageDispatcher ──▶ IMessageChannel
  (a database write;                              (BackgroundService,            (Clickatell,
   never waits on a gateway)                       retries with backoff)          …)
```

**Enqueuing is only a database write.** Nothing user-facing waits on a gateway or fails because one
is down. `OutboundMessageQueue.EnqueueAsync` resolves the country's channel at that moment, so a
country that is misconfigured produces a failed row immediately, next to the message it stopped,
rather than at some later point in a log nobody reads.

**The dispatcher drains the queue.** `OutboundMessageDispatcher` is a `BackgroundService` that
claims due messages in batches, hands each to its channel, and records the outcome. Because the
queue is a table rather than in-process state, a restart mid-send resumes.

**Retries are classified, not blind.** A channel returns `DeliveryResult.TryAgain` for a timeout or
a 5xx and `DeliveryResult.Rejected` for a bad credential or a barred number. Retrying the latter
only burns attempts and delays the failure staff need to see. Retries back off exponentially — 1,
2, 4, 8 minutes — up to `Messaging:MaxAttempts`.

**Failures are a screen.** Country Administration → Transactions → Message Delivery Log lists every
message with its status, attempt count and the verbatim error.

## Adding a channel

Implement `IMessageChannel`, register it, and add a value to `SmsBackendType`:

```csharp
public class MyChannel : IMessageChannel
{
    public string Key => "sms.mine";
    public bool CanServe(CountrySettings settings) => !string.IsNullOrWhiteSpace(settings?.ServiceUrl);
    public Task<DeliveryResult> SendAsync(OutboundMessage message, CountrySettings settings, CancellationToken ct) { … }
}
```

Then in `Program.cs`:

```csharp
builder.Services.AddHttpClient<MyChannel>();
builder.Services.AddScoped<IMessageChannel>(sp => sp.GetRequiredService<MyChannel>());
```

and map the new `SmsBackendType` value to `MyChannel.Key` in `MessageChannelRegistry.KeyFor`. The
workflow that sends does not change.

`CanServe` is the reason a half-configured country fails with a clear message instead of a null
reference at send time — check the settings your channel actually needs.

## Channels

### Clickatell (`sms.clickatell`)

The only gateway carried forward. RescueSMS and TurboSMS were removed; their clients, the SOAP
service reference and the WireMock stubs went with them.

It is driven by a URL template in `CountrySettings.ServiceUrl`, where `{0}` is the recipient and
`{1}` the message:

```
https://platform.clickatell.com/messages/http/send?apiKey=…&to={0}&content={1}
```

That shape is inherited — it is what deployed countries already have — so it was kept rather than
migrated. Non-GSM-7 text is sent as UCS-2 hex with `unicode=1` appended, without which Arabic
voucher messages arrive as mojibake.

Clickatell keeps the value `2` it had in the old dispatch, so a country already pointed at it keeps
working. A country left on a removed gateway resolves to no channel and every message fails with a
message naming the problem — deliberately visible rather than silently rerouted.

## Inbound

Inbound is already channel-agnostic and did not change:

```
POST /api/VoucherWorkflow/ValidateTransactionSMS/{countryIsoAlpha2}?secret=…
{ "to": "...", "from": "...", "message": "..." }
```

Any gateway, phone acting as a modem, or chat bot can post to it. That is the seam a new inbound
integration plugs into.

## Configuration

| Key | Meaning |
| --- | --- |
| `Messaging:PollIntervalSeconds` | How often the queue is drained (default 5) |
| `Messaging:BatchSize` | Messages dispatched per pass (default 25) |
| `Messaging:MaxAttempts` | Attempts before a message is marked failed (default 5) |

Per-country settings live under Organization Administration → Country Settings: the gateway, its
credentials, and the message templates. **Nothing is sent until a service URL is set** — a fresh
install queues messages and fails them with exactly that reason.

## Still to do

- No delivery *receipts*. A message marked Sent means the gateway accepted it, not that it arrived.
  Clickatell can call back with a status; nothing consumes that yet.
- No per-country rate limiting. A large distribution enqueues one message per household and the
  dispatcher will send them as fast as the gateway accepts them.
- `OutboundMessages` is never pruned.
