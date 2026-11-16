# UC059 closure integration ? partial source implementation

The existing Verify service previously closed Maintenance/Incident without releasing Incident room blocks. It now invokes a caller-owned SERIALIZABLE participant after the verified closure transition. It locks affected rooms and complete block ranges, checks branch coherence, releases only this Incident's currently effective blocks, records release actor/time/note and before/after audits, then recalculates effective readiness from persisted block and cleaning/inspection facts. Other Incident/manual blocks and future blocks are preserved. READY is projected only when the existing GBR-044 calculator allows room use; physical changes create room_status_history with the existing MAINTENANCE trigger. No synthetic BLOCKED enum or OUT_OF_SERVICE mapping is introduced.

All writes use the existing DbContext and authorization persistence boundary within the closure transaction; no nested commit. A readiness/source/authorization failure aborts closure with its block/history writes. Verify and Return now require native If-Match and return the existing scoped detail plus updated ETag, excluding unscoped material disclosure.

Evidence: inspected EF RoomBlock/RoomStatusHistory mappings, existing FE05 approval/readiness implementation, and disposable SQL CHECK constraints. MAINTENANCE is an allowed history trigger; only internal_request_escalations exists in that disposable, with no Incident escalation storage identified. No database mutation or generated entity/schema change was performed.

Validation: backend build PASS (zero warnings/errors), 18 existing readiness/submission regressions PASS. They cover readiness ordering and submission-source parsing, not the new SQL transaction. Solution dotnet test exits zero without discovery. SQL transaction/races/rollback and HTTP execution of this participant are NOT_RUN. No frontend changes.

Production Verify/Return admission remains closed. Remaining UC059 acceptance: approved Incident escalation persistence/closure, closure/return/asset notification intents and delivery, submitted result review UI with evidence/material authorization, Return-to-executor work interval lifecycle, complete production review authorization, and end-to-end/rollback/concurrency validation. No UC COMPLETE_VERIFIED claimed.

Files: MaintenanceWorkService.RoomClosure.cs (new), MaintenanceWorkService.Workflow.cs, MaintenanceWorkService.Commands.cs, and this receipt.

## Incident escalation storage and automatic closure

UC057 explicitly requires one Open escalation per Incident, automatically closed when the Incident is Closed or Cancelled. Added a disposable-only additive installer and internal closure participant. Storage retains opening event/time/reason/requested support, and closure event/time. Composite event-source foreign keys prevent linking a closure event from another Incident; no cascade deletes or EF entity edits. A filtered unique index prevents two Open escalations. Existing storage is rejected for comparison, never overwritten.

Maintenance Verify/Cancel now require installed escalation capability before performing the workflow. After the actual Incident status event is saved, the participant verifies persisted event actor, branch, Incident status, event type and timestamp under the caller SERIALIZABLE transaction, then closes Open escalations. Before/after audits use that same transaction and persistence revalidation; any missing capability or failure rolls back closure. Return does not close escalation. No public Escalate/Verify/Cancel admission or grants are opened by this change.

Direct evidence: disposable identity rechecked; installer ApplyChanges=0 succeeded and table, source unique key and marker absence were subsequently verified as ROLLBACK_CONFIRMED. Incorrect zero GUID rejected with SQL 51845 before mutation. No persistent schema/data change. Backend build PASS (zero warnings/errors); 39 existing Maintenance/readiness tests PASS. Solution-level test exited zero without discovery. These tests do not execute the new DML participant, its event proof, same-Incident foreign keys with synthetic rows, concurrency, or full API rollback; those remain NOT_RUN. No complete UC claim.

Additional files: IncidentEscalationClosure.cs and scripts/verification/CanonicalDatabase/IncidentEscalation.sql; MaintenanceWorkService.Commands.cs and this receipt updated. Remaining: escalation creation eligibility/authorization and acknowledgment notification, UC059 closure/return notifications, reviewer UI/admission and runtime validation.

## Return notification intent and delivery

Return now adds a versioned MAINTENANCE_RETURNED intent and normalized reason to the owning work audit, in the same transaction as the existing RESOLVED-to-IN_PROGRESS Incident event and work log. A separate delivery service binds the exact audit transition, original/current assignee, reviewer, Incident/branch and matching status event. Later resolution/closure/cancellation events supersede the actionable notice; event IDs also handle equal timestamps. A reassignment does not redirect the original recipient.

