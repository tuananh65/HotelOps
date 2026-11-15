# UC105 — verification acceptance ledger (PARTIAL)

C68 current status: **PARTIAL**, with direct and Internal Request task workflows verified. The remaining legacy MAINTENANCE/INSPECTION/CLEANING requester bindings are still gated. Requirements: SRS11.4 and UC102's optional Internal Request source, GBR001/002/005/007/008/010/012/013/133, decisions66/68. No new UC is counted complete in C68.

[C68 runtime](checkpoint68-linked-trial1.json) passed on its first run: 102 records, including 34 submission assertions, 34 verification assertions, 13 requester assertions and 42 stages across seven browser results. It verifies:

- Original InternalRequest.RequesterId differs from task creator and is stored as requested_by for linked Return/Accept; both identities remain excluded from verification.
- A requester change after review is reauthorized on POST even when task ETag is unchanged; self requester and task creator are denied403.
- Foreign source branch, inconsistent requester/receiving department, missing binding and unsupported legacy source types fail closed; no decision/state transition is written.
- Direct-history requester remains unchanged after the owned fixture is linked to a real synthetic Internal Request. The subsequent browser Return → Start/resubmit → Accept/Closed uses real public workflows and persists separate linked-source audits.
- Source Internal Request lifecycle remains unchanged; terminal private read and notification authorization regressions pass. [Actual worker](checkpoint68-verification-worker.json) delivers once without source mutation.
- [SQL cleanup](checkpoint68-cleanup-final.txt) and [files/hosts](checkpoint68-files-host-cleanup.json) prove clone/backup removal and restored source schema. No new schema or shared mutation.

The following C66 ledger remains historical evidence of the direct-task implementation, not a claim that C68 reran every earlier tool invocation.

[Final runtime](checkpoint66-verification-final.json): success=true, 86 records, including 31 verification assertions, 34 submission assertions and 7 browser results with 42 stages. These are not 86 independent tests. [Verification worker](checkpoint66-worker-final.json) and [submission worker regression](checkpoint66-submission-worker-regression.json) separately verify actual opt-in hosted delivery/retry.

| Requirement | Evidence/status |
| --- | --- |
| Authenticated Department Head, scoped authority | Anonymous401; executor403; foreign branch403; missing CSRF400/ETag428; existing DEPT_MANAGER plus TASK_MANAGE/TASK_VIEW; no catalog change |
| Review submitted results, notes, evidence and actual time | Canonical task snapshot, private evidence download, verification panel and persisted history; time equals sum of closed effective interval durations |
| Accept with verification note | Browser rejects empty note; valid Accept produces MSG08 and CLOSED; physical COMPLETED/completed_at and ended COMPLETED assignment verified in SQL |
| Return for same assignee with reason | Short reason rejected; valid Return yields Accepted/TODO and unchanged active assignee; no interval opens until executor explicitly Starts |
| Processing-cycle segregation | Submitter with manager role403/MSG22; distinct requester403; earlier performer403; performer remains excluded after Return/resubmission |
| Concurrency and exact submitted version | Foreign submission ID412; concurrent Return [200,412]; fresh decision on Closed409; exact source ID and strong ETag sent by client |
| Atomic audit/decision/history/assignment | Injected verification insert failure rolls back audit, decision, workflow, assignment and intent; validation denials preserve fingerprint |
| Repeat further-work loop | API Return → browser Start/resubmit → browser Return → browser Start/resubmit → browser Accept → Closed; three distinct decision records and original first start retained |
| Durable assignee notification | Revoked recipient denied; concurrent retries reconcile one notice; actual hosted worker recovers a committed decision notification without duplication or source modification |
| Terminal reader access | Executor opens final inbox source, reads Closed/history and downloads private PDF despite ended assignment; role revocation makes source404 and submission403 |
| Linked task sources at C66 | Historical gap: all linked sources were gated. C68 resolves INTERNAL_REQUEST with official requester/branch evidence; MAINTENANCE, INSPECTION and CLEANING remain unresolved |
| Cleanup | Three exact owned clones and backups removed; source remains empty with original trusted task/source CHECKs; trial tables/markers removed; private files and owned HTTPS listeners zero |

Creation/assignment, Working workday and initial interval are synthetic preconditions. Initial submission cases retain the C65 fixture resets; the C66 Return → Start → Submit → Verify loop uses real APIs and browser actions without resetting workflow between its three decisions. Temporary source/creator/contributor changes are negative fixtures only, not product mutation paths. Reopen remains UC106 work; Return never acts as an implicit new processing cycle.

The first [trial](checkpoint66-verification-trial1.json) stopped because its negative fixture used an unsupported source code; the real CK_operational_tasks_source_type rejected it. The fixture was corrected to the verified INTERNAL_REQUEST code, retaining the CHECK. [Trial2](checkpoint66-verification-trial2.json) passed. Subsequent review tightened performer exclusion across Return and incomplete-source rejection; the expanded final run passed. No failed run was relabeled PASS.

C67 added canonical workflowStatus and filtering alongside compatible physical status fields. Full UC105 remains PARTIAL for the remaining legacy source bindings; their fail-closed guards are not counted as implemented requester handling. UC102/104 public writers and UC106 cycle boundaries remain separate lifecycle work.
