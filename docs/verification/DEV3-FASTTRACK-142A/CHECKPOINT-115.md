# FASTTRACK-142A checkpoint115 — own daily time log API

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=97ab1c248fda7371a7c03b72bab00ad04115b626
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
NEW_COMPLETE_VERIFIED=[]
TOTAL_COMPLETE_VERIFIED=24/144
PARTIAL=120/144
SHARED_DB_MUTATION=NO

UC111 gains GET `/api/branches/{branchId}/workdays/own-time-log?date=yyyy-MM-dd&page=1&pageSize=50`. It extends existing own-workday reads under ATTENDANCE_RECORD and the current authenticated session/branch resource resolver. User identity is not model-bound; even broad grants cannot select another user. Existing user/time locks and Serializable transaction protect the read. Response DTOs contain own interval provenance and row versions, never target descriptions or target-read grants. No correction operation is exposed.

The selected hotel date is converted to a half-open UTC period. Daily totals cover every matching interval regardless of page. Closed effort and provisional open effort are separate, as are workday duration and work-item effort. Pauses are absent intervals, not elapsed start-to-finish time. Source timestamps remain available while duration is clipped at date boundaries. Inconsistent ownership, timestamps or overlapping intervals fail closed.

[Trial2](checkpoint115-trial2-runtime.json) passes actual HTTPS and disposable SQL with synthetic workday/task intervals: two intervals separated by pause, summary across pages, current own grant, another employee in the same branch excluded, attempted query-user override ineffective, invalid page400, foreign branch403, revoked attendance grant403. Trial1 passed the initial cases before adding same-branch employee isolation. Both clone/backup cleanup receipts confirm removal. No shared schema, permission or data mutation; fixtures only on verified disposable clones.

Backend solution build PASS0 warnings/errors; solution test exits0 without discovery; relevant OwnTimeLog/TaskWorkload tests6 PASS. Tests include hotel-midnight clipping, pause exclusion, page-independent totals and separate workday/effort. ModelProbe build PASS with three existing EF1002 warnings. Frontend unchanged; no frontend build/test or browser claim in this batch.

UC111 remains PARTIAL: canonical My Time Log UI, correction permission contract, request/apply/reject/audit/notification, and all work-kind runtime acceptance remain. SQL evidence here covers TASK intervals; generic projection tests include CLEANING/REWORK/INCIDENT labels but do not establish those writers or schema capabilities. Next step is canonical own-log UI and browser verification, followed by correction only with proven authorization. FE13 receipt access/Reopen permission/material completion decisions remain pending; sprint is active.
