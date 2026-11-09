# FAST008 remaining dependencies

## R1 trusted business point

The appropriate candidate point is the new-Inspection creation transaction in UC041 AssignInspectionAsync, after an explicit responsible-supervisor selection and server-derived Inspection/task/room/branch/current-cycle identity. Existing AssignInspectionRequest has only InspectorId, DueAt, Reason; SRS UC041 selects inspector, not a separate persisted responsible supervisor.

No selected supervisor can be derived from inspector, AssignedBy, creator, session, role name or latest assignment. The existing UC041 writer/DTO is unchanged. Before adding a writer: approve explicit selection/qualification and reassignment semantics, conditional Inspection+binding revision checks, schema ownership and audit actor. Do not backfill legacy Inspections. The candidate table/trigger and reader are ready, but no authoritative setter is registered or exposed.

Candidate fields: Inspection PK/FK, task/room/branch FKs, explicitly chosen responsible supervisor FK, exact current-cycle revision, Inspection revision at binding (historical snapshot, not compared forever after the root touch), binding rowversion, explicit actor/time/reason. Trigger validates relation/current cycle/root revision, audits insert/update set-wise and touches affected Inspection roots atomically. Removal/rebinding through DELETE is deliberately rejected pending an actor/removal contract. Schema validation is not permission to assign responsibility. Qualification must be enforced by the future trusted setter, not inferred by the reader.

## FE04 R04

Pinned R03 remains d37e99f42eaaf3089eb510c6298296a0f0743faa. No exact R04 original-cleaner source package/commit was supplied. Remote searches for named original-cleaner/R04 branches returned no pin; that is not evidence no local R04 exists. No FE04 worktree was edited.

Required adapter input: exact InspectionId/CleaningTaskId/RoomId/BranchId/current-cycle revision. Required output: independently persisted immutable original-cleaner identity and accepted invalidation semantics. It must support advisory roster discovery with no retained downstream locks, then authoritative re-read in the caller transaction. IInspectionOriginalCleanerSource is a closed FE05 port awaiting that exact contract; the default throws. OriginalCleanerFixture appears only in isolated tests and is not an authority implementation.

## Evidence storage

ReworkAuthorityReader hashes bytes read through existing HistoryFile. That read closes its file handle; a second digest read is not an immutable lease. D4 invalidates metadata/link mutations, not a private file overwritten in place without metadata changes.

Before opening writers, the storage owner must guarantee immutable/version-addressed evidence for hashing, commit and subsequent FE04 consumer read: no overwrite or replacement of a referenced version; replacements must create a distinct version/key and update metadata through D4-protected paths. Need an accepted file-version/retention/access guarantee and tests for overwrite, replace, delete, rollback and rebind. A FileShare.Read lease alone only covers its lifetime and does not establish post-commit immutability. No storage schema or new digest authority was invented.

## N01/runtime and owner assertions

Recipient source-read acceptance remains null by PL direction. N01 store still requires its exact registered SQL lane/lease; no remote/shared connection, retarget, combined schema execution or production issuer registration occurred. Concrete owner coordination compiles and checks retained session/actor, branch, transaction, catalog, post-state, operation, receipt, issuer hash, audit, selected set, FE04 proof/snapshot and persisted projection audit. Positive full-graph completion/replay/proof/projection remain NOT_RUN until accepted policies/substrate/authority/storage exist.
