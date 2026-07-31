---
title: Translation
nav_order: 11
---

# Translation

The operator interface is translated into English, Arabic, French and Spanish, and lays itself out
right to left when the language calls for it.

## What is and is not in the catalogue

**The catalogue is the operator-facing interface only** — the screens field staff work in. The text
beneficiaries actually read is somewhere else and always has been: the SMS templates are per-country
data that staff write themselves under Country Settings, so a Jordan deployment already sends Arabic
if somebody typed Arabic into the template. That is the right place for it, because the wording of a
voucher message is a programme decision, not a software one.

Server-rendered PDF reports are not translated. Their headings are in the Razor templates under
`TalonAdmin/Views/Reports`.

## How a language is chosen

1. An explicit choice in the header, remembered in local storage. This is for the handful of staff
   who administer several countries at once.
2. Otherwise the active country's `LanguageCode`, because a country is the unit a programme runs in
   and its staff read one language.
3. Otherwise English.

Choosing **Follow the country** in the header clears the override.

## Right to left

Arabic sets `dir="rtl"` on the document, which mirrors the whole interface: the sidebar moves to the
right, tables and forms flip, and the spacing follows. That works because the stylesheet uses logical
properties — `margin-inline-start` rather than `margin-left`, `text-align: start` rather than `left`
— so there is no second stylesheet to keep in step.

Three things needed saying explicitly, because they carry a direction of their own:

- The active-link marker in the sidebar is a `box-shadow`, which has no logical form.
- The input group that prefixes a phone number with its calling code has rounded corners on one end.
- **Codes, phone numbers and money keep their own direction inside a right-to-left sentence**
  (`unicode-bidi: plaintext`). Without that a voucher code renders with its digits reordered, which
  is worse than useless in a tool whose whole job is reading codes back correctly.

Adding a right-to-left language means adding its code to `RIGHT_TO_LEFT` in `src/i18n/index.ts`;
Farsi, Hebrew, Kurdish, Pashto and Urdu are already listed.

## The menu is data, not strings

The sidebar comes from the server: categories, groups and links are rows in a table, seeded in
English. Rather than migrate that data — and overwrite whatever a deployment has renamed things to —
`src/router/stateMap.ts` maps the seeded names and ui-router states onto catalogue keys. **Anything
unmapped keeps the text the server sent**, so a country that renamed a menu item keeps its own
wording.

## Adding or changing a language

Catalogues are flat JSON under `src/i18n`, one file per language, and `en.json` is the reference.

```bash
cp src/i18n/en.json src/i18n/pt.json     # then translate the values
# register it in src/i18n/index.ts: import it, add it to LOCALES and to `messages`
npm run i18n:check
```

`npm run i18n:check` reports the three things that go wrong with a catalogue over time and that none
of them break the build:

- a key used in the source that no catalogue defines;
- a key in `en.json` that nothing uses any more;
- a key in `en.json` that another language has not translated.

It also lists sentence-like text still sitting in a template, which is a prompt rather than a
verdict — deciding what is user-facing needs a person.

A key with no translation falls back to English silently rather than showing a raw key to a user.
That is deliberate, and it is why the check exists: the gap should surface in a build, not in front
of somebody trying to do their job.

## A caveat on the translations themselves

**The Arabic, French and Spanish were written by the same process that wrote the code, not by
translators or native speakers, and they have not been reviewed.** They are a working starting point
and should be treated as one.

This matters more here than in most software. "Cancel" meaning *dismiss this dialog* and "Cancel"
meaning *void this voucher* are different words in every one of these languages, and getting that
wrong in a tool that moves money to people who need it has consequences that a typo in a blog does
not. The vocabulary of the domain — voucher, redemption, reconciliation, cycle, beneficiary — also
has established translations inside humanitarian organisations that will not necessarily match what
is here.

Before a deployment in any of these languages, have a native speaker who knows the programme read
the catalogue. It is 347 short strings in one file; that is an afternoon, and it is worth it.
