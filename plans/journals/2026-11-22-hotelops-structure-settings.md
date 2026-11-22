---
title: HotelOps structure settings
date: 2026-11-22
summary: Move room types and departments into system settings with branch scope preserved
---

# HotelOps structure settings

Moved department and room-type catalog editing into system settings, retaining branch-specific API scope and existing permissions. Removed hotel-detail menu entries and redirected legacy links while preserving the chosen hotel. Guarded scoped catalogs against missing branch context. Frontend build passed without warnings. Mock browser tests passed for branch switching, create/edit request scope, legacy redirects and mobile layout; 48 navbar regression cases also passed. No real API writes or database changes. Updated two real-backend regression scripts for the restored settings dropdown but did not execute them. Report: plans/reports/test-261122-2031-structure-settings.md. AgentWiki publish skipped.

> Historical work record — not durable authority. Prefer docs/specs/ADRs for current decisions.
