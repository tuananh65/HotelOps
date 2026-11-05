# Frontend → API mapping

| Screen | Before | FS006 behavior / API | Evidence class |
| --- | --- | --- | --- |
| 25 CreateCleaningTaskScreen | EntityForm / service.save persisted mock and navigated as successful | Existing header/panel retained, explains unavailable real create, disabled Create, return to Monitor; no guessed POST/DTO | IMPLEMENTED; VERIFIED_WITH_MOCK no-mutation check; real browser blocked-control check in live suite |
| 27 CleaningAssignmentScreen | mock task selection, service.assign, role-filtered mock staff/workload | GET existing assignment-options via shared getJson; real task/candidates/raw shifts/workload, search/page/cancel/retry,401/403/404/409/503; syntactic draft only, Save disabled | IMPLEMENTED; VERIFIED_WITH_MOCK and real GET suite when pass |
| 26 CleaningMonitorDetail | live monitor overview/timeline and UC032 link | Added link to planning with task+branch, no mock identity/scope | Existing live path preserved; real detail→planning navigation |
| 24 Monitor / 23 Readiness | FS005 live reads | No code changes | Existing interception + read regression |
| 26 CleaningProgressPanel UC032 | Existing GET/PUT+fresh CSRF/ETag | No source change, no additional write invoked | Node/interception regression + real GET only |

Planning URL: /cleaning/assignment?branchId=<id>&taskId=<id> (existing record query alias accepted without mock fallback). IDs entered or from a server-authorized monitor item are untrusted until backend gates. No default synthetic branch/user ID. Cannot grant CLEANING_ASSIGN via UI role or by viewing CLEANING_VIEW detail.

Draft request fields mirror existing AssignCleaningRequest only: assigneeId, dueAt UTC ISO, reason, adoptUnassignedLegacy:false. Pure buildAssignmentDraft makes zero requests, performs no writes and provides no success notification. It accepts only a candidate on the currently loaded page and a verified PENDING_ASSIGNMENT snapshot; server must reauthorize/revalidate if write integration is later approved. Raw recorded shift states are not labeled eligible-now, and no mock workload appears.

All requests reuse FS002 getJson: cookies include, no-store, redirect error, AbortSignal; no competing transport. Current branch/task/page/search key prevents stale result visibility; cancellation stops prior response from populating the screen. Scope/error never falls back to mock. Save/create buttons cannot submit. No unsafe mutation adapter, CSRF cache, auto retry or invented If-Match is added. Existing GET ETag remains available on server; this read adapter does not expose/consume it for a future POST.

When mandatory contracts are approved, integrate fresh CSRF/exact strong ETag, POST response DTO/notification truth, conflict handling, explicit reload and list/detail refresh. These behaviors are NOT_IMPLEMENTED here, not VERIFIED_WITH_MOCK as if they were a working write path.
