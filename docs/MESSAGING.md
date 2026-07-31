---
title: Messaging
nav_order: 10
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

There was also a latent bug: the `SmsBackendType` enum and the dispatch that read it numbered the
gateways differently, and the two never agreed — so a country configured through the UI was routed
to a gateway other than the one it named. The picker also offered a gateway that existed nowhere in
the code.

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
reference at send time — check the settings your channel actually needs. It is asked about a
`ChannelSettings`, which is your channel's own slice of the country's configuration, not the whole
country.

Return `DeliveryResult.Unroutable` rather than `Rejected` when the failure is about *this recipient*
rather than this message — the dispatcher then hands straight over to the next channel in the chain
instead of giving up.

## Channels

A country configures an **ordered chain** under Organization Administration → Country Settings, not
a single channel. Which channels are right depends less on the technology than on what the caseload
actually carries: a feature phone with no data rules out everything but SMS.

| | Reaches | Cost | Needs |
| --- | --- | --- | --- |
| Clickatell | Any phone | Per message, international A2P rates | A Clickatell account |
| Android gateway | Any phone | Domestic SMS rates on a local SIM | A handset, a SIM, and power |
| Telegram | Smartphones with data | Free | The recipient to enrol first |

A common shape is `telegram, sms.android`: free for everyone who has enrolled, domestic-rate SMS for
everyone who has not.

### The chain

Each message walks the chain in order until one channel takes it:

```
Telegram ──enrolled?──▶ sent, logged as `telegram`
    │
    └─ not enrolled ──▶ Android gateway ──▶ sent, logged as `sms.android`
                             │
                             └─ refused ──▶ Failed, with every reason it was turned away
```

This is what makes Telegram usable for a real caseload. On its own it reaches only the people who
have enrolled and silently fails for everyone else; behind it, an SMS channel catches the rest. A
distribution to twelve households where two have enrolled sends two Telegram messages and ten texts,
and the delivery log says which is which.

The distinction that drives it is the channel's own answer:

| A channel reports | The dispatcher |
| --- | --- |
| **Sent** | records which channel carried it |
| **Unroutable** — "I cannot reach *this person*" | tries the next channel immediately |
| **Transient** — a timeout, a 5xx | keeps the same channel and backs off |
| **Rejected** — bad credentials, barred number | fails the message; no other channel will do better |

A whole pass down the chain counts as one attempt. The chain is re-read on every attempt rather than
pinned when the message was queued, so a recipient who enrols after their first message failed is
reachable on the retry.

**Each channel carries its own credentials.** A bot token and a gateway URL cannot share one field,
so channels after the first store theirs as country properties — `Channel <key> Url`, `… User`,
`… Password`. A channel with none of its own falls back to the country's original three fields,
which is what keeps every single-channel country working untouched. The chain itself is the
`Message Channels` property; without it, the country is on whatever single backend it always was.

### Clickatell (`sms.clickatell`)

Driven by a URL template in `CountrySettings.ServiceUrl`, where `{0}` is the recipient and
`{1}` the message:

```
https://platform.clickatell.com/messages/http/send?apiKey=…&to={0}&content={1}
```

The template shape is what deployed countries already have, so it is kept as is. Non-GSM-7 text is
sent as UCS-2 hex with `unicode=1` appended, without which Arabic voucher messages arrive as
mojibake.

A country pointed at a backend with no channel behind it resolves to nothing, and every message
fails with a message naming the problem — deliberately visible rather than silently rerouted.

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

A message to someone who has not enrolled is reported **Unroutable**, so a country with an SMS
channel behind Telegram reaches them there instead. With nothing behind it, the message fails and
the delivery log names the number.

Register the webhook once per country:

```bash
curl "https://api.telegram.org/bot<TOKEN>/setWebhook" \
  -d "url=https://your-talon-host/api/Telegram/JO?secret=<SystemSecret>"
```

Telegram only calls HTTPS URLs, and it retries an update until it gets a 200, which is why the
endpoint answers 200 even for updates it ignores.

## Message templates

Talon ships **no default wording**. Every message it sends is a template you write, stored per
country under **Organization Administration → Country Settings**. Until you write them, a
distribution enqueues one message per household and fails every one with *"The message body is
empty"* — the codes are issued and valid, but nobody is told what they are.

Write the templates before the first distribution, not after.

### The substitution syntax

A template is plain text with `@Model.Something.Field` placeholders, which are replaced with values
from the record the message is about. `@(Model.Something.Field)` works too, and is the form to use
when the placeholder is followed by a letter or a dot that would otherwise be read as part of the
name.

```
Your voucher code is @Model.Voucher.VoucherCode for @Model.Voucher.Value JOD.
```

This is a deliberately small substitution, not Razor. There are no conditionals, no loops, no
formatting specifiers and no method calls — only field lookups. Anything else is left in the message
exactly as you typed it.

{: .caution }
> **A placeholder that names a field which does not exist renders as nothing, and the message still
> sends.** `@Model.Voucher.Code` is not a field — the field is `VoucherCode` — so that template
> delivers "Your voucher code is ." to every household, and the delivery log records it as sent.
> Nothing warns you. Send yourself a test message and read it before you run a distribution.

### What you can put in each message

