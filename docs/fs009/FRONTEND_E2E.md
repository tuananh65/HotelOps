# Frontend and E2E

UC028 renders real server cleaning-item labels, existing-task navigation, loading/error states, type/priority/due/instruction form and explicit submit. Success opens live Detail/Monitor; alternate Save-and-Assign navigates existing assignment route after exactly one Create. Snapshot remains first-Start, not initialized by Create. Forbidden/session/unavailable options never enable business post. Conflict blocks blind retry.

UC029 reuses FS006 planning/candidates/workload/shift UI, shared DEV1 cookie send transport and strong task ETag from GET response. Capability absent/unavailable keeps Save disabled. Only PROVISIONAL_NOT_ACCEPTED capability + valid ETag + PENDING_ASSIGNMENT enables candidate Save. Captured body/GUID/original tag are immutable for explicit same-command retries; every attempt bootstraps fresh CSRF. Committed assignment shown with pending delivery, then planning refreshed. No DELIVERED label from staging. Invalid result identities/tags/contracts rejected.

UNIT Node46/46 after candidate result-contract tests. INTERCEPTION browser47/47 includes existing Monitor/Detail/Room board/UC032, candidate Save/result/explicit retry/fresh CSRF and Create401/403/503. These are not counted as SQL acceptance. Real browser/API suite has no interception, ignoreHTTPSErrors=false, uses existing trusted store-key gateway/public certificate and actual cookie/SQL authentication. API-assisted fixture login is not a full sign-in UX certification.

Real UI cases: Create to Detail to Monitor; OWN assignment header without monitor widening; candidate Assign to committed/pending display; blocked-room preview/Save-and-Assign/existing-task link. Real API cases cover exact statuses, concurrency, rollback/replay and source delivery security. Final LIVE_RESULTS.json is authoritative for actual outcomes. No screenshot/trace/video or plaintext credentials committed.



Final validation: backend build PASS (0 warnings/0 errors), actual xUnit119/119, Node46/46, interception47/47. Enabled candidate12 unique cases have latest PASS, composed of full10/11 + terminal rerun1/1 + final React happy-path1/1 + assignment-authority1/1. This is not one uninterrupted12/12 run. Separate default-off real browser/API/SQL1/1. Five distinct real UI cases and eight live API/SQL cases; nested SQL checks are not counted as additional live tests. See LIVE_ACCEPTANCE_SUMMARY.json and VALIDATION.json. Shared acceptance/ledger credit unchanged.

Final UI race hardening: changing branch/task while an assignment POST is pending cannot display the old operation result/error on the new task. Current scope ref plus reset guards protect asynchronous state; dedicated interception race PASS and final real React assignment PASS.
