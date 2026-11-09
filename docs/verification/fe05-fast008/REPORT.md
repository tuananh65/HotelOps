# FE05-FAST-008 — PARTIALLY_BLOCKED

## Identity and implementation

- Worktree D:\SEP490\Projects\HotelOps-DEV1-FE05-FAST008; branch feature/dev1-fe05-supervisor-coordination; parent dd1dc1d19df75aa13a5bde37c76911063ed1d871.
- DEV2 DECISION08 source 8b241249b8a6b99f601ff35ee048528d0319eaf0f4d63565d758715021ab67e5. Exactly 3 changed files imported and raw SHA256 checked (decision08-import.csv). ZIP SHA256 D6E23A38938352914E265BCB9F57CF321CEFC1CF3FDB0B1EC263A614DDE16519. ACCEPT07 foundation was not recopied/reimplemented.
- 16 source/script/test files: 6 modified, 10 added; exact paths, ownership, raw SHA256 and normalized Git blobs in source-manifest.csv. Documents/receipts are additional in commit-inventory.txt.

| Gate | Implementation status | Verification/acceptance limit |
|---|---|---|
| D08-R1 | Candidate persisted supervisor table, 6 FKs, cycle/root/binding revisions, audit/invalidation and exact reader implemented | Schema consistency verified on owned synthetic SQL. Trusted UC041 explicit-supervisor setter/input/reassignment approval still missing; no production authority, backfill or shared deployment. |
| D08-A1 | Concrete fail-closed CanRecipientReadAsync plus exact source/roster checks; permission/resource mapping prepared | acceptedConcreteBinding=null by PL decision; no recipient positive source-read binding verified. |
| D08-L1 | Advisory discovery before transaction; existing sorted Users locks; immutable retained roster/physical transaction; re-read after source locks; late IDs/reassignment poison existing commit guard | Focused isolated tests PASS. No DMV/server-permission lock authority. Actual R04 source remains missing for production Inspection Failed roster. |
| D08-C1 | Concrete opt-in owner coordination, caller/session/branch/operation assertions, typed receipt/fingerprint/selected set/audit/proof/snapshot/projection assertions wired to FE05-owned seams | Default source coordination stays Unaccepted; successful full graph commit, SQL replay and authoritative handoff NOT_RUN. |

## Approved shared hook

PL explicitly approved optional onUsersLocked in RoomTypeBatchService.cs. Exact diff: shared-hook.patch. Existing UserEligibilityLock still runs once, sorted, in the caller SERIALIZABLE transaction. Only its returned dictionary is passed to the optional callback, immediately before existing Gate. No callback leaves existing Gate/operation/final-Gate flow unchanged. No other shared auth, mapping, Program, generated entity or transaction framework changed.

FE05 retention validates complete roster/key/value/eligibility, copies identity facts and binds the exact physical transaction. No late lock acquisition, no DMV, no new permission. Callback/roster errors propagate and poison NotificationCommitGuard where involved. Discovery uses the authenticated current principal and existing branch permission; SQL session recheck remains in BatchAsync inside the transaction (no illegal ISqlSessionService call outside a transaction).

## Receipts

| Check | Actual final result | Qualification |
|---|---|---|
| API solution build | PASS, 0 warnings/errors | Final changed C# source |
| dotnet test HotelOps.slnx | Exit 0 | API-only solution; no functional assertions counted |
| New structural/unit/DI tests | 28/28 PASS | Receipt-shape fixtures, no source-read or authority grant |
| Exact DECISION08 N01 NoSQL | 145/145 PASS | Explicit in-memory/scripted graph/authorization; not SQL durability |
| Schema/roster/prelock isolated SQL | 20/20 PASS | Mutations explicitly rolled back; original-cleaner fixture is test-only |
| Approved shared-hook real SQL | 6/6 PASS | Actual password credential/session and existing actor permissions; no-hook path, ordering, rollback, roster mismatch, concurrency, revocation blocking |
| Recipient exact read allow / new full completed receipt graph | NOT_RUN | Accepted concrete policies/substrate/proof missing |
| UC045/046 successful public authoritative mutation | NOT_RUN | Gates retained; no HTTPS E2E claim |

Environment in receipts/environment.json: newly created local SQLEXPRESS trial with UUID ownership guard, synthetic only. Candidate schema installation committed only in this trial; candidate supervisor records all rolled back (0 retained), 6 valid FKs, no production issuer present. Shared-hook tests committed synthetic authentication fixture sessions and read-only successful batches; injected SQL mutations/revocation attempts rolled back. No business FAILED/Rework/proof/notification committed. Older trials were not reset/dropped.

Initial local compile/test-fixture setup errors were corrected before the final receipts. After shared-hook approval, all DMV-based lock checks were removed; earlier DMV candidate test runs are superseded by the delivered hook tests. Concurrent tests cover two real Batch callers and a recipient revocation contender, not every writer ordering or a proof of deadlock freedom. SQL1205 injection and positive authoritative replay remain NOT_RUN.

## Remaining decisions and handoff

PERMISSION_MAPPING.md gives per-recipient exact resources/permissions/relations/checks/missing acceptance. DEPENDENCIES.md identifies the trusted UC041 creation point, R04 port, evidence guarantees and N01 runtime prerequisites. No responsibility or permission was inferred from actor/role/branch, and no broad INSPECTION_VIEW/INSPECTION_PERFORM grant was added.

Production/default registrations, Reject/Rework dependency gates and worker/producer OFF remain unchanged. No API opened, shared SQL, deployment, shared merge or owner-worktree modification. D4, FAST007 issuer and FE04 R03 code unchanged. Only the separately authorized minimal shared Batch hook changed.

Task estimate 70%; UC044 45%, UC045 80%, UC046 80% (UC completion estimates unchanged because no newly accepted public authoritative path). Code readiness improved; test counts do not imply acceptance.

FE04_DEPENDENCY_STATUS: NOT_READY — R03 pinned; exact R04 original-cleaner contract and same-transaction worker source-read agreement outstanding. No AUTHORITATIVE_INSPECTION_HANDOFF_READY.

Private branch commit/push identity is provided in the delivery response after scope/hash/secret checks. No ACCEPTED claim.
