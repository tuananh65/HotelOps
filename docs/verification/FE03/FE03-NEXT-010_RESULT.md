# FE03-NEXT-010 — History Live Acceptance Preparation

- PR42: OPEN / DRAFT, remote HEAD66d599713a3c6aeacd4174f1275d72fa7f92f60a verified before this task. Application SHA remains717715fa0f9fa51ba467592f1dd01ea06e8ce568; new changes are harness/docs only.
- Existing handoff inspected read-only: HotelOps-Integration/obj/env-fe03-001/ENV-CONTRACT.json, manifest.json and LIVE-HANDOFF.md. Only available runtime source is6a0c9248008203c245c347d9d6831279835ee544. No717715 handoff found in targeted local handoff filename search.
- LIVE_ACCEPTANCE_STATUS: BLOCKED_RUNTIME_VERSION. New runner rejects this old handoff at RUNTIME_GUARD with exit2, zero checks, sql_attempted=false and https_attempted=false. No credentials read, SQL query, session/login or HTTP call performed.

## Harness implementation

- HistoryAttribution.Assertions.ps1: pure source/lease/identity guards, actor and SQL-to-API page assertions, fixture-coverage preflight.
- Read-HistoryLiveAcceptance.ps1: exact expected application717715, runtime DLL ProductVersion/hash, listener PID/process path, exact database GUID/owner/token/lease via handoff helper, restricted SQL identity, isolated cookie containers and TLS validation. Read-only SQL and room GETs; auth/login/logout only when an authorized matching runtime is available.
- Reads changed_by from authorized-branch persisted SQL history and compares every API page with same-room SQL: actor/null, from/to statuses, UTC timestamp, history ID, count and ChangedAt DESC/HistoryId DESC ordering; checks cross-branch404/403, denied403 and anonymous401. Missing/null fixture coverage is BLOCKED_FIXTURE_COVERAGE, never fabricated.
- Required fixtures: an allowed ROOM_VIEW/BRANCH actor, denied actor, foreign branch/room, at least one persisted non-null changed_by and one persisted null changed_by in the allowed scope, and at least two history rows for one allowed room to prove ordering. No fixture creation or business writes in runner.
- Test-HistoryAttributionHarness.ps1:28 offline cases, synthetic in-memory inputs only. Covers recorded/null actor, missing/invalid/fallback actor rejection, wrong room/event/order/date/page/total/coverage, empty next page, source downgrade and environment guard failures, insufficient fixtures.
- Malformed-response rejection is offline harness evidence, not a claim that the live server returned malformed payloads or that new application717715 is live-verified.

## Exact commands

Run from D:\SEP490\Projects\HotelOps-FE03:

```powershell
# Safe offline test; no ENV, SQL, HTTPS or credentials required.
powershell -NoProfile -File scripts/verification/FE03/Test-HistoryAttributionHarness.ps1

# Only after a separately authorized717715 TRIAL_READY handoff exists.
# Replace this path with the actual newly approved local handoff directory.
$approvedHistoryHandoff = 'C:\APPROVED\HISTORY717715-HANDOFF'
powershell -NoProfile -File scripts/verification/FE03/Read-HistoryLiveAcceptance.ps1 -EnvRoot $approvedHistoryHandoff -ExpectedApplicationSha 717715fa0f9fa51ba467592f1dd01ea06e8ce568
```

Runtime handoff must supply ENV-CONTRACT.json and manifest.json with matching source_commit717715, TRIAL_READY, endpoint/database identity/lease, runtime PID/binary hash; Common.ps1 read helpers compatible with the existing FE03 handoff; protected Allowed/Denied/SqlRuntime credential references; fixtures and consumer authorization. Runtime/backend/HotelOps.Api.dll must match expected source and declared hash. No automatic runtime replacement, lease extension, schema/permissions change or request to ENV.

Expected exit codes:0=full live PASS,2=BLOCKED_RUNTIME_VERSION / BLOCKED_ENVIRONMENT / BLOCKED_FIXTURE_COVERAGE,1=verification failure. Inspect sanitized receipt at obj/fe03-next-010/history-live-acceptance.json; it records actual attempted SQL/HTTPS flags and never records passwords/tokens/cookies. BLOCKED and FAIL cannot produce a PASS receipt.

## Verification and safe continuation

- PowerShell parser: PASS for all3 new harness scripts.
- Offline harness tests:28/28 PASS, exit0.
- Real old handoff metadata guard: PASS (correctly blocked, exit2 before SQL/HTTPS/auth).
- No application changes relative to717715; existing backend66/66, frontend60/60, browser14/14 and builds remain accepted source evidence and were not rerun. New harness syntax/selftests do not replace live acceptance.
- Source diff/secret scan: PASS before commit. No shared Program.cs/auth/mappings/schema, other worktrees or ENV files modified.
- MUTATION_IMPLEMENTABLE: NONE_CONFIRMED / BLOCKED_CONTRACT. D01–D05 still lack approved writer/command transitions, mutation permission, version/atomic audit and block/readiness dependency contract. ROOM_VIEW is read permission only; UpdatedAt is not a version token. No speculative mutation added.
- PR_MERGE_READINESS: NOT_READY; FE03-owned harness is prepared for matching authorized runtime handoff. PR42 remains Draft; no live PASS, ready-for-review conversion, merge or deployment performed.
- Required Leader approval: explicit command-level D01–D05 approval before mutation. Required runtime evidence is separate and does not approve business rules. No new request sent to ENV/FE04/FE05/DEV2.
- NEXT_TASK: execute exact live command only against matching approved717715 handoff with sufficient persisted actor/null/order fixtures; inspect receipt and then decide Draft PR acceptance gate. Application717715 and UC027 remain NOT_ACCEPTED.
