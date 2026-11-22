---
title: HotelOps trim Admin dashboard
date: 2026-11-22
summary: Three metrics and two charts with fewer data requests
---

# HotelOps trim Admin dashboard

Removed the six requested Admin dashboard cards/charts. Kept three metrics and two charts, consolidated layout, removed unused role/permission/department fetches and chart CSS. Build passed. The first broad UI run had three five-second timeouts; rerun with 30-second assertions passed all 12 scenarios without weakening assertions. Database-backed counts match, removed requests absent, and other actors remain functional. See plans/reports/test-261122-1847-dashboard-trim.md. Single agent; browser/build finished; no publishing. AgentWiki publish skipped.

> Historical work record — not durable authority. Prefer docs/specs/ADRs for current decisions.