Delivery uses the existing configured notification worker with an independent cursor, retained audit discovery, per-source application lock and idempotent notification/recipient checks. Both current Maintenance read access and source relevance are checked before delivery and again before commit. No eligible recipient leaves the intent available for retry within configured retention. The notification contains generic text; the reason is read through the scoped work detail. Opening the source revalidates current session, recipient and read authorization. No new grants, implicit read access or worker enablement.

Validation: backend build PASS with zero warnings/errors; 16 focused Return/submission/read tests PASS (six new Return parser cases). Solution test exits zero without discovery. SQL delivery, duplicate/race/rollback behavior and browser opening remain NOT_RUN. No database mutation or external messages sent. Public review admission is still closed, so this source increment does not declare UC059 complete. Verify closure notifications to reporter/technician/HK Supervisor and asset-condition follow-up are still outstanding, as are Return executor restart and full review UI/authorization.

Files: MaintenanceReturnSource.cs, MaintenanceReturnDelivery.cs, MaintenanceReturnSourceTests.cs (new); MaintenanceWorkService.Commands.cs, NotificationCenterService.cs, TaskNotificationWorker.cs, Program.cs and this receipt updated.

## Returned-work executor restart

The existing Return transition leaves physical Maintenance/Incident IN_PROGRESS without starting executor effort at reviewer time. Resume now accepts this state only when the current executor owns an exact, current, unused versioned Return audit/status-event proof. The source query rejects legacy/missing evidence, reassigned recipients, superseding submissions/closures, any open source interval, and any interval started since or closed after Return. ON_HOLD Resume keeps its existing contract.

The write checks this proof under current execution authority, native ETag and user/time/Incident/Work locks. The shared interval participant still enforces Working workday, branch/source eligibility and single running interval. A successful restart consumes the native work version even though the physical state remains IN_PROGRESS, and audits Return source ID plus the actual new interval opening time. ActualStartAt is preserved; reviewer wait time is never fabricated as effort. Existing transactional rollback and no-automatic-retry behavior remain.

Detail exposes CanResume for this proven returned state; the canonical Resume panel accepts the server flag and uses the existing CSRF/ETag endpoint. No new status, grant, generated entity or database mutation. No public review admission opened.

Validation: backend build PASS (zero warnings/errors); frontend typecheck/build PASS with existing React Router use-client and bundle-size warnings; 20 focused execution/Return/read tests PASS; four mocked browser tests PASS (Hold, Resume, Start, returned-work Resume). Solution test exits zero without discovery. These tests do not execute the new Return-proof SQL, workday/interval transaction or concurrent restart, which remain NOT_RUN. UC059 remains PARTIAL pending Verify notifications, full reviewer UI/authorization and SQL/HTTPS acceptance.

Files: MaintenanceReturnedExecution.cs (new), MaintenanceWorkService.Commands.cs, MaintenanceWorkService.Logs.cs, MaintenanceReadContract.cs, MaintenanceResumeContract.cs, MaintenanceResumePanel.tsx, maintenance-hold-resume-browser.test.mjs and this receipt.

## Direct disposable SQL escalation evidence

Ran IncidentEscalationRollbackProbe.sql against the revalidated Recovery_2b221f40 disposable (recovery GUID 5270D07C-7C27-477B-A60E-86485ADD7FAD). The probe owns an outer transaction, includes the guarded installer as a nested transaction, and creates only synthetic branch/user/Incident/event/escalation rows. No role/grant changes, no shared database access, no FK/CHECK/trigger disabling. Expected constraint errors are statement-scoped within the test; the outer transaction is always rolled back.

Actual SQL result: PASS for duplicate Open rejection (unique index), foreign-Incident closure event rejection (composite FK), successful closure preserving opening event/time in OUTPUT, repeated closure affecting zero rows, and rollback cleanup of all fixture rows plus table/source unique key/capability marker. This provides direct database constraint and DML evidence. It does not execute IncidentEscalationClosure through EF, authenticate an HTTP request, validate full Maintenance transaction rollback, or exercise concurrent sessions. Those acceptance checks remain outstanding; UC059 is still PARTIAL.

Only the focused SQL probe and this receipt changed in this increment. No C#/frontend changes; builds were not rerun. The probe's PASS is direct execution evidence, not a historical handoff claim.

## Verified closure notifications ? reporter and technician

Verify now writes a versioned MAINTENANCE_VERIFIED intent in the work audit within the owning closure transaction. It binds the original technician, Incident reporter, reviewer, exact COMPLETED-to-CLOSED transition, review timestamp and note. The delivery source additionally proves current CLOSED Work/Incident and the matching RESOLVED-to-CLOSED Incident event; reopened or reassigned sources do not remain actionable.

