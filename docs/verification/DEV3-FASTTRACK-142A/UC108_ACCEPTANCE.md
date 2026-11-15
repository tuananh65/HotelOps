# UC108 — Start Assigned Task: COMPLETE_VERIFIED (isolated)

Checkpoint64, 2026-11-15. Newly completed: **UC108 only**. Source: SRS section11.7, GBR001/002/005/007/008/130/136/137/138 and decision64. Direct receipt: [checkpoint64-entry-restart-verified.json](checkpoint64-entry-restart-verified.json), success=true, cleanup=true, **48 records including one browser result with 10 stages**. These are not 47 independent automated tests.

| Acceptance criterion | Direct evidence |
| --- | --- |
| Authenticated, authorized executor | Anonymous Accept/Start with valid anonymous CSRF returns401; missing CSRF400; revoked TASK_EXECUTE403; unassigned task and foreign branch403 |
| All three SRS actor roles | HK_STAFF full path; MAINTENANCE_STAFF and STOREKEEPER each Accept/Start using actual scoped catalog grants; no role IDs or permissions invented |
| Current accepted responsibility | Assigned cannot skip Accept409; lost assignment403; assignment changes make the old ETag stale412; inactive department blocks entry |
| Working day and no concurrent work | Missing workday409/MSG30; another open interval409/MSG31; both leave workflow/parent/history/interval/audit unchanged |
| Transition and first actual start | Accepted→In Progress; exactly one interval; actual_start_at equals interval opened_at/effective_started_at; progress preserved |
| Accepted task with prior actual start | Additional real SQL/HTTPS case starts an Accepted task with prior first start and preserves it; the UC105 Return operation itself remains unimplemented |
| Atomic persistence and audit | Injected final task audit failure rolls back state/version/first start/history/interval and both audits; two committed entry audits and one interval audit before subsequent role trials |
| Concurrency and idempotent rejection | Concurrent Accept and Start each yield [200,412]; fresh repeat on running task409; no duplicate open intervals |
| Capability compatibility | Entry rejects missing capability503; legacy physical CHECK unchanged; original progress constraint restored after trial |
| Canonical browser success and reload | Login→assigned read→Accept→reload Accepted→Start→progress40%→Pause Other with note→Resume without new reason→reload history→logout; no browser mocks or TLS bypass |
| Time persistence after handoff | SQL proves two intervals, one closed and one open, progress40%, original actual_start_at retained through Pause/Resume |

Task creation/assignment and Working workday are synthetic preconditions. The probe resets its owned task between independent role cases and the browser path; those resets are not production transitions. This does not establish UC107 Decline, task creation, mandatory-criteria initialization, UC112 submission or UC105 verification. Existing permission catalog may authorize other assigned executor roles; no new SRS actor restriction was inferred from UC135.

The first expanded trial failed because its anonymous POST omitted CSRF and correctly received400 before authorization. The corrected client obtains its own anonymous antiforgery token to isolate401. The next expanded trial reached the browser but a success-text locator matched prior history entries; it now targets the live status notification. Retained failed receipts: [authentication harness](checkpoint64-entry-final.json), [browser locator harness](checkpoint64-entry-delivery.json). No product guard or assertion was weakened. The final complete run passed.

Independent cleanup: [task/time rows and original constraint](checkpoint64-task-cleanup-delivery.txt), [full fixture cleanup](checkpoint64-cleanup-delivery.txt), [schema rollback](checkpoint64-schema-final-rollback.txt). Shared DB untouched; no deployment certification.
