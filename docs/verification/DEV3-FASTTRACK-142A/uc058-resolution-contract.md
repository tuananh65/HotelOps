# UC058 result validation increment — 2026-11-16

Status: PARTIAL_IMPLEMENTATION. Newly COMPLETE_VERIFIED UCs: 0.

GBR-077 and UC058 require diagnosis/root cause and action taken. The previous service accepted only an unstructured ResolutionNote. SubmitMaintenanceResultRequest now requires both explicit fields; an optional additional note cannot substitute for either. Whitespace-only values are rejected before execution. Existing resolution_note storage (nvarchar(max) in the release schema and unbounded string in EF mappings) receives labeled normalized content, preserving historical notes without parsing or rewriting them. The transactional maintenance audit additionally records submitted_diagnosis and submitted_action_taken explicitly. Actual end continues to come from the closed work interval's server timestamp, not client input.

Submit now requires the native Maintenance execution ETag after source locks, before interval closure. Its result uses the scoped read query and updated ETag without disclosing material usage. The existing transaction still owns interval, state and audit writes. No generated EF entity, schema, status, grant or notification policy was changed.

The frontend operation catalog matches the new required fields. A004A-030 is NOT added to the production contract registry or canonical host enabled actions. This is not an operational Submit release: material/evidence validation and verifier notification must be completed before admitting the mutation. Maintenance Lead execution and actual HTTPS/SQL acceptance also remain unverified.

Direct evidence:

- Backend solution build PASS, zero warnings/errors.
- Frontend TypeScript/production build PASS. Existing React Router directives and bundle-size warnings remain.
- Focused result-validation and execution-binding tests: 12 PASS.
- Solution test command exits zero without test discovery; not counted as coverage.
- No API/SQL runtime execution or database mutation in this increment.

Changed files: SubmitMaintenanceResultRequest.cs; MaintenanceResolution.cs; MaintenanceWorkService.Workflow.cs; MaintenanceWorkService.Commands.cs; frontend/src/dev2/catalog.generated.ts; MaintenanceResolutionTests.cs; this receipt.

Next: bind result attachments to the Incident under existing evidence permissions; revalidate material consumption against posted issue/return provenance with correct lock order; persist verifier notification intent in the Submit transaction with current branch/role/read authorization. Then expose the complete versioned Submit form and verify the real lifecycle.

## Submission material validation increment

Submit now acquires the existing inventory catalog and original issue-document locks before its user/time and Incident/Maintenance source locks. Before closing the work interval, it revalidates every recorded usage against the existing FE09 fulfillment/request/approval/posted-issue provenance checker. It checks the work-log association, item, historical base/issue UOM, conversion factor and exact base quantity. The existing capacity participant checks total consumption across all works plus posted returns without adding current consumption a second time. A header-only legacy MaterialIssueId is rejected for reconciliation rather than interpreted as zero usage. No material is required when no usage was recorded.

The same maintenance submission audit captures the validated usage IDs, quantities/UOMs, source issue/request/fulfillment IDs and aggregate issue/consumption/return quantities at submission. This does not claim immutable receipt storage or authorize users to read inventory data; the existing scoped response still omits material disclosure.

Direct checks: backend build PASS, zero warnings/errors; existing resolution/execution regressions 12 PASS before the audit snapshot addition; final build after that addition PASS. Solution test exits zero without discovery. The SQL-dependent material-validation branches, concurrent return/write behavior and rollback are NOT_RUN. The legacy integration runner requires database mutations and an HTTP-only fixture, so it was not run against an unverified target. No database mutation, schema change or frontend change in this increment. Submit production admission remains closed; evidence binding, verifier notification and live acceptance remain outstanding.

## Existing result evidence binding increment

Submit accepts optional ResultAttachmentIds for already uploaded Incident evidence. IDs must be distinct and positive. The service requires independent current Incident detail read authority, exact INCIDENT/source identity and undeleted attachment rows. The existing private evidence reader verifies generated storage keys, file existence, size, MIME and reparse-point restrictions. Client paths/URLs are not used. Existing upload authorization remains separate; this change does not grant upload access or write files.