| Template | Sent to | When |
| --- | --- | --- |
| `Beneficiary Voucher Message` | Beneficiary | A voucher is issued. **This is the one that carries the code** — if you write only one, write this. |
| `Beneficiary Confirmed Message` | Beneficiary | Their voucher was redeemed |
| `Vendor Confirmed Message` | Vendor | They redeemed a voucher, and on a repeat of the same code inside the grace window |
| `Beneficiary Cancelled Voucher Message` | Beneficiary | Their voucher was cancelled |
| `Vendor Cancelled Message` | Vendor | A voucher they held was cancelled |
| `Beneficiary Already Used Message` | Beneficiary | A code already redeemed was presented again |
| `Vendor Already Used Message` | Vendor | As above |
| `Beneficiary Cannot Accept Message` | Beneficiary | The vendor is not of a type this voucher category allows |
| `Vendor Cannot Accept Message` | Vendor | As above |
| `Wrong National Id Message` | Vendor | The identifier given with the code did not match. The interface labels this one **Wrong Personal Identifier Message** — the stored name is older than the rename, and countries have already filled it in under the old one |
| `Invalid Voucher Message` | Vendor | The code is not a voucher at all |

The fields available are the ones on the records themselves. The useful ones:

| Placeholder | Value |
| --- | --- |
| `@Model.Voucher.VoucherCode` | The redemption code |
| `@Model.Voucher.Value` | The voucher's value, unformatted |
| `@Model.Beneficiary.Name` | First and last name together |
| `@Model.Beneficiary.FirstName` | Given name alone — usually the better choice in a text |
| `@Model.Beneficiary.MobileNumber` | Their number |
| `@Model.Vendor.Name` | The vendor's trading name |
| `@Model.Vendor.MobileNumber` | The vendor's number |

Stick to fields on the three records themselves. Following a relationship out of them — the
distribution a voucher belongs to, the location a beneficiary lives in — reaches for something the
workflow did not load, and an unloaded relationship renders as nothing in exactly the same silent
way a misspelling does.

Not every message carries every record, either. `Invalid Voucher Message` and
`Wrong National Id Message` fire before a beneficiary is known, so `@Model.Beneficiary.…` is empty
in both, and a template that opens by greeting them produces a message that starts with a blank.
`Invalid Voucher Message` has no voucher either — by definition, the code matched nothing — so the
code the vendor actually typed is at `@Model.VoucherCode`, not `@Model.Voucher.VoucherCode`.

### A worked set

Short, unambiguous, and useful to someone holding an old handset:

```
Beneficiary Voucher Message
  @Model.Beneficiary.FirstName: your voucher code is @Model.Voucher.VoucherCode,
  worth @Model.Voucher.Value. Show this code at a participating shop.

Beneficiary Confirmed Message
  Your voucher @Model.Voucher.VoucherCode was used at @Model.Vendor.Name. Thank you.

Vendor Confirmed Message
  Accepted: @Model.Voucher.VoucherCode for @Model.Voucher.Value.

Beneficiary Already Used Message
  Voucher @Model.Voucher.VoucherCode has already been used. Contact the programme office.

Invalid Voucher Message
  That code was not recognised. Please check it and try again.
```

### Things worth getting right

**Keep it inside one SMS.** A GSM-7 message is 160 characters; a message containing any Arabic
character is UCS-2 and drops to **70**. Longer messages are split, and every gateway charges per
part. Since the placeholder expands at send time, a template that looks short can cross the boundary
once a long name is substituted — check with your longest name, not your shortest.

**Say who is writing.** A code arriving from an unknown number is indistinguishable from a scam.
Name the programme or organisation in the message that carries the code.

**Put in only what the message needs.** These arrive on shared and borrowed handsets, and a text
message sits in the inbox afterwards. The code and the amount are needed; a personal identifier, a
full name, a location or a household size generally is not. Never place `@Model.Beneficiary.PIN` or
`@Model.Vendor.Password` in a template — they are on the record, so they will render.

**Write them in the language people read.** Templates are per country and are stored as you type
them, so they can be written in any script — this is separate from the interface language, which
only affects staff screens. Right-to-left text is stored and delivered unchanged.

**An organisation can override a country's wording.** If the same country is run by more than one
organisation, a template set on the organisation's own country record wins over the country-wide
one; blank there falls back. That is the mechanism to use when two programmes in one country need to
identify themselves differently.

## Inbound

Redeeming a voucher is a message going the other way, and it works from any channel.

The rules — is this a registered vendor, is the code real, is it the right vendor type, has it been
used, does the identifier match — used to live inside
`VoucherWorkflowController.ValidateTransactionSMS`. That meant an SMS gateway posting to that one
action was the only way to redeem anything; a vendor replying to the bot had nowhere to go. They now
live in `InboundMessageProcessor`, and the transport is an argument:

```
SMS gateway ──▶ POST /api/VoucherWorkflow/ValidateTransactionSMS/{iso}?secret=…
                     { "to": …, "from": …, "message": "482913 9876543210" }
                                     │
Telegram    ──▶ POST /api/Telegram/{iso}?secret=…                              │
                     chat id ──▶ TelegramContact ──▶ the vendor's number ──────┤
                                                                               ▼
                                                        InboundMessageProcessor
                                                                               │
                                                     ledger entry + confirmations
```

A Telegram redemption resolves the chat back to a phone number first, because the processor
identifies the vendor by number; only an enrolled chat can redeem. Confirmations go back out through
the queue and the country's channel chain, which for an enrolled vendor means the same chat.

Every outcome is an HTTP 200. An SMS gateway treats a non-200 as a delivery failure and redelivers,
which would replay the redemption.

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
