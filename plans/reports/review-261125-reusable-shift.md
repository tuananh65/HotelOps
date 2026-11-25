# Independent review — reusable shift allocation

Reviewed 25 November 2026. Read-only implementation review; no database writes, server starts, or implementation edits. Scope: group-B configuration migration/service/UI, lifecycle guard, reusable allocation integration, and new housekeeping fixture.

## Findings

- **P1 — Unconditional cleanup can stop and revoke another verification run.** `scripts/verification/v2-housekeeping-runtime.mjs` calls inventory Setup, housekeeping Setup and inventory Start in sequence, but its `finally` always calls inventory Stop, housekeeping Cleanup and inventory Cleanup. Both fixtures intentionally reject an existing fixture. If Setup rejects because another run owns the fixture, this runner still invokes cleanup against that existing fixture and can stop its API, revoke its sessions, and deactivate its test branch/items. Track successful ownership of each setup and successful start; perform cleanup only for resources acquired by this invocation. Prefer passing a run identity checked by each cleanup action, including recovery after partial setup. This is a TEST isolation/concurrent-run risk, not evidence of Production mutation.
- **P2 — Corrupted Vietnamese label in group-B configuration list.** `frontend/src/compact/supply-configuration.tsx:61` renders the literal `C?p ??u ca:`. Replace it with `Cấp đầu ca:` using UTF-8. The form label itself is correct.

## Confirmed by source inspection

- The migration is restricted to the exact isolated TEST database, uses XACT_ABORT and a transaction, replaces the constraint with WITH CHECK, and preserves existing rows/revisions/balances.
- The immutable trigger retains the original EXCEPT comparison of revision data; existing revisions can only transition current → noncurrent. INSERT validation additionally requires a branch/item REUSABLE profile for a current group-B configuration with positive initial allocation. Group B requires per-guest quantity zero; group C retains initial quantity zero.
- Save requires OM plus ROOM_SUPPLY_CONFIG in the requested branch, uses the existing supply transaction lock, validates active item/base unit and the branch's unique warehouse, and creates a new revision rather than rewriting an old one.
- Lifecycle reclassification away from REUSABLE is blocked while a current group-B positive allocation remains. It uses the same lock. Existing reconciliation guard additionally rejects classification changes with stock movement or staff-balance history.
- Consumable shift balances explicitly select CONSUMABLE profiles. Reusable handover uses reusable custody positions and an immutable configuration snapshot; it does not increment consumable Used. Returning clean staff holdings preserves ownership; dirty/damaged custody is not made READY.
- Fixture SQL creates a uniquely prefixed synthetic branch and users, checks exact DB_NAME, and creates no stock directly. PowerShell requires a recently verified TEST backup, hardcodes the TEST catalog, and cleanup retains ledgers while revoking synthetic access. The ownership problem above remains despite these data guards.

## Verification and limits

- `node --check scripts/verification/v2-housekeeping-runtime.mjs`: PASS.
- No C# build, SQL execution, or browser run performed by this reviewer; the parent owns runtime verification.
- The HTTP chain asserts consumable actual usage and final reusable ownership, but does not itself exercise revision update / attempted mutation of a retired revision, profile-change rejection, unauthorized cross-branch configuration, or start retry idempotency. Add focused runtime assertions before treating those guarantees as independently tested.
- No unresolved business-policy question identified.
