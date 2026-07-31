---
title: Operating a programme
nav_order: 4
---

# Operating a programme

This page is about running Talon rather than installing it: the order things have to be set up in,
what each concept means, and what happens during a distribution. The
[Walkthrough](WALKTHROUGH.html) is the same ground with a screenshot at every step.

---

## How the pieces fit together

```
Organisation
└── Country                        active tenant; owns reference data
    ├── Locations                  where households and vendors are
    ├── Vendor types               "grocery", "pharmacy" — gates what a voucher buys
    ├── Voucher types              units of assistance
    ├── Vendors                    who honours a voucher; identified by mobile number
    ├── Beneficiaries              households, grouped into cycles
    └── Programme                  the funded intervention
        ├── Voucher categories     entitlement per household, per type, with a valid-from date
        └── Distribution           one issuance event, to one cycle, at one location
            └── Vouchers           one per category per household, each with a unique code
                └── Transactions   the ledger: issuance, redemption, cancellation
```

Almost everything is scoped to a **country**, which is the tenant. The country switcher in the header
decides what you are looking at, and staff can be granted access to some countries and not others.

---

## Setting up, in order

The order matters — each step depends on the one before it.

### 1. Country

System Administration → Reference Data → Countries.

The ISO alpha-2 code matters operationally: it becomes part of the SMS webhook URL, so `JO` means the
gateway posts to `/api/VoucherWorkflow/ValidateTransactionSMS/JO`. The calling code and currency
symbol flow into every form and report that follows.

Then link your organisation to the country and grant your user access to it, or it will not appear in
the switcher.

### 2. Country reference data

- **Locations** — camps, districts, neighbourhoods. Households and vendors both belong to one.
- **Vendor types** — the categories you will restrict vouchers to. Worth thinking about: a voucher
  category can be locked to a vendor type, and that is the mechanism preventing a food voucher being
  spent on something else.
- **Voucher types** — the units of assistance a programme hands out.

### 3. Vendors

Register each vendor with **the mobile number they will redeem from**. That number is the vendor's
identity: whichever channel a redemption arrives on, Talon resolves it back to a number and matches
that against the vendor list, so a wrong number means every redemption from that vendor is rejected as
an unauthorised phone.

Assign each vendor a type. That is what the voucher category's restriction is checked against.

### 4. Beneficiary households

Register households against a location and group them into a **cycle** — a named cohort you will
distribute to as a unit, like "Winterization 2026 — Cohort A".

In real use these arrive through the Excel import on this screen rather than one at a time. Export
first to get the column layout, fill it in, and import.

Each household needs a **personal identifier** — the number quoted back at redemption to confirm the
right person is being served — and a mobile number if codes are to be texted to them directly.

The field was called "national ID" and was expected to hold a government document number. That suits
neither the caseload nor the risk: many of the people a cash programme reaches have no such document,
and writing a real one into a voucher database that a vendor reads out loud creates a protection
problem for no benefit. **Generate** on the beneficiary form issues a random 8–10 digit number
instead, unique within the country, which the household is told once and keeps.

Recording a real document number still works if a programme requires it — nothing validates the
format — but it should be a deliberate choice rather than the default.

### 5. Programme

A programme carries the fund code, the distribution mechanism and the **voucher code length** — six
digits by default.

