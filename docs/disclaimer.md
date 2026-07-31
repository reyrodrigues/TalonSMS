---
title: Disclaimer and responsible use
nav_order: 6
---

# Disclaimer and responsible use

Talon is published in the hope that it is useful to someone. It is not a product, it is not
supported, and nobody is standing behind it. If you deploy it, the consequences are yours.

Please read this page before you use it with real people's data.

---

## Provided as is

Talon is free software under the GNU Affero General Public License v3. That licence's own terms
govern, and [`LICENSE`](https://github.com/reyrodrigues/TalonSMS/blob/master/LICENSE) in the
repository is the authoritative text. Sections 15 and 16 are the parts that matter most here, and in
plain language they say:

- **There is no warranty of any kind.** Not of fitness for any purpose, not of merchantability, not
  of correctness. The software is provided as is.
- **The author is not liable** for any damages arising from its use — including lost data, lost
  money, or harm to the people whose data it holds.

Nothing on this site changes that. The documentation describes how Talon is intended to work; it is
not a promise that it does work, in your environment, for your programme.

---

## No claim that it is secure

**I make no claim that Talon is secure.**

It has never had an independent security review. The code was written in 2015 under field conditions
and modernised in 2026. That modernisation fixed real defects — the voucher code generator was
replaced because its output was measurably biased, and rate limiting was added because there was
none at all — but finding and fixing some bugs tells you only that some bugs existed. It says
nothing about what remains.

The [Security](security.html) page is an honest description of the design and the gaps I know about.
It is not an assurance, and it is certainly not a complete list. Assume there are weaknesses in this
code that neither I nor anyone else has found yet.

If you are moving meaningful value, or holding data about people who could be harmed by its
disclosure, **commission your own review.** Do not treat my having written this and my having
published a security page as a substitute for that.

---

## Using it responsibly

Talon holds names, personal identifiers, phone numbers and locations for people who are frequently
vulnerable precisely because of who they are and where they are. In a conflict or displacement
setting, a beneficiary list is a targeting list in the wrong hands. That is the real risk here, and
it is larger than the risk of someone stealing a voucher.

If you deploy this, you are taking that on. At minimum:

**Protect the data.** Serve it only over HTTPS. Restrict who can reach the admin interface — an IP
allow-list or a VPN is a reasonable precaution in a contested environment. Use a database provider
that encrypts at rest, and treat every backup as being as sensitive as the live database. Collect
only the fields you actually need, and delete data when you no longer need it. Talon has no built-in
retention policy; that obligation is yours.

**Do the setup properly.** Work through the checklist at the top of the
[Security](security.html#do-these-before-going-live) page before real data goes anywhere near it.
Several of the defaults are safe for a demo and unsafe in production, and they will not warn you
loudly enough on their own.

**Meet your own legal and regulatory obligations.** Depending on where you operate, moving value to
individuals may engage financial regulation, and holding this data engages data-protection law.
Sanctions and counter-terrorism rules may constrain who you may transfer value to. None of that is
handled for you, and none of what I have written here is legal advice. Your organisation's own
counsel and data-protection function have to be involved.

**Apply the standards your sector already has.** If you work in humanitarian response, the
obligations you already operate under — informed consent, data minimisation, do no harm, the duty of
care you owe the people on the list — apply to this system exactly as they do to any other. A tool
being free and convenient does not lower that bar.

**Pilot before you scale.** Run a small distribution, reconcile it fully, and satisfy yourself that
the numbers add up before you put a whole caseload through it.

**Have a fallback.** SMS gateways fail, hosts go down, and this software may have bugs that surface
at the worst moment. Do not build a distribution plan that has no manual path.

---

## No endorsement of your deployment

I did not build your instance, I cannot see it, and I have no way to know whether it is configured
safely. Nothing here — including any help I may give you in setting it up — constitutes an
endorsement of your deployment or a certification that it is fit for your purpose. If I answer a
question about how Talon works, that is a description of the software, not a sign-off on how you are
using it.

---

## If you run it as a service, the licence asks something of you

The Affero clause is the difference between the AGPL and the ordinary GPL, and it is easy to miss:
if you modify Talon and let people use it over a network, you have to offer those users the source
of your modified version. Running an unmodified copy for your own programme does not trigger this.
Modifying it and exposing it to users does.

This is worth raising because deploying a web application *is* the case the clause is about. If that
is a problem for your organisation, resolve it before you build on this rather than after.

---

## Reporting problems

If you find a security weakness, please report it — see
[Security](security.html#reporting-a-vulnerability). A weakness reported is much better than one
discovered in the field, and I would rather hear about it than not.

Bugs, questions and corrections to this documentation are welcome as issues at
[github.com/reyrodrigues/TalonSMS](https://github.com/reyrodrigues/TalonSMS/issues). There is no
service-level commitment attached to any of that; it is a personal project, and responses come when
they come.
