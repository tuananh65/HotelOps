---
title: HotelOps fixed operational levels
date: 2026-11-22
summary: "Fixed 8 operational levels, preserved references and removed Admin configuration UI"
---

# HotelOps fixed operational levels

User requested fixed priority/severity data and removal of operational configuration from Admin. Backed up and verified HotelOpsCompactDB, normalized existing demo/generated codes while preserving IDs and added five missing values for eight fixed LOW/NORMAL/HIGH/CRITICAL entries. Preserved dependent inventory/asset catalogs rather than deleting live references. Added catalog write guards and ordered workspace lookups. Admin settings now show roles only, including legacy routes. Builds passed with zero warnings; .NET 28 passed/2 fixture skips. Twelve API mutation attempts rejected, reference data unchanged, role/UI regression and desktop/mobile checks passed. Whole-DB constraint check timed out; scoped checks and FK/link validation passed. First browser attempt preceded API readiness; rerun after health passed. A test patch formatting error was repaired before tests. No source publication or schema changes. API PID 2964 on 5768; FE 5186. AgentWiki publish skipped. Report: plans/reports/test-261122-1954-fixed-operational-levels.md.

> Historical work record — not durable authority. Prefer docs/specs/ADRs for current decisions.
