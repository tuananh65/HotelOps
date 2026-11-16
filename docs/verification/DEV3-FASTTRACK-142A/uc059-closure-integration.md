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
