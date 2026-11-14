# FASTTRACK-142A — checkpoint 11: FE13 history query component

Baseline `5e54cc20237438d5635baba58915f617c8bfd5b7`; canonical `integration/dev1-dev2-dev3-unblock-20261114`. Previous checkpoint was verified progress; sprint ACTIVE.

Implemented `Services/InternalRequests/InternalRequestHistoryQueries.cs`: DTO-only list and timeline projections from a caller-authorized EF query. Filter by type, priority, receiving department, status, literal keyword and overdue; count/pagination with bounded page size and overflow rejection. Deadline order places null last and breaks ties by request ID. GBR156 overdue is strictly after due time and excludes CLOSED/REJECTED/CANCELLED. Timeline orders occurrence time then event ID; nonexistent and out-of-scope requests both return null. No linked-work details are exposed without that resource's separate authorization.

This is a query component, not a public API or complete service workflow. Its precondition is an already authorized branch-scoped query inside the caller's stable authorization/read transaction. It does not establish authorization itself. No controller, DI registration, frontend route, new permission, generated entity or schema was added. Withdrawal, handler display, assignment/history sources beyond events, linked-work visibility, feedback, pagination for long timelines and public authorization composition remain.

Read requirements UC123 and GBR156, existing IR entities/DbContext and release catalog before coding. Contract gap: SRS permits sent/received/handled history for several actors, but catalog has IR_CREATE, IR_PROCESS, IR_OVERDUE_HANDLE and no read grant; SYS_ADMIN lacks these IR grants. Mapping these mutation-oriented permissions to history, especially branch-wide IR_CREATE and historical handlers, needs an explicit business contract. Proposed next contract: requester-only history through existing IR_CREATE, active-handler history through IR_PROCESS OWN, branch processing/overdue scope only via the corresponding branch grants; do not grant administrators access merely because SRS names that actor. This proposal is not implemented as an authorization policy or treated as approved.

`SprintInternalRequestHistoryProbe.cs` uses existing SprintApproveFixture exact disposable server/database/GUID/run guard before inserting. All fixture rows and schema work are inside a SERIALIZABLE transaction and rolled back. Caller-scoped SQL query in the probe tests query composition, not the real permission engine. Fixed UTC snapshot and synthetic own/foreign requests cover twelve assertions: scope exclusion; due/null ordering; strictly overdue boundary with closed exclusion; non-overdue null/equal/closed; literal percent search; scoped total/pagination; event timestamp tie order; foreign/missing timeline equivalence; three invalid pagination cases.

First run failed SQL translation when OrderBy accessed a constructed record's property (`ir-history-first.json`); rollback true. Fixed by anonymous SQL projection with filtering/sorting/paging before final DTO construction, without client evaluation. Final `--sprint-ir-history .../ir-history-verified.json`: **12 assertions PASS; rollback=true**. `dotnet build HotelOps.slnx` PASS0warnings/errors (`checkpoint11-build.log.txt`). `dotnet test HotelOps.slnx` exit0, restore only/no discovered tests (`checkpoint11-solution-test.log.txt`). Frontend and HTTPS unchanged/not rerun. No browser/API/whole-UC acceptance claim.

No shared HotelOpsDB/ImportReview mutation. Target remains meita / HotelOps_DEV3_142A_20261114_cc78c13f / GUID1D5A9B53-7D19-435A-96AB-9FDFF447B6E0 / marker cc78c13ffc124efcad67b3d804baa84c. No API process started. The source file's query component remains intentionally unmounted pending authorization composition; full sprint is not blocked by this one contract gap.

TASK_ID=DEV3-FASTTRACK-142A
CHECKPOINT=11_ACTIVE
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
INITIAL_SHA=8d2f1e0375c8654cdfaffa2ebc8c9c2e99a3292e
SOURCE_IMPLEMENTED=FE13_HISTORY_LIST_TIMELINE_QUERY_COMPONENT
CHANGED_PATHS=src/HotelOps.Api/Services/InternalRequests/InternalRequestHistoryQueries.cs; scripts/verification/CanonicalDatabase/ModelProbe/{Program.cs,SprintInternalRequestHistoryProbe.cs}; docs/verification/DEV3-FASTTRACK-142A/{CHECKPOINT-11.md,UC_STATUS_MATRIX.md,checkpoint11*,ir-history*.json}
TESTS_EXECUTED=12_REAL_SQL_ASSERTIONS_PASS
BUILD_STATUS=BACKEND_PASS_0_WARNINGS;FRONTEND_UNCHANGED
FE05_STATUS=PARTIAL_UNCHANGED
UC006_UC010_STATUS=PARTIAL_UNCHANGED
FE11_STATUS=PARTIAL
FE13_STATUS=HISTORY_QUERY_COMPONENT_SQL_VERIFIED;NO_PUBLIC_AUTHORIZATION_BINDING
FE14_STATUS=PARTIAL_PREVIOUS_CHECKPOINTS
FE15_STATUS=AUDIT_READ_PARTIAL;REPORTS_PENDING
ISOLATED_SQL_STATUS=FULL_TRANSACTION_ROLLBACK
HTTPS_API_STATUS=NOT_RUN_THIS_CHECKPOINT
E2E_VERIFIED_WORKFLOWS=NONE_NEW;SQL_QUERY_COMPONENT_ONLY
UC_MATRIX_TOTAL=144
UC_COMPLETE_VERIFIED=0
UC_SOURCE_READY=0
UC_PARTIAL=144
UC_BLOCKED=0
CRITICAL_BLOCKERS=FE13_HISTORY_AUTHORIZATION_CONTRACT;FE05_PUBLIC_LIFECYCLE;FE11_13_WRITERS;UC006_010_WRITERS;BROWSER_SQL_E2E
NEXT_IMPLEMENTATION=Resolve FE13 read binding against approved permissions; complete authorized history API/UI; advance FE11 and browser-to-SQL acceptance

CURRENT_SHA/COMMIT_SHAS/PUSH_STATUS follow delivery to avoid self-reference.
