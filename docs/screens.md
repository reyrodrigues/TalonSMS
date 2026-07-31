---
title: Every screen
nav_order: 6
---

# Every screen
{: .no_toc }

What each part of the interface is for, in the order the menu presents it.
{: .fs-6 .fw-300 }

1. TOC
{:toc}

---

[Operating a programme](operate.html) is the narrative — the order you do things in to get money to
people. This is the inventory: every screen, including the ones a normal week never touches.

The sidebar is data, not code. Categories, groups and links live in the database and are seeded on
first boot, so a deployment can rename or reorder them. Everything below describes the seeded
arrangement.

**Two things hide screens from you.** A menu category can be restricted to roles — Country
Administration and above are — and every category except the first two is hidden until a country is
selected. Within a screen, the New, Edit and Delete buttons are gated separately by the permission
list; a user without *Create Beneficiaries* sees the list without the New button rather than a
button that fails.

---

## Everywhere

**The header** carries three menus. The country picker appears only when your account has more than
one country, and switching returns you to the dashboard because everything on screen is scoped to a
country. The language picker overrides the country's own language for you alone and remembers it.
The account menu signs out.

**The dashboard** is the landing screen: issued against redeemed value for every programme with a
distribution, broken out by location and vendor, with a chart of vouchers distributed against
vouchers used over time. It updates live over SignalR when a redemption lands. A programme appears
here only if **Show in Dashboard** is set on it and at least one of its vouchers has been used.

---

## Programme

### Beneficiaries

The household register. One row per household, not per person: a beneficiary record is who receives
and who redeems.

- **Search** covers names, mobile number and personal identifier.
- **Export** and **Import** move the whole list through Excel, which is how bulk changes are
  normally made. Import matches on the **`Id` column**: a row that carries one updates that
  household, a row with the cell blank creates a new one. Export first and edit what comes back
  rather than building a sheet from scratch, or every row will be treated as new.
- **Cycle** and **Location** import by name, and a name that does not exist yet is created. A typo
  therefore makes a new cycle rather than an error, so check the cycle list after a large import.
- Editing a household exposes its **personal identifier** — the number a vendor asks for at
  redemption — with a **Generate** button that issues a fresh 8–10 digit random one. Prefer that to
  recording a national ID document; see [Security](security.html).
- **Disabled** stops a household receiving vouchers in future distributions without deleting the
  record or affecting vouchers already issued.
- A household belongs to a **cycle**, and a distribution goes to a cycle.

### Cycles

Named groups of households — "Winterization 2026 — Cohort A". A distribution targets a cycle, so
this is how you decide who is in a given round. A household is in one cycle at a time.

### Programs

A funded programme: its name, fund codes, the voucher code length, whether it shows on the
dashboard, and its **voucher categories**.

Categories are the part that matters. Each one is a voucher type, a value and a vendor-type
restriction — "JOD 25, food, redeemable at grocers". Every household in a distribution receives one
voucher per category, so a programme with two categories issues two vouchers per household. A
programme cannot be distributed until it has at least one.

{: .note }
> A category also carries a **validity offset** — how long after issue the voucher becomes
> redeemable — which the distribution honours but this screen does not expose. Setting it means
> writing to `ProgramVoucherCategory.ValidAfterOffset` directly. Left alone, vouchers are redeemable
> immediately.

**Distribute Vouchers** is on this screen, not on Distributions. It asks for the cycle and the
location, then issues one voucher per category to every active household in that cycle and queues
their messages. The Distributions screens are where you watch the result.

### Distributions

A distribution is one round of one programme to one cycle. The list shows what has been sent; the
detail screen shows the outcome:

- a summary of households reached, vouchers issued and value
- **Unused Vouchers** — issued and not yet redeemed, with **Resend** for a household that says the
  message never arrived, and **Cancel** for a voucher that should not be redeemable
- **Used Vouchers** — redeemed, with the claim code and the vendor
- **Distribution Report** — the PDF accounting for the round

Distributing is a single irreversible action: it issues codes and queues the messages. There is no
undo, only cancellation of individual vouchers.

### Vendors

The shops and traders who accept vouchers. A vendor has a type, a location, a registered mobile
number — the number redemptions must arrive from — and optionally sales people and devices.

Vendors can be nested: a chain has a parent record and branches under it. Reconciliation rolls
branches up to the parent, so the parent is who gets paid.

### Vendor Types

The categories vendors belong to, which is what voucher categories restrict against. A food voucher
redeemable only at "Grocery" needs a "Grocery" type here first.

---

## Reporting

### Distribution Report

Every voucher in one distribution with its status, the claim code where redeemed, and totals in the
country's currency. This is the document that accounts for a round.

### Program Closure Report