Reporter and technician candidates are deduplicated and independently authorized. A technician with current Maintenance read scope opens the scoped Maintenance detail. A reporter with current Incident source-read scope opens Incident detail. Neither role relation supplies missing grants or cross-branch access. When one person fulfills both relations, one recipient is written; a valid Maintenance target takes precedence. Notification text is generic and does not disclose the verification note.

Delivery follows the existing retention, per-event lock, retry and current authorization revalidation design, using its own worker cursor. Opening revalidates session and the exact authorized target. No actual notifications were delivered in this run; worker defaults were not enabled and no database changes were made.

Build PASS with zero warnings/errors. Twenty focused closure/return/read tests PASS, including six new closure parser cases. Solution test exits zero without discovery. SQL worker idempotency/races, rollback and HTTP opening remain NOT_RUN. Housekeeping Supervisor delivery awaits confirmation of a Room Status source with ROOM_VIEW instead of broadening INCIDENT_VIEW; asset-condition follow-up is still missing. Production review admission remains closed and UC059 is still PARTIAL.

Files: MaintenanceClosureSource.cs, MaintenanceClosureDelivery.cs, MaintenanceClosureSourceTests.cs (new); Program.cs, MaintenanceWorkService.Commands.cs, NotificationCenterService.cs, TaskNotificationWorker.cs and this receipt updated.

## Canonical Return API and UI admission

A004A-032 Return now has a compiled production contract, independent of still-closed Verify. It requires HTTPS/current unrestricted SQL session, an active Maintenance Lead with current MAINTENANCE_REVIEW BRANCH/GLOBAL and independent scoped MAINTENANCE_VIEW. Current assignees, recorded performers and previous submitters are denied by the existing separation helper. Reviewer authority was extracted unchanged from submission notification eligibility so both paths use the same predicates. Actor and current assignee locks precede the service transition; exact root/request/action binding, native ETag and Serializable persistence revalidation are mandatory.

The existing service requires COMPLETED/RESOLVED, restores an eligible current assignee, writes reason/audit/Return notification intent, leaves room blocks untouched, and starts no interval. The executor subsequently uses the previously added proof-bound Resume. Canonical detail exposes CanReturn with final authority revalidation; the dedicated panel requires a ten-character reason and confirmation, sends fresh CSRF and the original ETag, validates the returned identity/version/state and never retries automatically. Generic unversioned A004A-032 is excluded.

Validation: backend build PASS, zero warnings/errors; frontend typecheck/production build PASS with existing use-client and bundle-size warnings. Thirty-nine focused Maintenance/Dev2 tests PASS, including new exact Return binding/HTTPS/no-Verify-admission checks. Two mocked browser tests PASS for success and stale ETag. Solution test exits zero without discovery. These do not prove real SQL authorization, transactional rollback or full UI-to-HTTPS-to-SQL Return; runtime acceptance remains outstanding. No database mutation, production deployment or grant change. UC059 remains PARTIAL; Verify UI/admission and remaining notification recipients are still open work.

Files: new MaintenanceReturnContract.cs, MaintenanceReviewAccess.cs, MaintenanceReturnPanel.tsx, MaintenanceReturnContractTests.cs, maintenance-return-browser.test.mjs; updated Dev2ActionContracts.cs, MaintenanceReadContract.cs, MaintenanceSubmissionSource.cs, MaintenanceWorkDetailDto.cs, MaintenanceReadView.tsx, Workspace.tsx, SharedAuthHost.tsx and this receipt.
# UC059 approved Housekeeping room-release notification increment

Project Leader decision implemented: each committed room-release audit carries INCIDENT_ROOM_RELEASE/version 1, the room/work/Incident identities and released block IDs. No marker is emitted when no effective block was actually released. This intent is saved by the existing closure transaction, not a pre-commit notification send.

IncidentRoomReleaseSource validates the room release audit, matching released blocks, a matching verified closure audit and the RESOLVED -> CLOSED event. Historical closure evidence is used rather than requiring the Incident to remain Closed forever. Delivery discovers current HK_SUPERVISOR candidates, retains user locks and serializable source reads, and checks active user/account/role, explicit branch access, ROOM_VIEW grant and room/parent coherence. OWN or unrelated-branch grants cannot substitute for branch room access. It rechecks source and recipients before commit. Per-event application locking plus existing recipient lookup prevents duplicate recipient delivery; failures leave the retained audit intent discoverable for retry.

