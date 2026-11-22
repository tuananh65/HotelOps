---
title: HotelOps navbar page titles
date: 2026-11-22
summary: Move all authenticated page titles into the sticky navbar
---

# HotelOps navbar page titles

Navbar headings now use a shared React portal owned by each page. Updated all authenticated pages, including dynamic hotel titles; section headings and dialogs remain local. Responsive navbar wraps on mobile and empty heading toolbars take no space. Frontend build passed. Browser checks use intercepted API fixtures only. Initial harness failures were incorrect collection fixtures and a missing reload after changing the mocked profile; no application fix was needed for those failures. Final validation is recorded in plans/reports/test-261122-2014-navbar-page-titles.md. No database mutations, backend changes, deployment or Git operations. AgentWiki publish skipped.

> Historical work record — not durable authority. Prefer docs/specs/ADRs for current decisions.
