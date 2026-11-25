# Independent replacement workflow service/SQL verification

Database: `HotelOpsCompactWorkdayTestV22611250010` only. Synthetic users, sessions, memberships, roles, shifts, room, assets, maintenance work and checklist are created inside SERIALIZABLE outer transactions and rolled back after each test. No real passwords, production data, SMTP or AI touched. This is actual service + SQL integration, not HTTP/UI E2E.

## Defect found

`WorkService.Replacement.cs` assigns snapshot via `db.WorkEvents.Local.Last(e => e.WorkId == id)`. EF Local enumeration is not guaranteed to append newly added entities after existing saved events. Running propose → confirm → approve in one context produces a confirmed event with NULL snapshot; approve throws `ArgumentNullException` at `JsonSerializer.Deserialize<ReplacementSnapshot>(last.SnapshotJson!)`. First run: 8 PASS / 6 FAIL, all fixtures rolled back. Evidence: `artifacts/restructure-v2/replacement-shared-context-results.json`.

Suggested fix: retain the new WorkEvent reference, or select the exact added event by WorkId + ActorUserId + OperationKey before assigning SnapshotJson. Do not rely on Local.Last ordering. It is an integration/composability defect; the ordinary controller uses a fresh request scope and is being separately checked with tracking cleared per command.

## Validation

- Node launcher syntax: PASS (`node --check`).
- Harness build: PASS; NU1900 warning because the NuGet vulnerability feed is unreachable. No C# errors.
- Fresh request tracking run: **14 PASS / 0 FAIL**, including rollback cleanup, in `artifacts/restructure-v2/replacement-runtime-results.json`. Includes a populated PASS checklist that is reset upon installation, real asset identity handoff, independent confirmation/approval, wrong branch/actor, stale version, rejection reason, pending completion block, and original/new asset location gates.
- An intermediate run failed fixture setup because its work item lacked the matching checklist_template_id for the composite foreign key. The fixture was corrected before the passing run; no business source was changed.
- SQL fixture creation bypasses normal asset registration/work creation intentionally; tested business methods are real WorkService.ReplaceAsset/ReplacementQueue/ReplacementDetail/Act.
- No persistent server process started.

Unresolved questions: none.
