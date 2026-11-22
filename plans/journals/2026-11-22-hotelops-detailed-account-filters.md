---
title: HotelOps detailed account filters
date: 2026-11-22
summary: Compact account filters with live SQL and browser verification
---

# HotelOps detailed account filters

Moved Create account next to compact collapsible filters. Replaced account status filtering with current employment branch, department and role combinations. Extended the existing USER_VIEW-protected user resource with read-only DTO arrays; retained authorization and schema.

FE and BE builds passed, BE zero warnings/errors. .NET: 26 passed, one unrelated worker fixture skipped. Dedicated live API/browser checks 8/8; existing UI regression checks 12/12. Compared 56 users, 53 employment memberships and 56 roles with independent SQL after verified backup. Initial harness failures were empty SQL JSON fields and exact label selectors; corrected the harness using verified combobox accessible names. Screenshots disable transitional animation. API restarted on 5768 PID 5304; FE 5186 retained. No business writes, Git publication or extra services. AgentWiki publish skipped.

Report: plans/reports/test-261122-1855-account-filters.md

> Historical work record — not durable authority. Prefer docs/specs/ADRs for current decisions.
