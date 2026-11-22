---
title: HotelOps V10 actor flows and sticky headers
date: 2026-11-22
summary: "Approved role mapping, real V10 flows, sticky lists and verified primary preview; single agent."
---

# HotelOps V10 actor flows and sticky headers

## Outcome
Implemented sticky Compact table headers, Admin management/catalog screens and V10 workflows against the real API. The user approved the role mapping and storekeeper access to shared items. Backed up and tested on an isolated database before applying four roles/accounts to the primary database. Preserved 40 tables and 160 foreign keys. No legacy database changes, push or deployment. Single-agent throughout.

## Evidence and lessons
Passed: 15 actors/110 page visits, 127 API regressions, 26 unit tests plus one separate worker test, four frontend logic tests, ten V10 flow groups, 54 visual screens with zero errors/warnings, and 36 sticky measurements. Verified fail/rework/resolve/reinspect and HKS Start/End. Fixed notification deadlocks with authorized reads and an idempotent delivery update. Disabled catalog Save until all lookups finish loading. Corrected test harness accessToken, Workday route and login429 handling without changing the application rate limit. Moved the primary smoke test to a UTF-8 file after PowerShell stdin corrupted Vietnamese selectors; both primary accounts passed. Rewrote this journal body after detecting the same stdin encoding issue.

## Handoff
Preview5178/API5768 use the primary database: 40 tables, 12 roles, 15 users and 30 shift definitions. Report: plans/reports/test-261122-1359-v10-actor-flows.md. The preview worker remains disabled and was verified separately; no production assignments were generated. Confirmed test ports5179/5770 closed and primary ports5178/5768 listening. AgentWiki publish skipped.

> Historical work record — not durable authority. Prefer docs/specs/ADRs for current decisions.
