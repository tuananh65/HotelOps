---
title: HotelOps role cleanup and action buttons
date: 2026-11-22
summary: Verified inactive role deletion and clearer role management UI
---

# HotelOps role cleanup and action buttons

Removed two inactive demo roles after a verified backup and rollback rehearsal. Deleted only their 47 grants and three revoked assignments; active assignments and grants compared bidirectionally and preserved. Audit 253 contains before/after data. Standardized ten Vietnamese role descriptions, removed the UI code column and aligned colored edit/permission buttons. Build and four workflow tests passed; live API/UI and three viewport checks passed. Single agent. No source publishing. User-facing Vite 5186 and API 5768 retained; browser closed. See plans/reports/test-261122-roles-cleanup.md. AgentWiki publish skipped.

> Historical work record — not durable authority. Prefer docs/specs/ADRs for current decisions.
