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
workflow that sends does not change. Add the value to `SMS_BACKENDS` in `CountrySettingsView.vue`
so it can be selected.

A channel may depend on more than an `HttpClient` — `TelegramChannel` takes the voucher `Context`
to resolve a chat id — so register it with `AddHttpClient<T>()` and let DI supply the rest.

`CanServe` is the reason a half-configured country fails with a clear message instead of a null
reference at send time — check the settings your channel actually needs.

## Channels

A country selects one, under Organization Administration → Country Settings. Which one is right
depends less on the technology than on what the caseload actually carries: a feature phone with no
data rules out everything but SMS.

| | Reaches | Cost | Needs |
| --- | --- | --- | --- |
| Clickatell | Any phone | Per message, international A2P rates | A Clickatell account |
| Android gateway | Any phone | Domestic SMS rates on a local SIM | A handset, a SIM, and power |
| Telegram | Smartphones with data | Free | The recipient to enrol first |

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

### Android gateway (`sms.android`)

A phone with a local SIM, acting as the modem. This is usually the cheapest way to reach people:
messages go out at domestic rates on a SIM bought locally, which sidesteps both the cost of
international A2P routes and the countries that block them outright. No aggregator contract is
involved.

The wire format is the one [android-sms-gateway](https://sms-gate.app) exposes, because that is the
app most likely to already be installed — `POST {base}/message` with Basic auth and
`{"message": …, "phoneNumbers": [ … ]}`. Configure:

| Setting | Value |
| --- | --- |
| Service URL | The API root, e.g. `http://192.168.1.50:8080` or `https://api.sms-gate.app/3rdparty/v1`. Either the root or the full `/message` endpoint is accepted. |
| Service user name | The gateway username (defaults to `sms`) |
| Service password | The gateway password — **required**, since an unauthenticated send endpoint on a phone with a real SIM is an open relay |

The app has two deployment modes and the channel does not care which is in use; only the URL
differs. In **local** mode the phone serves its API on the office LAN and Talon posts straight to
it. In **cloud** mode the phone holds an outbound connection to a relay and Talon posts to the
relay — which is what makes this work at all when the handset sits behind NAT with no inbound
reachability, the normal case for a field office.

Two operational limits worth planning around:

- **Carrier throttling.** A consumer SIM is usually good for somewhere between 30 and 100 messages
  a day before the carrier starts dropping them. This suits a field office sending hundreds, not a
  national programme sending tens of thousands.
- **It is one device.** A flat battery, no signal or a misplaced phone stops the channel. Failures
  are retried with backoff, so a phone that comes back within the retry window recovers on its own
  — but the delivery log is the thing to watch.

### Telegram (`telegram`)

Free, instant, and it works over wifi, which matters where people have a smartphone but little
credit. It reaches only those with a smartphone, data and a Telegram account, so it is an addition
to SMS rather than a replacement for it.

| Setting | Value |
| --- | --- |
| Service URL | Leave blank for `https://api.telegram.org` |
| Service password | The bot token from BotFather |
| `Telegram Bot Username` property | The bot's `@name`, used to build enrolment links |

**A bot cannot message someone who has not written to it first.** That is a Bot API rule, not a
configuration problem, and it is the whole reason this channel needs an enrolment step that SMS
does not:

```
staff open a beneficiary ──▶ copy their enrolment link
                                  https://t.me/YourBot?start=<signed payload>
beneficiary taps it ──▶ Telegram sends "/start <payload>"
                        POST /api/Telegram/{countryIso}?secret=…
                                  │
                                  ▼
                        payload verified, chat id stored in TelegramContact
                                  │
                                  ▼
                        the channel can now reach them
```

The payload is the country and phone number plus an HMAC of both, keyed on `Jwt:SigningKey`. It has
to be unforgeable: anyone who could mint one could point another person's voucher codes at their
own Telegram account. It is also kept under 64 characters and restricted to `A-Z a-z 0-9 _ -`,
which is what Telegram's `start` parameter allows.

A message to someone who has not enrolled is **rejected, not retried** — waiting will not enrol
them — and the delivery log says exactly that, naming the number.

Register the webhook once per country:

```bash
curl "https://api.telegram.org/bot<TOKEN>/setWebhook" \
  -d "url=https://your-talon-host/api/Telegram/JO?secret=<SystemSecret>"
```

Telegram only calls HTTPS URLs, and it retries an update until it gets a 200, which is why the
endpoint answers 200 even for updates it ignores.

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

- **One channel per country.** A country on Telegram cannot fall back to SMS for the people who
  have not enrolled — their messages simply fail. A per-recipient fallback chain is the obvious
  next step, and the registry is already keyed the right way for it; what it needs is an ordered
  list per country and a record of which channel actually carried each message.
- **Telegram is outbound only.** Redeeming a voucher by replying to the bot would mean routing the
  webhook into the inbound pipeline, which currently lives inside a 166-line method on
  `VoucherWorkflowController` along with the rest of the redemption logic. Extracting it is worth
  doing on its own rather than as a side effect of adding a channel.
- **No delivery receipts.** A message marked Sent means the gateway accepted it, not that it
  arrived. Clickatell and the Android gateway can both call back with a status; nothing consumes
  that yet.
- **No per-country rate limiting.** A large distribution enqueues one message per household and the
  dispatcher sends them as fast as the channel accepts them — which is exactly how a consumer SIM
  gets throttled.
- **Phone number formats are not reconciled.** A number stored nationally (leading trunk `0`, no
  country code) will not match one stored internationally when Telegram looks up a contact. The
  same ambiguity already reaches the SMS gateways, which pass the stored number through untouched.
- `OutboundMessages` is never pruned.
