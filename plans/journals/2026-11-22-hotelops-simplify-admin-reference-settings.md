---
title: HotelOps simplify Admin reference settings
date: 2026-11-22
summary: Hide inventory settings and manual codes while preserving database references
---

# HotelOps simplify Admin reference settings

Kept internal reference codes after inspecting required EF fields and unique kind/code index; role/permission codes also support authorization. Hid codes in Admin reference table/forms and generate UUID-based codes for new reference records; existing updates omit code and kind. Removed UOM and ITEM_CATEGORY from Admin UI rows and creation choices, retaining asset types as a separate category. No backend authorization or schema changes. Build and live browser verification passed. POST/PUT test requests intercepted; before/after real reference data identical. See plans/reports/test-261122-1834-admin-reference-settings.md. AgentWiki publish skipped.

> Historical work record — not durable authority. Prefer docs/specs/ADRs for current decisions.
