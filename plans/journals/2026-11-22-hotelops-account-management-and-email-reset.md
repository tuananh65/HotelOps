---
title: HotelOps account management and email reset
date: 2026-11-22
summary: Account management implemented and verified; real SMTP configuration pending
---

# HotelOps account management and email reset

Expanded account management with profile edits, current roles/memberships and revoke confirmations. Added USER_MANAGE-protected email password reset with admin reauthentication, rowversion, cryptographic temporary credentials, 24-hour expiry, session revocation and password-free audit. SMTP sends with STARTTLS and rolls SQL back on send failure. Fixed expired temporary password changes through an already-open restricted session.

Local SMTP configuration is absent; the UI/API fail closed. Real external sending awaits provider/sender and local credentials. Verified reset on a backed-up isolated SQL copy using a fake mail transport; primary staff passwords remain unchanged. FE/BE builds pass, .NET 29 pass/1 worker skip, management API/UI 8/8, account filters 8/8 and UI regression 12/12. Fixed test-only same-hash modal navigation and credential-safe assertions. SQL/SMTP distributed commit limitation is documented. Keep Vite 5186 and API 5768 PID 33052; no extra services or publishing. AgentWiki publish skipped.

Report: plans/reports/test-261122-1914-account-management.md

> Historical work record — not durable authority. Prefer docs/specs/ADRs for current decisions.
