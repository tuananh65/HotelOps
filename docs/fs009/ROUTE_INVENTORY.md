# FS009 route delta

- Existing POST /api/branches/{branchId:long}/cleaning-tasks/{id:long}/assign: same HTTP route/controller/DTO/strong task ETag; additive stable Idempotency-Key in opted-in candidate. Default-off503 verified on real SQL with zero mutation. Candidate actual cookie/session/permission/source gates and mandatory guard precede one commit.
- Existing GET assignment-options: same paging/filter/shift/workload/ETag, additive assignmentCapability only; missing/unavailable capability disables Save.
- Existing GET cleaning-task-options: additive actual cleaningItems/existingTaskId. POST Create unchanged shape and same authority.
- New GET /api/branches/{branchId:long}/cleaning-assignment-notifications: cookie/session, exact current user, real CLEANING_VIEW scope and per-assignment source reevaluation, no-store DTO. No userId override. Candidate-only.
- Source header/Monitor/Detail/progress/history routes retained. No recovery-controller markers or unrelated inspection/rework actions enabled.

Route shapes inspected against active controllers; no method/path ownership collision found. Actual new/modified paths executed in live evidence. Scoped candidate/store/delivery use existing scoped DbContext; generated context/entities and authentication/cookie/session pipeline unchanged. This is development route ownership evidence, not shared acceptance of a new inbox API.
