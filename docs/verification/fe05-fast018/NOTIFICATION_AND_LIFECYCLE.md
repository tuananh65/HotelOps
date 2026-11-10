# Exact remaining notification / lifecycle contract

## Existing support (unchanged)

Services/Notifications/Fe05NotificationContracts.cs defines Fe05NotificationKind InspectionFailed=1 and CleaningReworkRequested=2 only. ValidateSource requires COMPLETED/FAIL/FAILED; it cannot represent Assigned or Passed. No approved event type/version, producer, read-policy descriptor or recipient profile exists for UC041/044. Do not relabel Cleaning Assigned, bypass this check or add a parallel publisher.

Fe05OperationStaging.cs supplies Fe05OperationRequest, ReserveAsync, completed-operation verification, staging/completion/Verify using the retained caller connection/transaction and NotificationCommitGuard. Fe05SourceCoordination.cs and Fe05Decision08Recipients.cs provide Users-first discovery/retained-roster coordination. Reuse that graph after acceptance. Current operation request identifies an existing Inspection; UC041 reserve-before-create needs an accepted pre-Inspection task/cycle operation-resource binding, not a fabricated InspectionId.

## Minimal additions needing acceptance, not implementation authority

| Requirement | UC041 Inspection Assigned | UC044 Inspection Passed |
|---|---|---|
| Business recipient (proposal per SRS) | Selected inspector, explicitly persisted assignment | Original cleaner from R05 exact attribution |
| Exact event/version/producer/read-policy | NOT_DEFINED / approval required | NOT_DEFINED / approval required |
| Source | Branch, room, cleaning task and explicit current cycle; pinned checklist version; persisted new Inspection/inspector/supervisor binding; source and post revisions | Exact PASSED Inspection/task/current cycle/version and trusted durable conclusion receipt; authorized R05 AssignmentId/CleanerUserId/CompletionProgressId/CompletionRevision/WriterTransactionId |
| Minimal summary payload proposal | Operation/actor/branch/room/task/Inspection/version IDs, inspector, explicit supervisor, due time, source/post revisions and server occurrence time; no private evidence | Operation/actor/branch/room/task/Inspection/version IDs, PASS conclusion and revisions/time; cleaner attribution bound server-side; no private evidence |
| Read authorization | Existing grants + exact assigned Inspection/cycle relationship; session/account/branch eligibility at staging; recipient role alone grants nothing | Existing grants + exact attributed task/cycle summary relationship; no broad INSPECTION_VIEW/private bytes grant; acceptedConcreteBinding remains null |
| Canonical command proposal | Stable client operation ID with separate UC041 namespace; actor/branch/task/cycle/checklist, selected inspector/supervisor, due, reason, source revisions; currently no accepted idempotency transport on candidate | Separate UC044 operation namespace, Inspection/cycle/version/source revisions and canonical existing command; existing IInspectionApproveAcceptedBinding seam |
| Atomic/replay requirement | Users roster locked ascending before downstream locks; reserve before create; assignment + binding + audit + required staging + durable receipt + verify + single guarded Serializable commit; completed replay validates fingerprint/current reads before returning stored identity | Same caller connection/Serializable transaction; trusted PASS writer + durable receipt/audit + required staging + exact FE04 proof/projection; Verify before commit; completed replay must not rerun writer |

Any mandatory failure rolls back; changed roster requires rollback, never late Users lock. Unknown commit requires reload/recovery, never automatic mutation retry. Assignment public contract/reassignment/schema and new notification bindings must be accepted before dispatch. UC044 original cleaner reader must follow prior exact Inspection/branch authorization; CompletionRevision is neither Inspection nor current task rowversion. No event string/profile or permissions are invented here.

Existing UC044 seam: InspectionApproveComposition.cs / IInspectionApproveAcceptedBinding RequireAcceptedAsync, ReserveOrReadCompletedAsync, StageRequiredAsync, CompleteWithTrustedSqlReceiptAsync, VerifyCompletedAsync. Its closed binding remains closed.

## UC047 criterion mapping

Authoritative criterion facts are persisted inspection_checklist_items joined to inspection_results through checklist_item_id and the EXACT room_inspections.version_id. FE04 InspectionRoomProjectionAdapter.cs reads RequiredCriteriaPassed from active required criteria with PASS results and no required non-PASS; HasFailedCriterion from persisted FAIL results belonging to that same version. It additionally requires trusted receipt, active branch/current cycle, terminal Inspection/task state and open-Rework/block checks.

FAST016/017 fe05_reinspection_results_v1 stores Fixed/Not Fixed keyed by selected ORIGINAL defect identity; persisted binding retains original_version_id and assigned_version_id separately. This is not inspection_results.outcome. All Fixed proves only remediation evaluation, not mandatory checklist PASS; Not Fixed does not itself create an Inspection criterion FAIL. Original defects may reference a different version and may have no criterion association. No safe automatic one-to-one mapping is approved.

Proposed operational plan stays All Fixed -> Inspection PASSED/Rework COMPLETED/task COMPLETED; Any Not Fixed -> Inspection FAILED/Rework FAILED/task REWORK_REQUIRED, CanApply=false. Approval needed: original-vs-assigned version execution policy, full mandatory criterion evaluation, mapping for version differences/unlinked/multiple defects, accepted FAILED Rework schema/status, audit/revision/proof/staging and projection lifecycle. Retain exact selected defects and source revisions; no automatic next-round Rework. Current Fixed/NotFixed reader/proposal and original FE04 projection remain unchanged. Existing FAST017 compatibility tests already deny projection with missing criterion facts; no new redundant framework was added.
