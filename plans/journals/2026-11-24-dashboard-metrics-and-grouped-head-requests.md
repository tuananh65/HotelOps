---
title: Dashboard metrics and grouped head requests
date: 2026-11-24
summary: Colored metrics before attendance; stable summary and permission-aware Head tabs verified on local Production
---

# Dashboard metrics and grouped head requests

Dashboard previously mounted attendance before all role content. Moved it after metrics, added semantic card colors and green WORKING/red ENDED badges. Department Head now has one Requests menu with permission-filtered tabs and preserved routes.

Independent review found fallback attendance remounting; split metric and detail content to preserve one stable instance. Fixed two stale test fixtures (date and array response), then four browser suites and TypeScript/Vite passed. Production5080 verified with trusted TLS, expected bundle, login and API401; API health ok. Preview5082 stopped. No backend or database changes, no commit/push.

Evidence: plans/reports/implementation-261124-0958-dashboard-requests.md. AgentWiki publish skipped; local journal only.

> Historical work record — not durable authority. Prefer docs/specs/ADRs for current decisions.