The whole financial position of a programme, for sign-off when it ends.

### Vendor Financial Report

What one vendor is owed, and the receipt they sign. **Running it finalizes** the reconciled
transactions it covers — see the warning under Vendor Receipt below. Reachable from here and from
the reconciliation screen; it is the same report either way.

All three render a Razor template to HTML and convert it with headless Chromium, ask for a paper
size, and archive a copy under Report History.

### Report History

Every report that has been generated for a programme, with the PDF and the spreadsheet, and a slot
to **attach a signed copy** once a vendor or a finance officer has signed and returned it. That
attachment is the audit trail for a payment.

### Vendor Receipt

Vendor settlement, in the order it has to happen:

1. Pick the vendor and programme and **load** what they have redeemed and not been paid for.
2. **Hold back** anything disputed — it drops out of the total and stays outstanding for a later
   run.
3. **Reconcile All** records who agreed to pay the rest, and when.
4. **Issue Receipt** produces the PDF the vendor signs and *finalizes* those transactions.
5. **Payment Schedule** is the spreadsheet finance works from, over a date range.

{: .caution }
> Step 4 is load-bearing. The payment schedule reports finalized transactions only, so reconciling
> and going straight to the schedule produces a spreadsheet with nothing on it.

---

## Country Administration

Restricted to Country Administrator and above.

### Transaction Log

Every point-of-sale transaction uploaded by the offline vendor client — date, household, vendor,
amount credited, amount remaining, transaction code. Read-only; this is a ledger, not a form.

### Quarantine

The same view filtered to transactions the sync could not reconcile — a code that does not match, a
balance that does not agree, a duplicate. They are held here rather than being applied or silently
dropped, for a human to resolve. An empty quarantine is the normal state.

### Card Load Log

Value loaded onto beneficiary cards, for deployments using the card mechanism rather than SMS codes.
Read-only.

### Message Delivery Log

Every outbound message: when it was queued, who it was for, its state, how many attempts it took and
the last error. Searchable by recipient, name, body and error text.

This is where you look when a household says no code arrived. Before the queue existed, messages
were sent and forgotten and a failure left no trace at all.

### Locations

The country's places — camps, districts, neighbourhoods. Households and vendors are assigned to one,
and the dashboard and reports break down by it.

### Voucher Types

The kinds of voucher a programme's categories can be — food, hygiene, cash. Reference data used when
defining a programme.

### My Profile

Your own name, email and password. This is where you change the seeded administrator's password
after a first deployment.

---

## Organization Administration

Restricted to Organization Administrator and above.

### Users

Staff accounts: name, email, roles, and which countries they may see. A user with one country never
sees the country picker.

### Country Settings

Per-country configuration, and the screen a deployment spends real time in:

- **Delivery channels** — an ordered chain of messaging channels with their credentials, tried in
  order. See [Messaging](MESSAGING.html).
- **Message templates** — the text sent to households and vendors, with placeholders for the code
  and the value. **These are the strings beneficiaries actually read**, and they are data, not
  translations: write them in the language your recipients speak.
- Currency, calling code and the country's own language.

---

## System Administration

Restricted to System Administrator.

### Users

The same account management, unrestricted by organisation.

### Roles

The four seeded roles — System Administrator, Organization Administrator, Country Administrator,
Reporting — and any you add. Roles gate menu categories.

### Actions

The permission list: *Create Beneficiaries*, *Cancel Vouchers*, *Distribute Vouchers* and so on,
three per collection plus a handful of specific ones. These gate the buttons inside a screen, where
roles gate whole sections of the menu. Editing this list is rarely necessary — it is seeded to match
the collections the application has.

### Organizations

The organisations a deployment serves, each with its own countries and users. Most deployments have
one.

### Countries

Countries and their ISO codes, calling codes and currencies. Creating one writes four related
records, including your own access to it, so the country you create is one you can immediately
select.

### Menu Categories and Menu Items

The sidebar itself. Categories carry a sort order, an optional role restriction and whether they
need a country selected; items carry a title, an icon, a parent and the router state they navigate
to.

{: .note }
> A link whose state has no screen behind it is hidden rather than shown as a dead end, so adding a
> row here does not by itself create a page.

---

## Not in the menu

**The offline point-of-sale client** is a separate application for vendors with a smartphone. It
signs in against `/api/App/VendorProfile`, downloads the vouchers for a distribution, records
redemptions with no connection, and syncs when it next has one. Anything it cannot reconcile lands
in Quarantine. See [Operating a programme](operate.html#offline-redemption).

**The inbound webhooks** — one for SMS gateways, one for Telegram — carry redemption. They are
listed under [Configuration](configuration.html#endpoints-you-may-need-to-wire-up).
