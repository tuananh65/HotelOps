# UC139 verification alignment after scheduled activation

No new UC completion; UC139 remains PARTIAL. This increment fixes stale verification and preserves dedicated trial isolation after canonical controller activation.

- WorkdayReadRouting now discovers exactly three reads (including existing own-time-log) and two scheduled writes. Cookie authentication, no anonymous metadata, route uniqueness, DTO/status/ETag checks remain.
- ScheduledWorkday checks admitted canonical writes and the final-session-before-commit boundary; existing identity, source, calendar and consumed-assignment checks remain.
- The bounded trial explicitly removes OwnTimeLog added to the shared read controller. It keeps eight approved actions and does not admit canonical WorkdaysController; no trial authorization or TLS/lease checks were weakened.

Actual offline results: WorkdayReadRouting 52 PASS; ScheduledWorkday 62 PASS; ScheduledWorkdayTrialHost --self-test 44 PASS. Counts include source assertions and offline behavior, not SQL acceptance. First --no-restore lacked project.assets.json; restore needed escalated local NuGet configuration access. Concurrent builds initially collided on the API assembly; sequential rerun passed. No unresolved build/test failure in these harnesses.

Read-only live SQL evidence: exact recovery database HotelOps_DEV3_142A_Recovery_2b221f40 on the approved local instance reports GUID 5270D07C-7C27-477B-A60E-86485ADD7FAD and both workdays/work_intervals tables. The old local receipt contained an empty GUID; a separate local receipt-workday-identity.json records the observed identity without changing the historical receipt. Backup path exists; no backup restore or new backup validation was performed. Table presence alone does not prove schema compatibility.

Runtime limitation: the existing starter's ValidateOnly invocation was rejected by PowerShell Execution Policy before execution. Policy was not changed. No host was started; no HTTPS or SQL mutation/rollback/cleanup acceptance was executed. No shared database mutation. Remaining: exact schema checks, safe synthetic fixture/rollback setup and live scheduled lifecycle acceptance, plus unscheduled Start and Pause All implementation.