Only generic room-release text is disclosed. EffectiveRoomReadiness is recalculated at initial delivery; remaining Blocked is explicitly stated as a delivery-time fact. No message claims that releasing an Incident block makes the room Ready. Notification opening revalidates current session and recipient authority, ignores stored ActionUrl, and returns the existing `/rooms/{id}?branchId={branch}` route. Its existing ROOM_VIEW-protected directory endpoint recomputes current effective readiness. No INCIDENT_VIEW requirement, grant or role assignment was added. Reporter/technician delivery is preserved. Inbox now permits only the exact server room route and existing Maintenance/Incident read routes, including fixed read action IDs.

Direct evidence for this increment: backend solution build PASS, 0 warnings/errors; frontend TypeScript/production build PASS with existing React Router directive and large-chunk warnings. Focused release/closure source tests: 11 PASS. Frontend notification transport/session/CSRF/path tests: 9 PASS (mocked HTTP). Solution test command exits zero without discovering tests. Notification SQL delivery, role revocation races, full HTTPS closure and rollback remain NOT_RUN. No notification was sent and no shared database was mutated by this increment.

UC059 remains PARTIAL: the Asset-condition Operations Manager follow-up and full production Verify admission/UI/evidence review still remain, alongside end-to-end QA. Newly completed UCs: 0. The separate UC139 production EF boundary probe ran on the verified disposable database and rolled back its synthetic fixture; that is not UC059 evidence.
# UC059 linked Asset follow-up increment

The verified closure audit now captures the linked Asset ID and condition only when the Asset is DAMAGED or UNDER_REPAIR, with matching Incident branch. No Asset field is updated. This retained audit is the transactional notification intent. IncidentAssetFollowupDelivery discovers Operations Managers, retains user/source locks, rechecks active user/role, branch access and independent ASSET_VIEW scope before delivery/commit, and deduplicates recipients under an event application lock. No grant is created. OWN access requires the current asset custodian, consistent with the existing asset resource facts.

The notification opens its own `/notifications/{id}/asset-condition` receipt. A dedicated HTTPS GET revalidates delivered-recipient ownership, current session and Asset access, and returns only Asset identity/name/branch, condition at closure, current condition and closure time. No Incident diagnosis, evidence, financial fields or unadmitted Asset mutation is exposed. The UI clears stale data on identity/route changes, distinguishes historical/current condition and does not claim closure automatically repairs an Asset. This is a read-only follow-up; the existing Asset mutation remains independently gated.

Direct validation: backend build PASS (zero warnings/errors); frontend TypeScript/production build PASS, existing React Router directive and large-chunk warnings remain. Focused backend source/scope tests: 16 PASS. Frontend notification path/session/CSRF tests: 10 PASS using mocked responses. Solution test exits zero without discovery. No SQL mutation, delivery execution or full browser/HTTPS/SQL closure was performed for this increment.

UC059 remains PARTIAL, not IMPLEMENTED_PENDING_QA: production Verify admission and reviewer UI/evidence review are still missing. This increment closes the Asset-notification source gap but does not establish live delivery or full UC acceptance. Newly completed UCs: 0.
# UC059 submitted-review binding increment

The scoped Maintenance detail read now exposes a submitted snapshot only to an eligible non-performing Maintenance Lead reviewer. It requires exactly one matching current submission audit, synchronized Work/Incident resolution timestamps and the closed executor interval. Evidence IDs additionally require independent Incident read scope. The read contract is V4 and revalidates the reviewer and snapshot before disclosure. The UI displays submitted diagnosis/action, audit ID/time and authorized evidence links separately from mutable work history.

Verify now requires a positive SubmissionAuditId. Before changing state it binds that ID to the current submission, validates material ISSUE lineage and this work's original usage quantities, and compares evidence metadata/content hashes to the submitted snapshot. Aggregate consumption by other work is not treated as a change to this work's historical usage. Evidence is re-read and compared immediately before commit. Closure audit records verified_submission_audit_id. Missing legacy snapshots or changed evidence cannot be inferred from resolution_note; they fail validation. No generated entities or schema were modified.

Direct evidence: backend solution build PASS, zero warnings/errors; frontend production build/typecheck PASS with existing Vite warnings. Eight focused reviewer/submission/Return contract tests PASS. Solution test command exits zero without discovery. Live SQL material/evidence reconciliation, filesystem races and browser closure remain unverified. No database mutation was performed.

UC059 remains PARTIAL. Verify production admission and the dedicated confirmation form still need to bind the displayed snapshot and native ETag, including independent material/evidence access and lock ordering. This increment deliberately does not enable the legacy generic Verify form or claim a complete business workflow.