Submission audit captures attachment/source IDs, display name, MIME, byte length, SHA-256, upload author and timestamp. Immediately before commit the service repeats scope/content validation and compares the proof. Missing, deleted, mismatched or changed evidence aborts submission. Audit provenance does not make the file immutable; later download still requires source authorization, and future verification must use the recorded hash if it needs to establish identical content.

Direct checks: backend build PASS (zero warnings/errors), ten focused resolution/Incident evidence tests PASS. These test result-selection validation and existing evidence validation; they do not prove the new SQL service path or concurrency/rollback behavior. Solution-level test exits zero without discovery. No database or frontend mutation; production Submit and its dedicated evidence-selection UI remain unopened. No newly completed UC.

Changed files: SubmitMaintenanceResultRequest.cs, MaintenanceResolution.cs, MaintenanceWorkService.Commands.cs, new MaintenanceWorkService.ResolutionEvidence.cs, MaintenanceResolutionTests.cs and this receipt. Next required work is durable verifier notification, the dedicated versioned Submit form and real transaction/security acceptance.

## Verifier notification source increment

Submit now writes a versioned MAINTENANCE_SUBMITTED notification intent into its existing transactional audit. No notification is emitted in that transaction. A dedicated delivery service, registered with the existing configured task-notification worker, discovers committed intents within the configured retention window. It validates the exact IN_PROGRESS -> COMPLETED audit transition, actor/assignee/source/time and current COMPLETED/RESOLVED pair plus the closed interval before delivery. Returned/reopened/stale submissions are not delivered as current verification requests.

Recipients are current active Maintenance Leads in the source branch with eligible MAINTENANCE_REVIEW and independently scoped MAINTENANCE_VIEW. The submitter and users with Maintenance work intervals on the work are excluded. Delivery locks candidate users and serializes each audit event with a transaction application lock, rejects conflicting existing notification/delivery records, and revalidates recipient permissions and source before committing. No eligible recipient leaves the committed intent discoverable for retry. Existing recipients are not duplicated on subsequent worker passes. Source navigation revalidates current session, current review/read authority and non-performer eligibility before returning the existing maintenance-detail route. No new roles/grants, database schema or worker configuration defaults were introduced.

The source remains the existing audit, subject to the existing audit-retention/archive policy; this does not claim a new immutable receipt. Discovery currently revisits intents within retention to support late eligible recipients, while delivery is idempotent. Runtime load and SQL races still require verification.

Direct evidence: final backend build PASS with zero warnings/errors after correcting a raw-string brace compile error; 11 focused source-intent/result tests PASS. These cover parsing and binding rejection, not SQL authorization/delivery races. Solution-level test exits zero without discovery. SQL/HTTPS notification creation, rollback and retry remain NOT_RUN. No live notifications were sent and no database was modified in this increment. Production Submit remains unopened pending its dedicated form, complete authority integration and live acceptance. Newly COMPLETE_VERIFIED UCs: 0.

Files changed: MaintenanceWorkService.Commands.cs, new MaintenanceSubmissionSource.cs, new MaintenanceSubmissionDelivery.cs, TaskNotificationWorker.cs, NotificationCenterService.cs, Program.cs, new MaintenanceSubmissionSourceTests.cs and this receipt.

## Assigned Staff Submit UI/API integration

A004A-030 now uses the production execution contract with exact controller/action/root/request binding, current HTTPS SQL session, current assigned Maintenance Staff execution permission, independent Maintenance read permission and existing branch/source checks. Submit acquires the inventory catalog in the authorization transaction before user locks; the owning service retains source/version, material, evidence, interval and audit validation. Native execution rowversion capability remains required. No other legacy action is admitted.

Canonical Maintenance detail exposes a separately gated Submit capability for a currently running owned interval. The new form requires diagnosis and action taken, displays the server-time completion policy, selects existing authorized Incident evidence by name and can reuse the existing separately authorized uploader. Submission waits while evidence uploads are busy. It reviews input, obtains fresh CSRF, sends the original ETag once and validates returned identity/status/version/time. Stale/uncertain outcomes require reload. Session changes abort outstanding requests; read-only workspaces suppress Submit. The generic unversioned form excludes A004A-030.

