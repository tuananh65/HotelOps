# UC112 — Complete Assigned Task: COMPLETE_VERIFIED (isolated)

Checkpoint65, 2026-11-15. Newly completed: **UC112 only**. Requirements: SRS 11.10, GBR001/002/005/007/008/010/016/132/138 and decision65. [Final direct runtime receipt](checkpoint65-submission-final.json): success=true, 53 records, including 37 named submission assertions and two browser results with 12 stages. These counts are not 53 independent automated tests.

| Acceptance criterion | Direct evidence |
| --- | --- |
| Authenticated, scoped current executor | Anonymous POST with valid CSRF401; missing CSRF400/ETag428; revoked executor grant, wrong assignee and foreign branch403 |
| All three SRS executor roles | HK_STAFF full browser/API path; MAINTENANCE_STAFF and STOREKEEPER each submit and close their interval with existing catalog grants |
| In Progress and actual work | Paused409; absent current interval409; Pending Verification repeat409; physical parent remains IN_PROGRESS with completed_at null, and no open interval after submission |
| Completion form and mandatory results | Canonical Complete task form exposes criteria, result, evidence and handover; missing required result returns MSG03 and exact label; user corrects and submits; recorded FAIL is accepted for later verification |
| Notes and evidence | Separate multiline result/handover persist; 4001-character note rejected without writes; invalid second file returns MSG23 and rolls back the first upload; private PNG and browser PDF download verified |
| Atomic results, actual end, history and audit | Audit-insert failure rolls back results, event, interval, workflow, history and attachments; missing criteria also roll back partial result edits; committed source joins the exact closed interval timestamp and audit identity |
| Concurrency | Two concurrent Submit calls return [200,412]; fresh repeat409; no duplicate submission interval; Pending Verification progress edit409 |
| Snapshot and capability | Missing capability503; pending state without committed submission409; later browser submission preserves the earlier FAIL snapshot while recording its own PASS result |
| Durable Department Head notification | Revoked recipient receives no delivery; recipient-insert failure rolls back notice/receipt while retaining source; concurrent retries reconcile one event and one recipient |
| Actual background worker | [Hosted worker trial](checkpoint65-worker-final.json): opt-in worker recovers one committed synthetic delivery, persists one notice/recipient after repeated polls, and leaves source snapshot unchanged; owned host stopped |
| Canonical browser handoff | Executor login → Complete → correct MSG03 → upload PDF/notes → Submit → MSG09/Pending Verification → reload → logout. Manager login → inbox → Open task → scoped snapshot → private PDF → reload → logout |
| Current source authorization | Manager source200; after role revocation source404 and snapshot403; stored ActionUrl does not provide the destination |
| Cleanup and schema governance | Both whole clones and owned backups removed; [source task cleanup](checkpoint65-task-cleanup-final.txt) and [full source cleanup](checkpoint65-source-cleanup-final.txt) pass; original trusted task CHECK restored, no submission table/marker/fault trigger, zero private files or HTTPS listeners |

Task creation/assignment, Working workday and active work interval are synthetic preconditions. Criteria initialization uses its existing authorized API, but this is not a claim that all creation/assignment UCs are complete. Independent role/browser cases reset only the owned fixture workflow and open a synthetic interval; those resets do **not** implement UC105 Return. The worker recovery fixture restores only its synthetic manager and removes only the selected delivery receipt/notice, preserving the committed source event and audit.

The snapshot is application write-once with an UPDATE-denial trigger; this trial does not certify resistance to privileged database deletion. Evidence projection retains the existing bounded progress snapshot (latest 100 evidence entries); complete historical evidence pagination is separate. UC105 Verify/Return, UC107 Decline and full FE11 acceptance remain open. Shared/production capability installation and worker enablement were not performed.

Fault injection produces SQL334 at the EF OUTPUT insert because the temporary fault trigger exists; it is an intentional insert failure proving rollback, not proof that the trigger's THROW51865 executed. Final API/probe build: zero warnings/errors. Known runtime warnings remain for RoomType indexes and unordered Take; Vite keeps use-client and large-chunk warnings. Earlier narrow [trial1](checkpoint65-submission-trial1.json) passed; the final run additionally covers all SRS actors and the fail-closed pending read.