## UC059 source completion: Verify admission and confirmation

Status: IMPLEMENTED_PENDING_QA. New source-complete UCs in this batch: 1 (UC059). New COMPLETE_VERIFIED UCs: 0. This supersedes the earlier PARTIAL source notes above, not their runtime limitations.

A004A-031 now has a dedicated compiled production contract. Admission requires HTTPS, the current unrestricted SQL session, exact actor/request/work binding, a positive current submission audit ID, native rowversion storage, active scoped Maintenance Lead review/read authority and performer separation. Inventory catalog locking precedes user/source locking. Existing material usage additionally requires independent MR_VIEW/INVENTORY_VIEW and complete provenance; legacy header-only usage or missing closure storage fails closed. Authority is rechecked at save and final disclosure without incorrectly requiring the pre-closure COMPLETED state after it has been persisted.

The detail read exposes CanVerify only when the current submitted snapshot and required capabilities are available. The dedicated form binds that snapshot and ETag, confirms the verification note, obtains fresh CSRF, performs one POST, and validates the returned work identity, CLOSED state and changed native version. The unversioned generic Verify form is excluded. Existing Return remains independently admitted.

SRS source reconciliation (02_use_case_specifications.md, UC059):

| Acceptance path | Source implementation |
| --- | --- |
| Open Incident and review result, evidence, materials and deadlines | Existing Incident-to-maintenance list/detail navigation; MaintenanceReadContract and MaintenanceReadView expose current submitted snapshot, authorized attachment links, independently scoped usage and planned dates. Planned end is not presented as an invented SLA deadline. |
| Accept and Close with current reviewer and version | MaintenanceVerifyContract, MaintenanceVerifyPanel, RequireReviewProofAsync and native If-Match; exact submitted quantities and evidence hashes revalidated. |
| Close Incident/work and Open escalation | Existing MaintenanceWorkService.Workflow/Commands and IncidentEscalationClosure in the owning Serializable transaction; persisted transition event is required. |
| Release Incident blocks and recompute room readiness | MaintenanceWorkService.RoomClosure locks room/block ranges, releases only effective blocks of this Incident and uses EffectiveRoomReadiness; other blocks remain. No automatic Ready when GBR-044 is unmet. |
| Atomic history and notification intent | Owning transaction records work/Incident/blocks/room/escalation audits and release/closure intents; failed transaction cannot produce a committed delivery source. |
| Reporter, technician and eligible HK Supervisors | MaintenanceClosureDelivery and IncidentRoomReleaseDelivery; current independent target permissions, per-event deduplication, retry, and authorization again on opening. HK source is Room Status, not Incident. |
| Return for more work | Existing dedicated Return form/contract/service preserves blocks, records reason/audit/technician intent; executor must explicitly Resume before a new work interval. |
| Linked damaged/under-repair Asset | IncidentAssetFollowupDelivery and authorized read-only follow-up; no automatic condition mutation or grant expansion. UC070 itself remains a separate workflow. |

Direct validation this batch: dotnet build HotelOps.slnx --no-restore PASS (0 warnings/errors); frontend npm run build (tsc + Vite) PASS, with existing React Router ignored use-client and bundle-size warnings. Focused backend tests: 18 PASS. Mocked browser tests: 4 PASS (Verify and Return success/stale ETag, CSRF, exact snapshot payload and no automatic retry). dotnet test HotelOps.slnx --no-restore exits zero without test discovery; it is not counted as test coverage. git diff --check PASS.

No database mutation, live closure, notification delivery or deployment was performed. Human QA must exercise authenticated UI -> HTTPS -> disposable SQL, real role/grant revocation, changed evidence/materials, concurrent room blocks, closure/return rollback and retry/deduplication. Runtime requires verified native maintenance version/material storage, guarded escalation installer and controlled notification worker activation. Missing prerequisites remain fail-closed. EF escalation DML materialization and complete closure transaction are not established by the mocked browser or parser/contract tests. This is source completion, not COMPLETE_VERIFIED or runtime acceptance.

Files in this batch: MaintenanceVerifyContract.cs, MaintenanceVerifyAvailability.cs, MaintenanceVerifyPanel.tsx, MaintenanceVerifyContractTests.cs and maintenance-verify-browser.test.mjs (new); MaintenanceReadContract.cs, Dev2ActionContracts.cs, MaintenanceWorkDetailDto.cs, MaintenanceReadView.tsx, Workspace.tsx, SharedAuthHost.tsx, MaintenanceReturnContractTests.cs, UC_STATUS_MATRIX.md and this receipt (updated).