Direct validation: backend build PASS, zero warnings/errors; frontend typecheck/production build PASS with existing React Router directive and large-bundle warnings; 19 focused backend tests PASS; two browser tests with mocked HTTPS endpoints PASS (success and stale ETag, required fields, selected evidence, CSRF/version payload and no automatic retry). Solution-level test exits zero without discovery. These are not live SQL/HTTPS acceptance results. No database mutation or deployment performed.

UC058 remains PARTIAL_IMPLEMENTATION: Maintenance Lead executor coverage, production progress/material-entry workflow and actual SQL/HTTPS acceptance are still outstanding. No newly COMPLETE_VERIFIED UC. Next high-impact work: finish that actor/material path and verify Start -> Hold/Resume -> Submit -> reviewer notification on the verified disposable runtime.

Files: MaintenanceResumeContract.cs, MaintenanceReadContract.cs, MaintenanceWorkDetailDto.cs; MaintenanceSubmissionPanel.tsx (new), IncidentEvidencePanel.tsx, MaintenanceReadView.tsx, Workspace.tsx, SharedAuthHost.tsx; MaintenanceExecutionAccessTests.cs, maintenance-submit-browser.test.mjs (new); this receipt.

## Maintenance Lead executor and review separation

UC054/055/058 name Maintenance Lead as an execution actor. The execution predicate now accepts either active MAINTENANCE_STAFF or MAINTENANCE_LEAD with its current MAINTENANCE_EXECUTE grant; independent scoped Maintenance read access, exact current assignee, branch access, source coherence, session, rowversion and workday/interval guards remain mandatory. No grants are created. Read capability and Workday Pause All reuse this predicate, so the same actor rule applies across Start/Hold/Resume/Submit and end-of-shift handling.

Review segregation now uses one helper for notification eligibility and Verify/Return service transitions. Current assignees, recorded Maintenance interval performers and prior submitters evidenced by IN_PROGRESS -> COMPLETED audits are excluded, including after reassignment. Legacy submission audits do not need the new notification marker. This is bounded by retained source evidence and does not reconstruct missing legacy/archived history. Verify/Return production admission remains unchanged.

Direct validation: backend builds PASS with zero warnings/errors; 18 focused execution/read/source regression tests PASS, including allowed Lead execution and revoked/foreign-branch/read-only/review-only grant rejection. SQL execution of the new historical-performer query and real Lead workflow remain NOT_RUN. No database mutation or frontend change. UC058 remains partial for production progress/material-entry and full SQL/HTTPS acceptance; no newly completed UC.

Files: MaintenanceExecutionAccess.cs, MaintenanceResumeContract.cs, new MaintenanceReviewSeparation.cs, MaintenanceWorkService.Workflow.cs, MaintenanceSubmissionSource.cs, MaintenanceExecutionAccessTests.cs and this receipt.

## Scoped material availability

A004A-034 now has a compiled production read contract and is enabled in the canonical Maintenance workspace. Existing HTTPS/current SQL session and scoped Maintenance read checks are supplemented with independent INVENTORY_VIEW and MR_VIEW. Material Request visibility is applied to source candidates before source proof, capacity calculations, total count and paging; an OWN grant cannot expose another requester's issues. The final disclosure boundary rechecks current grants and denies narrowed scopes. Inventory catalog locking precedes user/source locks. The existing source-provenance/capacity logic and current IN_PROGRESS/ON_HOLD corrective-work checks remain in place.

The legacy service signature is retained for compatibility; the production read handler calls a distinct scoped method and bypasses the legacy controller read. No generated entities, schema or grants were changed. This supplies the protected source picker data for the next material-entry step; production CreateLog/material usage mutation is still outstanding. UC055/058 remain partial.

Files: MaintenanceReadContract.cs, MaintenanceWorkService.Materials.cs, new MaintenanceMaterialAccess.cs, SharedAuthHost.tsx, new MaintenanceMaterialAccessTests.cs and this receipt. SQL translation, OWN filtering against real records, concurrent usage/return capacity and HTTPS execution remain NOT_RUN. No database mutation.

