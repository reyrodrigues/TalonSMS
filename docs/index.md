---
title: Home
nav_order: 1
---

# Talon

Talon moves money to people when the banking system cannot.

It was built in 2015, in a conflict area where the banks had stopped functioning — no ATMs, no
branches, no working way to get value into the hands of the people a cash programme was meant to
reach. Almost everyone still had a phone, and almost every phone, however old, could send and
receive a text message. So SMS became the transport, and a one-time redemption code became the
instrument.

A code is issued against a funded programme, delivered by text, redeemed once at a registered
vendor, and checked against a ledger. No smartphone. No data connection. No bank.

No blockchain, no tokens, no wallet — just codes and a ledger.

{: .note }
> Talon ran until the programme it was built for was shut down, then sat in a repository for eleven
> years. It has since been brought up to date and released publicly, and it is free for anyone who
> wants it.

---

## Start here

| If you want to | Read |
| --- | --- |
| See it running on your own machine in one command | [Quickstart](quickstart.html) |
| Put it on the internet on a free or cheap host | [Deploy](deploy.html) |
| Know every setting, and what it does | [Configuration](configuration.html) |
| Understand how a real programme is run through it | [Operating a programme](operate.html) |
| Find out what a particular screen is for | [Every screen](screens.html) |
| Know what it does and does not protect against | [Security](security.html) |
| Understand what you take on by deploying it | [Disclaimer and responsible use](disclaimer.html) |
| See it end to end, with screenshots | [Walkthrough](WALKTHROUGH.html) |

---

## What it actually does

Talon is the system of record for a cash-and-voucher programme. Field staff register the people
receiving assistance and the vendors who will honour it, define what each household is entitled to,
issue that entitlement as codes, and reconcile what was redeemed against what was funded.

**Beneficiaries and vendors.** Households are registered against a country and a location, in bulk
from a spreadsheet or one at a time. Vendors are registered with the mobile number they will text
from — that number is what authenticates a redemption.

**Programmes and entitlements.** A programme carries the funding code and the categories of
assistance it covers: 75 of food and 25 of hygiene, say, each redeemable only at particular kinds of
vendor and only after a particular date.

**Distribution.** One action turns a programme and a cycle of households into vouchers: a unique code
per voucher, one voucher per category per household, all issued against the distribution so the
arithmetic stays closed.

**Redemption.** A vendor sends back the voucher code and the household's personal identifier — by
text, or as a reply to a Telegram bot, whichever the country is set up for. Talon matches the vendor by
mobile number, checks the identifier against the household's record, verifies the code has not already
been spent, records the transaction, and sends back a confirmation code. A voucher redeems once.

**Reconciliation.** Every redemption is a ledger entry against a vendor, a household, a category and
a distribution — which is what makes the reports at the end of a programme add up.

Vendors with smartphones can use a point-of-sale client that works offline,
syncing when it next has a connection. That path is optional; SMS is the one that works everywhere.

---

## Why SMS, still

In much of the world mobile money has made this unnecessary. The situations where cash programming is
hardest are the ones where that stack is missing:

- Data networks fail before voice and SMS do.
- Mobile money agents need liquidity, and liquidity is the first thing to dry up in a crisis.
- Smartphone ownership is lowest among exactly the people a programme most needs to reach — the
  displaced, the elderly, the poorest households.
- Banking access can be switched off deliberately.

What survives is a basic handset and a tower. That was the design constraint, and it is why the design
has aged better than the code did.

---

## Before you run this for real

{: .caution }
> Talon is provided as is, with no warranty and **no claim that it is secure.** It has not had an
> independent security review. Defects have been found and fixed by reading the code, but that is not
> the same as being audited — assume weaknesses remain that nobody has looked for yet.
>
> Read [Security](security.html) for what is protected and what is not, and
> [Disclaimer and responsible use](disclaimer.html) for what you take on by deploying it with real
> people's data. Both before you go live, please.

---

## Getting help

Issues and pull requests are welcome at
[github.com/reyrodrigues/TalonSMS](https://github.com/reyrodrigues/TalonSMS). Critique of the
security model and the redemption logic is especially welcome — a weakness reported is better than a
weakness discovered in the field.