{: .note }
> Code length is a real trade-off. Six digits is a million possible codes, which is short enough to be
> read out over a bad phone line and short enough that guessing is worth thinking about. Eight or ten
> digits costs your recipients nothing extra if codes are delivered by SMS rather than dictated. See
> [Security](security.html#voucher-code-entropy).

Then define its **voucher categories** — what each household gets. A category has a value, a voucher
type, optionally a vendor type it is restricted to, and a valid-from date. Two categories means each
household receives two vouchers.

---

## Distributing

"Distribute Vouchers" asks only for the cycle and the location. Behind that one action Talon:

1. Creates the distribution and copies the programme's categories onto it, so later edits to the
   programme cannot retroactively change what was issued.
2. Generates a unique code for every voucher it is about to create, checked against every code already
   issued.
3. Creates one voucher per category per household, at status **Unclaimed**, each with an issuance
   transaction tying it to its household.

Twelve households and two categories produces twenty-four vouchers. The arithmetic is closed from this
point: every voucher belongs to a distribution, and every redemption is a ledger entry against one.

Codes reach recipients by message.

How a message is delivered is a per-country setting: a Clickatell account, an Android handset with a
local SIM acting as the gateway, or a Telegram bot. Which one is right depends on what the caseload
actually carries — a feature phone with no data rules out everything but SMS — and on what messaging
costs where you are. [Messaging](MESSAGING.html) covers the three, what each needs, and where failed
messages show up.

{: .note }
> Generating codes reads every existing voucher code into memory to guarantee uniqueness. That is fine
> at the scale a country programme runs at, and it is worth knowing about before you plan a
> multi-million-voucher issuance in one database.

---

## Redemption

A vendor sends two things — the voucher code and the household's personal identifier:

```
<voucher code> <personal identifier>
```

Talon reads the **last two whitespace-separated tokens**, so a human prefix like
"TALON 482913 9876543210" is tolerated.

**It does not matter which channel it arrives on.** A text message posted by an SMS gateway and a
reply to the Telegram bot go through the same rules; a Telegram message is resolved back to the
vendor's phone number first, because that number is the vendor's identity, which also means only an
enrolled chat can redeem. Whatever the route, Talon then:

1. Resolves the country.
2. Matches the sender to a registered vendor by number.
3. Finds the voucher by code within that country.
4. Checks the vendor's type against the category's restriction.
5. Compares the identifier against the one on the household's record, ignoring any punctuation.
6. Records the redemption and sends back a six-digit confirmation code, out through the country's
   channel chain — which for an enrolled vendor means the same chat they redeemed from.

What comes back when it does not work:

| Response | Meaning |
| --- | --- |
| `Unauthorized Vendor` | The code is real but the sender's number is not a registered vendor. |
| `Invalid Voucher Code` | The sender is a vendor but no such code exists in this country. |
| `Wrong Vendor Type` | Real code, real vendor, but the category is restricted to a different vendor type. |
| `Voucher Already Used` | The voucher was redeemed more than 30 minutes ago. |
| `Confirmation code resent` | Redeemed less than 30 minutes ago — treated as the vendor not having received the confirmation, so it is sent again rather than refused. |
| `Not a talon message` | Neither the sender nor the code matched anything. |

That 30-minute window is deliberate: on a bad network the confirmation SMS is the part most likely to
go missing, and a vendor's natural response is to text again. Re-sending the code is the right answer
to a retry, and it does not redeem the voucher twice.

{: .note }
> All of these come back as HTTP 200 with the reason in the body, because an SMS gateway treats a
> non-200 as a delivery failure and redelivers it — which would replay the redemption. A 200 carrying
> `Invalid Voucher Code` is Talon working correctly.

Every inbound message is written to a message log before it is interpreted, so an unparseable message
is still visible to whoever is investigating.

### Voucher states

| Status | Meaning |
| --- | --- |
| Unclaimed | Issued, not yet redeemed |
| Partially Claimed | Some value drawn against it |
| Claimed | Fully redeemed |
| Cancelled | Voided; cannot be redeemed |

---

## Offline redemption

Vendors with a smartphone can use the point-of-sale client instead of messaging. It downloads the
vouchers for its distribution, records redemptions locally, and syncs when it next has a connection.
Transactions it cannot reconcile are quarantined for a human to resolve rather than being silently
dropped.

This path needs a smartphone and an occasional connection. Messaging is what works on any handset, so
treat the offline client as an optimisation for well-equipped vendors, not the primary mechanism.

---

## Paying vendors

Redemption records a debt; it does not settle it. Settlement runs on the Reconciliation screen, in
three steps that have to happen in this order:

1. **Reconcile** — load what a vendor has redeemed and not been paid for, hold back anything
   disputed, and record who agreed to pay the rest. Held-back transactions stay outstanding for a
   later run.
2. **Issue the receipt** — the PDF the vendor signs. Issuing it is also what *finalizes* those
   transactions, and it is archived under Report History.
3. **Payment schedule** — the spreadsheet finance works from, over a date range.

{: .caution }
> Step 2 is load-bearing. The payment schedule only reports finalized transactions, so reconciling
> and skipping straight to the schedule produces a spreadsheet with nothing on it.

## Reporting

The dashboard shows issued against redeemed value, broken out by location and vendor, and updates live
over SignalR. Building it is expensive, so each instance holds the result in memory; a redemption
drops it immediately, and it is rebuilt at least every ten minutes regardless. A change that arrives
by some other route — a distribution created through the data screens — can therefore take up to ten
minutes to show.

Reports render Razor templates to PDF through headless Chromium:

- **Distribution report** — every household with each voucher and its status, with redemption claim
  codes and totals in the country's currency. This is the document that accounts for a distribution.
- **Programme closure report** — the end-of-programme position.
- **Vendor financial report** — what one vendor is owed, and the receipt they sign. Running it
  finalizes the reconciled transactions it covers.

Beneficiary and vendor lists export to Excel, and the same format imports, which is how bulk edits are
usually made.

{: .caution }
> Reports are the memory-hungry part of Talon, because Chromium is. On a 512 MB instance a report
> request may be killed while everything else works fine. See [Sizing](deploy.html#sizing).

---

## Day-to-day administration

**Users and access.** Staff are assigned roles — System Administrator, Organization Administrator,
Country Administrator, Reporting — and granted access per country. The sidebar and the create and edit
buttons are driven by those grants, so a user with narrow access sees a correspondingly narrow
interface.

**Cancelling a voucher.** Vouchers can be cancelled rather than deleted, which keeps the ledger and the
reports consistent. A cancelled voucher cannot be redeemed.

**Audit.** Changes to households and vendors are recorded in an audit log, and every inbound SMS is
logged. When a redemption is disputed, those two logs plus the transaction records are what settles it.

---

## Before a real deployment

{: .caution }
> Work through the [Security](security.html) page. The short version: set `Jwt__SigningKey` and
> `SystemSecret`, change the seeded admin password, turn off demo data, put it behind HTTPS, and decide
> whether six-digit codes are long enough for the amount of value you are moving. Talon has not had an
> independent security review.
