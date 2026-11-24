# P0 continuation and confirmed decisions — 24 November 2026

This records the user's supplied continuation of sections 24–32, following the truncated source. The conversation retains the verbatim text. This acceptance index does not claim implemented or verified behavior.

## Confirmed decisions

- Keep HK workday open after scheduled end while assigned work is unfinished. Show End only when employee work is complete. Manual End atomically returns leftover supplies. Prevent new assignments and refill outside the scheduled window.
- Storekeeper processes material requests staying within the hotel. OM handles warehouse replenishment approval, not ordinary interdepartmental requests.
- Missing durable objects must already be assigned to the room.
- One active warehouse per branch.

## Concurrency acceptance (§24)

Test concurrent issues of the same item; Start Shift, Complete Cleaning, End Shift and Inspection retries; approve/reject races; configuration changes during assignment. No negative stock/staff balance, duplicate allocation/consumption/return/fulfillment or lost update.

## UX/security (§25–26)

Preserve HotelOps design system and eight-actor permission navigation. Collapsed/resettable filters; modal/drawer mutations; searchable, sortable, paginated tables with loading/empty/error/status/actions. Request timeline shows submission, handler, confirmation, transfer, fulfillment, inspection, completion, rejection and rework. Vietnamese business errors; no false success or mock data presented as real.

Backend verifies permission, branch, applicable department, assignment, ownership, handler, type, state, account, workday and concurrency. Owner read-only; Administrator no default business authority. Retire obsolete grants while retaining definitions/history used elsewhere.

## Mandatory real E2E (§27)

| Case | Acceptance |
|---|---|
| 01 | Role migration, eight logins, membership, sessions, history preservation |
| 02 | Eight fill definitions, branch independence, cross-branch denial |
| 03 | Start allocation and retry without duplicate issue |
| 04 | Shortage at start, no negative inventory or false allocation/assignment |
| 05 | Reservation, cleaning, consumption, inspection and readiness |
| 06 | Insufficient staff supplies reject assignment through UI and API |
| 07 | Multiple room reservations and concurrent assignment |
| 08 | Reassign/cancel release and reserve atomically |
| 09 | Supervisor rejects refill with reason and no stock effect |
| 10 | Refill approval, Storekeeper issue, staff balance update |
| 11 | Store shortage, denied issue and reasoned rejection |
| 12 | Competing issues against remaining stock |
| 13 | End returns leftovers exactly once |
| 14 | End with work, reservations or refill pending preserves data |
| 15 | Minimum 50: alert at 49, deduplicate, resolve at 50 |
| 16 | Replenishment approval changes no stock; actual receipt does |
| 17 | Missing durable object remediation and reinspection |
| 18 | Durable rework preserves history and does not duplicate issue |
| 19 | Durable stock shortage cannot mark room fixed/ready |
| 20 | HK damage report through external maintenance and reinspection |
| 21 | Reception report preserves occupancy |
| 22 | Maintenance rework preserves all attempts |
| 23 | False report rejected without stock/maintenance effects |
| 24 | Multiple room blockers: partial resolution cannot make Ready |
| 25 | Unified types/forms/routing/timeline/parent-child/notifications/F5 |
| 26 | OM ordinary requests read-only, replenishment approval allowed |
| 27 | Eight actors, direct API, cross-branch, revoked grants |
| 28 | Full stock/staff/room inventory reconciliation in both branches |

## Execution/documentation (§28–32)

Ten phases: audit/backup/fixture; actors; inventory foundation; housekeeping integration; unified requests; refill/replenishment; durable remediation; external maintenance; role UX; complete regression/E2E. Continue independent work when one item blocks.

Update actor responsibilities, permission matrix, use cases, activities, database/API/state/routing/traceability/test/migration documentation with version history. Mark IMPLEMENTED, VERIFIED, PARTIAL, BLOCKED or NOT_TESTED accurately.

Authorized: Project 2 frontend/backend/API/schema/test migrations, role grants/assignments/membership, workday, supplies, requests, maintenance, tests and docs. Actual data migration only after fixture migration/validation on the correct TEST target. Forbidden: Project 1 changes, database reset, historical deletion, existing password/account changes, fabricated operational stock, bypassed auth/transactions/concurrency, supplier management, external technician accounts, commit/push/merge/deploy without separate request.

Final report must cover target/backup/audit/migration/roles/grants/user counts, workday/supply lifecycle, all request/remediation/readiness workflows, routing/notifications/audit/UI, transactions/concurrency/reconciliation, builds/unit/integration/E2E, documentation/files/recovery/blockers/verdict with real evidence. Integration Owner remains responsible for the complete contract and source.
