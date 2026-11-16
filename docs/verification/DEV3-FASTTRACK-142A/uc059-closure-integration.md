# UC059 closure integration ? partial source implementation

The existing Verify service previously closed Maintenance/Incident without releasing Incident room blocks. It now invokes a caller-owned SERIALIZABLE participant after the verified closure transition. It locks affected rooms and complete block ranges, checks branch coherence, releases only this Incident's currently effective blocks, records release actor/time/note and before/after audits, then recalculates effective readiness from persisted block and cleaning/inspection facts. Other Incident/manual blocks and future blocks are preserved. READY is projected only when the existing GBR-044 calculator allows room use; physical changes create room_status_history with the existing MAINTENANCE trigger. No synthetic BLOCKED enum or OUT_OF_SERVICE mapping is introduced.

All writes use the existing DbContext and authorization persistence boundary within the closure transaction; no nested commit. A readiness/source/authorization failure aborts closure with its block/history writes. Verify and Return now require native If-Match and return the existing scoped detail plus updated ETag, excluding unscoped material disclosure.

Evidence: inspected EF RoomBlock/RoomStatusHistory mappings, existing FE05 approval/readiness implementation, and disposable SQL CHECK constraints. MAINTENANCE is an allowed history trigger; only internal_request_escalations exists in that disposable, with no Incident escalation storage identified. No database mutation or generated entity/schema change was performed.

Validation: backend build PASS (zero warnings/errors), 18 existing readiness/submission regressions PASS. They cover readiness ordering and submission-source parsing, not the new SQL transaction. Solution dotnet test exits zero without discovery. SQL transaction/races/rollback and HTTP execution of this participant are NOT_RUN. No frontend changes.

Production Verify/Return admission remains closed. Remaining UC059 acceptance: approved Incident escalation persistence/closure, closure/return/asset notification intents and delivery, submitted result review UI with evidence/material authorization, Return-to-executor work interval lifecycle, complete production review authorization, and end-to-end/rollback/concurrency validation. No UC COMPLETE_VERIFIED claimed.

Files: MaintenanceWorkService.RoomClosure.cs (new), MaintenanceWorkService.Workflow.cs, MaintenanceWorkService.Commands.cs, and this receipt.
