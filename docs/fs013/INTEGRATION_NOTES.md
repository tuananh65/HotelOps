# FS-013 integration notes

Development checkpoint only. Notification remains **PROVISIONAL_NOT_ACCEPTED**, disabled by default and restricted to the exact FS-013 disposable local database and owner marker. Neither A01 nor A02 is accepted by this change. FS-012's missing executable shared-source findings are carried forward without another discovery/audit cycle.

## Changes and shared impact

- UC029 retains the existing authenticated service, SQL write boundary, immutable command and evidence verifier. FS013 adds a separate opt-in tuple and procedure name; FS001–FS012 trial tuples and scripts remain intact.
- `RoomTypeBatchService.BatchAsync` adds an optional callback after sorted user eligibility locks and before branch/catalog locks. Only the new cleaning methods use it to acquire the existing WorkTime participant locks. Other call sites retain their behavior. No global DI, authorization registration, generated entity or mapping changes.
- UC030 uses the same scoped DbContext and caller-owned transaction to close the previous owner's interval, end the old assignment using the existing schema status `REASSIGNED`, create the new assignment, update the task/readiness, audit and persist two mandatory notification intents. Historical replay returns the stored result; it does not repeat the mutation or promise that the historical assignee is still current.
- The service validates durable-result identity, state, notification contract and strong hexadecimal ETag before commit/response. SQL explicitly casts rowversion to binary before hexadecimal conversion. Earlier provisional FS013 rows written during the failing development runs are retained as evidence; malformed historical response contracts fail closed with 503 and are never rewritten or treated as accepted history.
- WorkTime saves its own interval/audit changes. The Housekeeping caller clears those saved tracked entities before the final SQL session revalidation; no pending write is discarded.
- Delivery is explicitly invoked for one actor/operation in the development harness, never a hosted/global dispatcher. Previous assignees receive only an ended-assignment receipt without source ID or action URL. New assignees require current source access. This content/event decision needs shared Notification acceptance; reusing the existing notification type is not approval of a new shared event contract.
- UC031 opens intervals through the existing `SqlWorkTimeTransactionParticipant`. It requires a Working workday, current ownership, eligibility, a current task ETag and no other open interval. Global permission never overrides current ownership. First actual start is preserved across reassignment.

## SQL boundary and unresolved contracts

All new DDL targets only `HotelOps_Canonical_20261106_f130001_Fresh`, verifies local SQLEXPRESS and `DEV1_FS013`, and refuses overwriting existing lifecycle tables. No existing trial, shared database, production database or EF migration was modified.

The repository has Workday/WorkInterval entities, mappings and participant source, but no executable shared deployment DDL. Script 11 reproduces those existing columns/mappings only in this disposable trial. It is not accepted WorkTime deployment SQL, and does not activate the `[NonController]` Workdays controller or implement the other WorkTime use cases. Live fixture workdays use the participant directly and cannot establish public Workday HTTP acceptance.

The room readiness ledger is additive trial infrastructure. `CLEANING_IN_PROGRESS` is added only to this new trial's readiness constraint. Legacy room status/history and final Reception/Operation Manager transition permissions remain unresolved; no new role grants or permission codes were invented.

No persisted cleaning blocker open/resolved contract exists in the current schema. A historical `BLOCKED` progress log cannot prove resolution. Resume fails closed with `BLOCKER_RESOLUTION_CONTRACT_REQUIRED`; UC031 is partial. UC033 needs a reviewed blocker persistence/recipient contract before real lifecycle writes.

The restricted `fs013_app` SQL boundary covers candidate assignment/reassignment writes. The complete application runtime still uses the local developer integrated identity in these trials. Broad application least-privilege acceptance and privileged direct-DML resilience must not be inferred from the scoped boundary proof or from service-level checks.

## Readiness

No FE-14/FE-15 implementation, PR, merge, Tester contact, new handover ZIP, TESTER_READY or TESTER_ACCEPTED transition is authorized by this evidence. FE-04 remains in development until the remaining UCs and shared critical paths pass acceptance and Project Leader approves review/merge.