Validation: backend build PASS with zero warnings/errors; frontend typecheck/build PASS with existing warnings. The first focused run found the existing accepted-read list assertion still expected only list/detail; it was updated to include the new read action while retaining no-mutation assertions. Final focused result: six tests PASS. Solution-level test exits zero without discovery. MaintenanceReadAccessTests.cs was also updated. These offline checks do not verify live material availability.

## Versioned progress/material-write backend

A004A-029 CreateLog now has exact action/root/request production admission under the existing current-session, assigned Staff/Lead execution and Maintenance read checks. Material-bearing requests additionally require current INVENTORY_VIEW and MR_VIEW for every source issue line's request, linked to the same Incident. These checks repeat through the persistence boundary. Nonzero client SpentMinutes is not admitted by this production contract: actual effort remains derived from work intervals. Note-only logs do not gain or require inventory access.

All log writes require the owning Maintenance ETag under source locks. The existing material source/capacity validation and work-log/usage audits remain transactional. A no-op update of existing status advances the owning native rowversion for a child log write without changing lifecycle state; the response includes the resulting ETag. The material response snapshot is read inside the transaction and restricted to this new log, replacing the prior post-commit usage query. The inventory catalog is acquired before user/source locks by the contract.

The dedicated canonical progress/material-entry form is still outstanding; A004A-029 has not been added to the host's enabled actions. SQL validation of source scope, concurrent consumption/returns, rollback and ETag retry remains NOT_RUN. No database/schema mutation performed. Backend build PASS with zero warnings/errors; solution-level test exits zero without discovery. No newly completed UC.

Files: MaintenanceResumeContract.cs, MaintenanceMaterialAccess.cs, MaintenanceWorkService.Commands.cs, MaintenanceWorkService.Logs.cs, MaintenanceWorkLogDto.cs, MaintenanceExecutionAccessTests.cs and this receipt.

Focused verification for this backend increment: 15 execution/read/material-access tests PASS. They cover admission bindings and existing scope predicates; the new usage-write SQL transaction, ETag advancement and capacity races are not proven by these offline results. Edited-line CRLF diff warnings were cleaned before delivery.

## Dedicated progress/material entry UI

A004A-029 is now enabled in the canonical host and excluded from the unversioned generic action form. Maintenance detail exposes CanLog only for the current authorized executor in IN_PROGRESS/ON_HOLD with native ETag capability; final disclosure revalidates execution access. The dedicated panel records a required technical note and optional multiple material usages selected from the independently protected paginated availability endpoint. Selection survives paging; only source-proven units are offered, with positive quantity/three-decimal/capacity feedback. Note-only recording remains available when material reads are forbidden. Actual effort is never entered by the client.

Writes bind the current session and original ETag, bootstrap CSRF, require explicit confirmation, verify returned log identity/version/material records, and do not automatically retry. Existing server source, authorization and transactional capacity checks remain authoritative. No database mutation or schema changes in this increment.

Validation: backend build PASS (zero warnings/errors); frontend TypeScript/production build PASS (existing React Router use-client and bundle-size warnings). Three browser tests PASS using mocked HTTPS responses: paginated multi-line selection/quantity validation, note-only with material 403, and stale ETag without retry. Initial browser run exposed an ambiguous select accessible label; an explicit label fixed it and all three reran successfully. Fifteen focused backend execution/read/material-access regressions PASS. Solution-level dotnet test exited zero without test discovery; it is not counted as coverage.

Live browser-to-HTTPS-to-SQL, concurrency/rollback and persistent rowversion capability deployment remain unverified. UC055/058 remain PARTIAL; newly completed UCs: zero. Changes are left uncommitted for review.

Files: MaintenanceProgressPanel.tsx (new), MaintenanceReadView.tsx, Workspace.tsx, SharedAuthHost.tsx, MaintenanceWorkDetailDto.cs, MaintenanceReadContract.cs, maintenance-progress-browser.test.mjs (new), and this receipt.
