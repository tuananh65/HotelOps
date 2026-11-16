# UC117 source completion

2026-11-16: IMPLEMENTED_PENDING_QA. No new COMPLETE_VERIFIED claim.

| SRS requirement | Source evidence |
| --- | --- |
| Open In Progress request as current handler or receiving Department Head | Canonical `/internal-requests`, HistoryPage/ProgressPanel; InternalRequestProgressController/Service/Authority, current session and current assignment or explicit department management mapping |
| Show progress, notes, linked work and attachments | Progress read snapshot and private evidence pagination/download; existing independently authorized task, cleaning, incident and material panels in HistoryPage |
| Save percent, notes, result and files | Bounds/text/file validation, row version and CSRF; Serializable progress/event/audit writes and EvidenceFileLifetime cleanup |
| Progress remains In Progress at 100% | Update predicate and response retain IN_PROGRESS; Complete is a separate action |
| Notify requester when reaching 100% | Transactional progress outbox on transition to 100%; InternalRequestProgressDelivery, hosted worker and IR_READY notification source reauthorize requester |
| Change deadline only as receiving Department Head, with reason | Separate CanChangeDeadline authority, nonblank reason, deadline later than official CreatedAt, audit of previous/current values and final authority revalidation |
| MSG08 and visible saved progress | This batch adds confirmed-save callback to reload canonical history and progress snapshot, retaining success message in the parent; failed or stale responses do not invoke it |

The previous PARTIAL description included material visibility and legacy completion classification. Current material read panels already implement the former; the latter is a UC120 completion concern and is not an additional UC117 progress step. This reconciliation does not promote UC118 or UC120 and does not infer mandatory/optional legacy metadata.

Validation for this batch is recorded in the handoff. Browser checks use mocked HTTP, not SQL acceptance. Historical C98/C101/C102/C146 evidence remains historical. No database/schema/configuration changes or new grants. Human QA must exercise the full flow on disposable SQL with real sessions: current/former handler, department manager, cross-branch/revoked grants, deadline restrictions, attachments and cleanup, concurrent/stale saves, 100% notification delivery and continued IN_PROGRESS state.
