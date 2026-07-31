---
title: Security
nav_order: 5
---

# Security

Talon issues bearer instruments. A voucher code is worth whatever it is worth to whoever holds it, and
the people holding them are, by definition, people with little margin for being defrauded. This page
is an honest account of what the system protects, what it does not, and what you have to do yourself.

{: .warning }
> **Talon has not had an independent security review, and I make no claim that it is secure.** The
> 2026 modernisation fixed defects found by reading the code — including a measurably biased voucher
> code generator and a total absence of rate limiting — but finding and fixing some bugs is not the
> same as being audited. Treat everything below as a description of the current design, not as an
> assurance about it. If you have the means to commission a review before running this at scale, do.
>
> See [Disclaimer and responsible use](disclaimer.html) for the terms this is published under and what
> deploying it responsibly requires of you.

---

## Do these before going live

A short list, in order of how much damage skipping them does.

1. **Set `Jwt__SigningKey`** to a long random value, unique per environment. It signs access tokens; a
   known key means anyone can mint a valid token for any user. The development default is in the
   repository, so it is public.

   ```bash
   openssl rand -base64 48
   ```

2. **Change the seeded admin password.** It defaults to `Talon!23`, which is published on this site.
   Set `Database__SeedAdminPassword` before first boot, then change it again after signing in.

3. **Set `SystemSecret`** and append it to your gateway's callback URL as `?secret=<value>`. Without
   it, the redemption endpoint accepts posts from anyone who finds the address. Talon logs a warning at
   startup when it is unset.

4. **Turn off demo data** — `Database__SeedDemoData=false` — so fictional households and vouchers do
   not end up in the same ledger as real ones.

5. **Serve it over HTTPS only.** Render and Railway terminate TLS for you. Access tokens and voucher
   codes both travel in request bodies and headers; over plain HTTP they are readable in transit.

6. **Decide whether six digits is long enough** for the value you are moving. See below.

---

## The threat model

Talon's design assumes the network is hostile and the handsets are ordinary. What it defends:

| Attack | Defence |
| --- | --- |
| Redeeming a voucher twice | Voucher status is checked and moved to Claimed on redemption; a repeat within 30 minutes re-sends the confirmation rather than redeeming again |
| A stranger redeeming a voucher | Redemption requires the sender's number to match a registered vendor |
| Spending a voucher on the wrong goods | Categories can be restricted to a vendor type, checked at redemption |
| Guessing a user's password | Rate limited to 10 attempts per 5 minutes per address |
| Reading another country's data | Every tenant-scoped query is filtered by the active country and organisation |
| A crafted JSON payload instantiating arbitrary types | The deserialiser is restricted to a type allow-list |

What it does not defend, and you should know about:

| Gap | Consequence |
| --- | --- |
| Voucher codes are short by default | A six-digit code space is a million candidates. See below |
| SMS is unencrypted and unauthenticated by design | Anyone who can read a recipient's messages, including their network operator, can read their code |
| Vendor identity is a phone number | A spoofed sender number, where an operator permits it, impersonates a vendor |
| No per-voucher redemption attempt limit | Rate limiting is per address, not per code |
| No independent audit | Unknown unknowns |

---

## Voucher code entropy

This is the part worth understanding, because it is a decision you make per programme rather than a
property of the software.

A voucher code's length is set on the programme, and defaults to **six digits — one million possible
codes**. Codes are drawn from a cryptographic random source, uniformly, and checked for uniqueness
against every code already issued.

Uniform and unpredictable is necessary but not sufficient. What matters operationally is the ratio of
issued codes to the size of the space. Issue 10,000 vouchers into a six-digit space and one in every
hundred guesses hits a real code. An attacker who can send SMS cheaply, and who is limited only by
what your gateway and the rate limit allow, does not need many guesses at those odds.

Mitigations, roughly in order of effectiveness:

- **Use longer codes.** Eight digits is a hundred times the space; ten is ten thousand times. If codes
  are delivered by SMS rather than read aloud, length costs the recipient nothing.
