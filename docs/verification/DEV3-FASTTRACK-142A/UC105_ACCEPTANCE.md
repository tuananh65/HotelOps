# UC105 — verification acceptance ledger (PARTIAL)

C66, 2026-11-15. **No newly COMPLETE_VERIFIED UC in this checkpoint.** Direct operational-task workflow is implemented and verified; linked-source requester provenance is still gated. Requirements: SRS11.4, GBR001/002/005/007/008/010/012/013/133, decision66.

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
| Linked task sources | **Not completed:** INTERNAL_REQUEST, MAINTENANCE, INSPECTION, CLEANING requester provenance is not established. Incomplete/missing binding returns503; no creator fallback or permission weakening |
| Cleanup | Three exact owned clones and backups removed; source remains empty with original trusted task/source CHECKs; trial tables/markers removed; private files and owned HTTPS listeners zero |

Creation/assignment, Working workday and initial interval are synthetic preconditions. Initial submission cases retain the C65 fixture resets; the C66 Return → Start → Submit → Verify loop uses real APIs and browser actions without resetting workflow between its three decisions. Temporary source/creator/contributor changes are negative fixtures only, not product mutation paths. Reopen remains UC106 work; Return never acts as an implicit new processing cycle.

The first [trial](checkpoint66-verification-trial1.json) stopped because its negative fixture used an unsupported source code; the real CK_operational_tasks_source_type rejected it. The fixture was corrected to the verified INTERNAL_REQUEST code, retaining the CHECK. [Trial2](checkpoint66-verification-trial2.json) passed. Subsequent review tightened performer exclusion across Return and incomplete-source rejection; the expanded final run passed. No failed run was relabeled PASS.

Current list/detail records still expose the existing physical status projection; canonical execution/verification panels show authoritative workflow states. Pending Verification filtering and the wider lifecycle/read contract should be reconciled as FE11 completion proceeds. Full UC105 remains PARTIAL; the linked-source guard is intentionally not counted as implemented source handling.
