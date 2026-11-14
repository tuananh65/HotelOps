# INSP-004A — loader preparation, not an authoritative writer

Run `dotnet run --project tests/HotelOps.Inspection.LoaderTests`.
This executable harness tests the internal read orchestration using doubles, plus the
real SQL adapter's missing-transaction guard with a never-opened SQL connection.
It does not execute SQL or prove EF materialization, deployed schema, physical lock
ordering, live authorization or race behavior. It is not a dotnet test adapter.

Production preparation is an internal method on the existing RoomTypeCreationService
partial boundary. Request inputs contain branch/inspection identity and reject command,
never a domain graph. The reader uses existing SqlSessionService, INSPECTION_PERFORM,
assigned-inspector checks, UserEligibilityLock, branch/catalog locking, HousekeepingSchema,
HistoryRooms, InspectionState and existing generated entities. No existing file is edited.
The loader requires a clean caller-owned SERIALIZABLE SQL transaction and does not begin,
commit, rollback, clear or save that transaction/context. All entity reads are untracked.
The caller retains locks until its own transaction ends and must rollback on any failure.
Caller must enter before taking feature/location locks, matching existing user-first order.

Inspection and CleaningTask rows are read with UPDLOCK/HOLDLOCK. Related criteria, results,
defects, evidence metadata/links are read in the same SERIALIZABLE transaction. The assigned
version is read by FK without publication status selection. The root revision is read
after the locked root load. ETag comparison is then applied to that persisted revision,
not to a client-supplied snapshot. A future writer must still use its own conditional update
and revalidate authorization/source immediately before commit. Current reader lock sequence
must be reconciled with the accepted room/current-cycle participant before writer activation.

D3 remains BLOCKED_SPECIFIC_RULE: no accepted current-cycle participant exists in this
baseline. No room_cleaning_cycle_state reader/schema is copied from FE-04. The loader never
returns Success or exposes a conclusion-ready domain source; otherwise valid loads return
Unavailable / BLOCKED_SPECIFIC_RULE:D3_CURRENT_CYCLE_BINDING. Task FK is not current-cycle proof.

D4 remains blocked: root revision covers existing result/defect triggers, not all evidence
link/metadata mutations. SQL locks protect the read transaction, not stale changes before it.
Soft-deleted evidence metadata is retained for source inspection, not downloadable proof;
no file bytes/storage keys are returned and no evidence-presence business rule is invented.
D5/D7 replay, staging and notification are not invoked or implemented. No receipt/authority,
endpoint, DI registration, schema/mapping/trigger changes, conclusion or lifecycle mutation.

The four INSP-002 files and their recorded tests remain unchanged. These new tests do not
independently verify the earlier 45/45 runtime result. IMPLEMENTED means read preparation;
in-memory tests passing does not mean live loader VERIFIED, shared integration ACCEPTED,
UC045 accepted or INSP-004 complete.
