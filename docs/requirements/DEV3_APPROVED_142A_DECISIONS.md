# DEV3 approved FASTTRACK-142A decisions

Recorded 2026-11-15 from explicit user approval in the active sprint. These decisions override conflicting historical drafts/catalog grants. They do not authorize shared HotelOpsDB or production mutations.

## UC110: TASK_PAUSE_REASON

Approved initially active codes: WAITING_MATERIAL, WAITING_EQUIPMENT, WAITING_APPROVAL, ACCESS_BLOCKED, SAFETY_ISSUE, OTHER. Reason is mandatory. Note is optional for the first five, required and non-whitespace for OTHER; enforce actual persistence length. Resume requires no new reason.

Use reference_values group_code/value_code uniqueness, never fixed IDs. Server validates group/code/active/note. Preserve historical meanings and existing deactivation. Seed idempotently only in verified disposable SQL. Only the current authorized executor may Pause In Progress or Resume Paused. Preserve ETag, responsibility, workday, branch scope and existing state/version authority. State, interval, history and audit commit atomically; effort excludes paused time.

Checkpoint31 implementation: additive task_pause_events references existing progress/reference/interval rows and snapshots code/label/note (nvarchar(1000)). It does not repurpose BlockerReason or edit generated EF entities. The seed/schema script has exact instance/database/GUID/run guards; it is not a production migration.

## UC135: FE15 report authorization

Operations Manager or Owner AND REPORT_VIEW, with applicable branch/department scope. DEPT_MANAGER and all other roles are denied even with REPORT_VIEW, unless an allowed role supplies appropriate access. Enforce the same feature policy in API, UI, query and CSV export using existing role mapping/principal; no hard-coded role IDs and no global revocation of REPORT_VIEW.

UC136/137 keep their separate Administrator actor; do not apply UC135 policy to them. Preserve CSRF, snapshot consistency, CSV safety and audit. Add positive/negative tests, especially DEPT_MANAGER with REPORT_VIEW.

Checkpoint32: feature policy and preview/CSV API admission/audit are implemented and HTTPS/SQL tested. Canonical report UI remains pending; no full UC135 acceptance.

## Remaining contracts

FE13 read authorization remains unresolved. Database edits must remain narrow and isolated.

## Autonomous execution directive (checkpoint39)

DEV3 may decide routine technical/SRS-defined matters and integrate directly on the canonical branch. Keep the original 2026-11-16 17:50 UTC deadline. Use UC v3/SRS, approved decisions and actual contracts; document narrow decisions, preserve security, and continue independent work where a dependency remains fail-closed. No shared database or production mutation, force-push or main/develop merge. No routine approval questions.

Decision39: UC035 initial assigned-source read is independent of Start/workday and does not open an interval. Reuse actual committed authority, session, ownership and CLEANING_VIEW; do not lower proof DML-denial checks. Canonical notification/rework login returns use the existing safe local destination registry. UC046 completion is assessed at its explicit Pending Acceptance handoff, separately from UC035/047. UC109 completion is assessed for an already assigned In Progress/Paused task, separately from creation/completion UCs. Exact acceptance evidence and remaining runtime limits are in CHECKPOINT-39.md.

## FE05 effective readiness (approved after checkpoint31)

### DEV3 technical decision41: isolated restricted SQL runtime

Use SQL Server application-role activation for the disposable Development HTTPS trial. The existing principal proof-DML denial remains mandatory; there is no special successful path for an administrator. Activation uses direct RPC, no reversal cookie, encrypted nonpooled connections, and no ambient transaction enlistment. Verify actual server/database GUID and ownership marker before activation; verify the effective role and denied database CONTROL afterwards. Invalid identity/secret/configuration closes the connection. Every new EF connection and the raw startup schema check must activate the role before application queries. No server login, server setting, shared database, generated entity or production configuration change is authorized by this trial.

