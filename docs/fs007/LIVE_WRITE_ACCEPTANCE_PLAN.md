# Live write acceptance plan — PLAN ONLY / BLOCKED_APPROVAL

FS007 authorized metadata/read-only only. Instance `.\SQLEXPRESS`, retained database `HotelOps_Canonical_20261105_180000_f040001_Fresh`, owner DEV1_FS004. No reset/drop/reseed; no auth API startup/login or business writes in FS007. SQL_METADATA.json is sys schema evidence only, not query/workflow/notification acceptance. TLS SQL exception remains scoped to this local trial; no browser certificate bypass.

## Concrete future approval package required

After exact contracts/diffs exist, name reviewed script+SHA256/base HEAD; exact synthetic existing/new room/task/user/branch IDs, actor grants/valid sessions, allowed INSERT/UPDATE tables/rows, operation GUIDs/markers, fault mechanism, expected before/after, max test budget/window and cleanup owner. Do not use production data or override shared User Secrets. IDs must be discovered read-only and recorded before approval; FS007 deliberately does not invent future IDs or treat task3's old progress approval as Create/Assign scope.

DDL approval separate: exact sidecar package and DEV2 B01/B02 installation/verify/recovery, version markers, owner guard, legacy compatibility and empty-owned rollback rules. Retained trial already populated: no 'rollback' dropping evidence. Fault injection (audit/intent failures, session/grant revoke, lock delays) changes data/schema and must be explicitly enumerated. No grant edits, creates/deactivation of accounts, moving rooms or editing a trial shift to make eligibility pass without approval. Expired retained shift is not a license to reseed; request narrowly bounded new synthetic roster fixture if required.

## Acceptance cases (all future writes NOT_RUN)

| ID | Operation / trigger | Expected evidence | Approved write set required |
| --- | --- | --- | --- |
| W01 | Valid Create active room/no open task |201 real DTO/tag; one new task+pointer/readiness event+audit; unchanged legacy occupancy/status/blocks; no mandatory Notification for Create alone|Exact new task, sidecars/audit only|
| W02 | Existing open task incl pending inspection/rework / unknown legacy |409/fail closed; authorized existing ID only when view allowed; zero new records|No expected business writes|
| W03 | Concurrent distinct Create attempts same Room |One success, other conflict; no orphan pointer/duplicate events/audits; no deadlock hidden by retry|Same W01 scoped room + two approved requests|
| W04 | Unauthorized/foreign branch/inactive hierarchy/reference |401/403/404 per policy or400 invalid refs; no existence leak; correct CSRF exercised before unsafe action|Only allowed session/auth activity|
| W05 | Valid Assign PENDING_ASSIGNMENT→ASSIGNED |Existing route200, ACTIVE assignment/task revision/audit and durable completed reservation/publication/one exact intent, then readable assignee inbox|Exact task/target/operation, notification tables/inbox|
| W06 | Missing CSRF/tag, malformed tag, stale tag, assigned/wrong state, ineligible/expired shift |400/428/412/409/ref error as actual contract; no source/publication partials|No expected business writes; fixture eligibility changes only if separately approved|
| W07 | Duplicate operation same key+same command; same key+different canonical command |Original authorized result with no second assignment/publication; fingerprint conflict409; new key can't bypass state|Exact stable GUID requests and operation store|
| W08 | Revoke permission/session or assignee access during lock wait |Fresh check denies, all source/audit/mandatory-intent rolled back; replay after revoke denied|Explicit scoped revoke/restore changes approved first|
| W09 | Fault after task/assignment, after audit, before/after mandatory stage, before commit |All mandatory business+audit+operation-completion/intent facts consistent; rollback leaves no committed partials|Exact fault mechanism/scripts/markers; don't add trial triggers blindly|
| W10 | Guard wrong context/transaction, sync/raw commit bypass, savepoint/rollback, missing stage |Fail closed or proven supported behavior; no commit without required stage/result|Approved C01 isolated harness/schema data only|
| W11 | Response lost/ambiguous commit, crash/restart worker, two claims |Durable resolve/replay, one business transition/intent/inbox; no client blind POST retry|Approved process fault and exact operation data|
| W12 | Delivery failure after commit |Assignment preserved; retry persisted next-due/backoff/attempt; eventual actual inbox and source-readable content, no duplicate event|Approved delivery fault/restart + inbox/queue writes|
| W13 | Recipient inactive/out-of-scope/source deleted/superseded vs transient dependency outage |Approved terminal reason only for definitive facts; outages retry; explicit-recipient source read independent of producer|Explicit recipient/source fixtures or doubles classified separately|
| W14 | Blocked/occupied room, ExpectedReleaseAt past, old pass, missing current pointer |No occupancy inference or automatic block release, never false Ready; history explains current-cycle result|Only approved source fixtures/sidecars; block writes need separate scope|
| W15 | Real React create/assign success and errors |Fresh CSRF/exact correct task ETag/stable key, loading/draft/errors, real list/detail refresh and authorized recipient navigation, no mock fallback/no unsafe retry|Same W01/W05 business scope, real session|
| W16 | Existing UC032 |Current GET SQL parity; approved PUT only if separately included, same ownership/CSRF/progress version/audit,100 not completion|Read-only allowed; PUT requires explicit updated scope|

Record UI→HTTPS→API→same trial SQL observations, before/after task/assignment/cycle/events/audit/reservation/publication/intent/actual inbox identity and source access. Logs must omit credentials/cookies/CSRF headers/private evidence bodies. Browser intercept tests separately VERIFIED_WITH_MOCK; business state/notification delivery must use real SQL and valid session, no AllowAnonymous/fake principal. A test with retry after1205 is not successful race acceptance; report first failure and fix reviewed order.

Acceptance cleanup: stop only owned processes by PID/start-time; retain trial/approved evidence rows unless exact removal approved; no reset/reseed; no blanket rollback schema on populated target. Re-running tests must use approved retained/replay plan, not create unlimited new data. Close entry gates only with actual receipts, no UC credit from plan or enqueue alone.