- **Set `SystemSecret`**, so the redemption endpoint is not reachable by strangers at all.
- **Reconcile promptly.** A guessed redemption shows up as a redemption by a real vendor against a
  household that did not receive anything, which is visible in the distribution report.
- **Keep distributions small relative to the code space**, since the ratio is what determines the odds.

{: .note }
> **What was fixed in 2026.** The original generator drew eight cryptographic bytes, rendered them as a
> decimal string, and cut a substring from a randomly chosen offset. That was biased: over 500,000
> draws of a six-digit code, codes beginning with `1` appeared 13.3% of the time against the 10% a
> uniform draw gives, while `0`, `8` and `9` were each under-represented. The offset was also chosen
> with a non-cryptographic PRNG, and its arithmetic went out of range at longer code lengths — at 18
> digits it threw on about 5% of calls. It was replaced with rejection sampling over a cryptographic
> source. Leading digits now land within 0.1 points of uniform, and the properties are pinned by
> `VoucherCodeGenerationTests`.

---

## Rate limiting

Talon has two limits, both partitioned per client address, and both taking `X-Forwarded-For` into
account so a platform's proxy does not collapse every caller into one bucket.

| Policy | Applies to | Limit |
| --- | --- | --- |
| `credentials` | `/Token`, vendor sign-in | 10 per 5 minutes |
| `sms-redemption` | The inbound SMS webhook | 120 per minute |

The credential limit is tight because no legitimate client tries many passwords quickly. ASP.NET
Identity lockout is not configured, so this limit is what stands in for it.

The redemption limit is deliberately generous, and it is important to understand why it is not the
primary control. A real distribution produces a burst of redemptions that all arrive from the
gateway's single address, so a tight limit there would reject genuine redemptions. It is a flood guard.
**`SystemSecret` is the actual gate on that endpoint** — the limit only bounds how fast someone who has
already got past it can work.

{: .note }
> Limits are in-process, so each instance counts separately. Running multiple replicas multiplies the
> effective limit; a shared store would be needed to enforce them globally.

---

## Authentication

Three schemes, chosen per request from the `Authorization` header:

- **`Bearer`** — a JWT from `/Token`, signed with `Jwt:SigningKey`, valid 14 days by default. Used by
  the admin client.
- **`Basic`** — for staff tooling.
- **`Token`** — for vendor devices, validated against the vendor's stored authorization token.

Two things worth knowing. Access tokens are valid for **14 days** and there is **no revocation list**,
so signing a user out does not invalidate their token — rotating `Jwt:SigningKey` invalidates every
token at once, which is the blunt instrument available. And password rules are relaxed (six characters,
no complexity requirement) because they were inherited; tighten them in `Program.cs` if your context
calls for it.

Optionally, sign-in can fall back to a directory service for staff accounts. It is off by default.

---

## Data protection

Talon holds names, ID numbers, phone numbers and locations for people who are often vulnerable
precisely because of where they are and who they are. That is the most sensitive thing in the database,
more so than the vouchers.

- **In transit** — whatever your host provides. Use HTTPS.
- **At rest** — whatever your database provider provides. Neon and Supabase both encrypt at rest on
  every tier. Talon does not add application-level encryption.
- **Backups** — your provider's. Both offer point-in-time restore. A backup of this database is as
  sensitive as the database.
- **Deletion** — there is no built-in retention policy or bulk erasure tool. If you have obligations
  about how long you keep beneficiary data, you will have to meet them yourself.

Think about who can reach the admin interface. A beneficiary list is valuable to people who may mean
its subjects harm, and an IP allow-list or a VPN in front of the application is a reasonable precaution
in a contested environment.

---

## Reporting a vulnerability

Open an issue at [github.com/reyrodrigues/TalonSMS/issues](https://github.com/reyrodrigues/TalonSMS/issues),
or, for anything you would rather not disclose publicly, contact the maintainer directly through
GitHub first.

Critique of the redemption logic and the code generation is actively wanted. A weakness reported here
is much better than one discovered in the field.