Temporary roles receive SELECT/metadata and only the authentication/rework/audit/interval writes needed for the selected workflow, with explicit proof DML denial. Passwords are random and passed only in process memory/environment. The verifier owns host shutdown, role removal, synthetic-row and private-file cleanup. This is an isolated acceptance mechanism, not a production deployment plan. SQL behavior was checked against [Microsoft application-role documentation](https://learn.microsoft.com/sql/relational-databases/security/authentication-access/application-roles) and [connection pooling guidance](https://learn.microsoft.com/en-us/sql/connect/ado-net/sql-server-connection-pooling).

Keep existing rooms.operational_status values. Never write BLOCKED into that column or translate it to OUT_OF_SERVICE. Calculate effective readiness using GBR-044: inactive first, then any effective block, then current cleaning/inspection lifecycle. Passed inspection is Ready only without effective blocks and with all lifecycle conditions met. Removing one block must recalculate from current data, including other blocks and the current cycle.

APIs, Room Status Board, Dashboard and all room-usability consumers must use effective readiness. Unsupported or unknown readiness must fail closed for use decisions. Preserve history, transaction, concurrency and MSG17. Verify Passed+block, multiple blocks, removal, failed inspection and concurrent block changes. Do not alter generated entities or CHECK constraints to introduce status codes.

Checkpoint32 actual schema finding: UX_room_blocks_active uniquely constrains room_id for unreleased rows, conflicting with multiple incident-room blocks. A narrow guarded disposable-only trial replaces it with uniqueness on (room_id,incident_id), matching GBR-075. The trial is rolled back together with synthetic fixtures; no lasting schema deployment has occurred. Existing room_status_history CHECK values and room_readiness_events' PENDING_CLEANING-only CHECK are retained; effective transition history/writer composition is still pending.

## UC045 rejection recipients (DEV3 approved, checkpoint35)

On Failed inspection, notify every active Housekeeping Supervisor with valid access to the inspection branch and INSPECTION_VIEW, plus the actual cleaner identified by official cleaner attribution. Deduplicate to one notification per user/event. Recheck recipient eligibility before delivery and source authorization when opening the record. Persist notification intent in the Reject transaction; rollback must publish nothing. Preserve retry, idempotency and audit, without expanding inspection read permissions.

UC045_SUPERVISOR_RECIPIENT_SCOPE=ALL_ELIGIBLE_SUPERVISORS_IN_INSPECTION_BRANCH. No shared database mutation or production deployment is authorized.

## UC046: Request Rework assignee (approved during checkpoint37)

Apply GBR-046: active account/employment, valid active HK_STAFF assignment, current branch access and matching active Housekeeping department. Request Rework and its options do not require a currently active PLANNED shift. Preserve separate Accept/Start/Resume shift and workday guards. Require current Failed inspection, no open rework, selected failed defects, assignee and future UTC deadline; create Pending Acceptance plus audit atomically. Assignee notification must follow commit and deduplicate. No shared database or production changes.

## DEV3 implementation decision42: UC036 submission handoff

Under the autonomous directive, submission uses the existing rework workflow/version and work interval authority. The disposable-only schema trial adds the three narrowly scoped remediation fields already proposed by the original candidate (note nvarchar(500), user attribution, server timestamp), and an attachment link table referencing the existing selected defects. It does not edit generated entities, introduce a second task state or deploy a shared migration. An owner-executed registered writer persists the completed operation receipt, audit and notification intent in the caller's serializable transaction; runtime cannot directly write the protected operation ledger or source proofs.

Requirement conflict: UC036 normal-flow step 6 says Pending Inspection unless Blocked, while GBR-044 explicitly puts "never passed inspection" before submitted/waiting inspection. Preserve the approved first-matching GBR-044 evaluator: rework is Pending Reinspection, but effective room readiness is Pending Cleaning for a never-passed room, Pending Inspection for a previously passed room, or Blocked when an effective block exists. None is usable. Keep the cleaning cycle Rework Required until UC047 concludes; do not prematurely complete the cycle or overwrite the physical room status. This is a documented precedence choice, not a claim that the two SRS statements agree.

Until UC041 assigns the next inspector, notify current eligible branch Housekeeping Supervisors (active HK_SUPERVISOR, valid branch access and INSPECTION_VIEW), rechecking at delivery. Use the same approved supervisor eligibility policy as UC045; do not infer the new inspector from the original failed inspector or current executor. Delivery retries the durable submission intent and deduplicates by rework occurrence/user. Source links remain fail-closed until a supervisor reinspection source endpoint is composed. No source permission is expanded to deliver a notification.

## DEV3 implementation decision43: exact reinspection assignment (in progress)

UC041 assignment for Pending Reinspection must name the submitted rework, verify its protected UC036 receipt, and bind the new inspection to that exact rework, cycle and selected original defects. The generic cleaning-task assignment may no longer infer a rework. Preserve current session, branch INSPECTION_PERFORM, active HK_SUPERVISOR actor/assignee, separation of duties, future UTC deadline, one open inspection, strong ETag and operation replay checks. Binding, assignment audit and durable notification intent share one serializable transaction. Existing unaccepted candidate flags remain unchanged.

The first composed lane uses the original checklist version only while it remains Published/effective (GBR-056), preserving the exact criterion identity of every original defect (GBR-065). A retired source version remains fail-closed; this is a remaining compatibility gap, not full UC041/047 completion. No label-based remapping or inferred checklist version is accepted. Initial-cleaning assignment, reassignment and the full conclusion/UI/delivery chain still require separate acceptance evidence.

The disposable-only proposed schema retains existing task state authority and generated entities. It adds binding/results/evidence links using existing keys. UC047's ended Failed rework is proposed as workflow FAILED with physical COMPLETED, preserving all existing CHECK alternatives; the trial captures and restores the exact previous CHECK. This mapping and conclusion writer are still under validation and are not a shared schema deployment. Only the named, GUID/run-verified disposable database may be used, with synthetic fixtures, rollback rehearsal and cleanup.

## DEV3 implementation decision46: UC047 projection compatibility

SRS appendix specifies reinspection ASSIGNED directly to PASSED/FAILED. Existing ordinary-inspection triggers permit checklist results only while INSPECTING and defects only while INSPECTING/PENDING_CONCLUSION. The disposable trial extends these two guards only for an ASSIGNED inspection with a protected exact rework binding. Criterion outcomes must equal the aggregate Fixed/Not Fixed decisions for that criterion; remaining defects must match selected Not Fixed source defects. Ordinary trigger predicates, generic API exclusion, source immutability and terminal protection remain enforced. No public Start transition, disabled trigger or generated-entity change is used to make conclusion pass.

This is a narrowly scoped, reversible isolated compatibility trial, not authorization to deploy to a shared database. Capture original trigger definitions before installation, rehearse cleanup under rollback, restore the original guard bodies and existing workflow CHECK, and clean only synthetic objects/rows/files. A discovered fixture-cleanup error (attempting workflow NULL, rejected by the source-immutability trigger) required identity-guarded recovery; recovery and its independent zero-row/zero-file verification are retained in checkpoint46 evidence. Production acceptance still requires both outcome workflows, canonical UI, read-after-commit and notification verification.

## DEV3 implementation decision48: terminal reinspection read

UC047 reload uses the same assigned-inspector INSPECTION_VIEW admission, active HK_SUPERVISOR eligibility, branch and source identity. A terminal read additionally requires the protected assignment/submission/conclusion chain, matching audit and current inspection/rework revisions, and complete recorded decisions. It returns the immutable conclusion separately from freshly computed effective room readiness. A later block change must not rewrite the historical receipt or make its readiness current authority. The mutation helper keeps terminal admission disabled. No role permission, generated entity, schema or state value is added by this read extension.

Executor notification opening remains a separate dependency: CLEANING_VIEW ownership must not be replaced with INSPECTION_VIEW or supervisor access. The current assigned-rework reader excludes COMPLETED/FAILED; compose its protected terminal acknowledgement before making a conclusion notification source navigable. Delivery must retain current recipient checks, durable intent, retry/idempotency and transaction rollback. This checkpoint does not claim delivery or a new permission grant.
## DEV3 implementation decision49: executor reinspection acknowledgement and Passed notification

UC047 normal-flow step 8 requires an in-app notice to the housekeeper; its Failed alternative ends at Request Rework. Deliver the existing protected REINSPECTION_PASSED intent to the submission actor only, after fresh CLEANING_VIEW ownership, active HK_STAFF, branch and Housekeeping department checks. Do not interpret the Failed intent as a new recipient policy. The original assignment eligibility query is shared without changing its conditions or introducing a shift requirement for reads.

The executor's existing rework route may show Completed/Failed only after validating the protected submission, exact assignment, conclusion, current source revisions and audit. It exposes a minimal result acknowledgement and the executor's existing defect/remediation evidence, not inspector commands or inspection read permission. Fresh effective readiness is separate from readiness at conclusion. A notification's canonical source path is resolved only after current recipient authorization and committed producer verification; stored ActionUrl is not authority. Delivery is atomic, retryable and deduplicated per terminal rework/recipient. No generated entities or database schema changes are needed for this composition.

## DEV3 implementation decision51: exact original inspection for the assigned reinspector

UC047 normal-flow step 2 requires a link to the original inspection. The generic inspection execution endpoint intentionally authorizes only its original inspector; that rule is unchanged. A separate read-only endpoint derives the original ID from the committed reinspection binding after current session, INSPECTION_VIEW, active HK_SUPERVISOR eligibility, branch, independent inspector and protected assignment/submission/conclusion validation. It accepts no caller-supplied original ID. It returns the exact original checklist/results, defects, summary and inspection-owned evidence; it excludes task-wide files and other inspections. Every download repeats the same authorization and proof checks in a serializable transaction. No mutation controls, new role permissions, generated entities or database schema changes are introduced.

The distinct-inspector acceptance fixture creates a separate synthetic supervisor using the existing role catalog and branch access. Its identity creation/cleanup is rehearsed under rollback, its exact owned references must be cleaned before deletion, and it runs only within the verified disposable fixture. It does not modify the original inspector to manufacture the test or weaken historical source proof checks.

## DEV3 implementation decision53: retirement after assignment and UC047 acceptance boundary

UC040 prevents a Retired checklist version from being used for a new inspection; GBR-057 preserves the version of an inspection already created. An exact reinspection assigned while its version was Published therefore retains that version and its criteria after retirement. Retirement must not invalidate the assignment, remap historical defects or authorize another assignment. Current role, branch, source proof, concurrency and mutation guards still apply. The isolated acceptance fixture retires only its own exact version, rehearses rollback, compares historical content and inspection/binding revisions, and deletes the owned graph normally at cleanup. It never un-retires the version or disables a trigger. This fixture is not UC040 API acceptance.

UC047 explicitly starts with a Pending Reinspection task already assigned through UC041. Its Failed alternative ends when Request Cleaning Rework opens; POST-01 permits a new rework round to start. A committed next Pending Acceptance task and its assignee source establish that handoff. Executing that next round to Passed remains a broader composed-demo task, not an extra prerequisite invented for this individual UC. The already-Retired-before-assignment compatibility gap remains in UC041; no label-based criterion remapping is authorized. Earlier checkpoint status descriptions grouped these dependencies with UC047; the final acceptance ledger must distinguish them and retain the broader sprint work.

The UC047 form must show the MSG03 field-specific missing-result error and the MSG08 saved confirmation. Keep native required-field validation, server validation and the existing explicit confirmation step; no authentication, CSRF, ETag or lifecycle guard changes are needed.

## DEV3 implementation decision54: opening a Reject notification

UC045/GBR-010 requires current source authorization when a recipient opens the notification. Resolve INSPECTION_FAILED only from a currently delivered, retained inbox row belonging to the authenticated user. Validate the unique matching Reject audit, producer/time, Failed inspection and official cleaner attribution using the same source verifier as delivery; require original recipient membership and current branch INSPECTION_VIEW. A supervisor additionally must remain an eligible branch HK_SUPERVISOR; the actual original cleaner is identified by the immutable completion attribution, never the current assignment. Stored ActionUrl is ignored. No new permission, role mapping or schema is introduced.

The canonical destination reuses the existing read-only inspection history workspace/API. The host first admits the exact branch/inspection through an authorized detail read, remounts on authority changes and permits no inspection mutation. Every history/evidence request retains its existing server policy. A notification does not grant history access. The shared source verifier uses serializable held read locks; delivery retains ordered eligibility locks, its audit/update locks and transactional deduplication. Reading a delivered inbox row must not request an unnecessary update lock on the inspection in the reverse order of delivery.

This closes the immediate notification-to-source handoff. It does not certify all UC048 history filtering/pagination behavior or the entire UC045 exception/rollback acceptance. Those require their own evidence; source after later cleaning-cycle transitions remains a lifecycle regression target.
