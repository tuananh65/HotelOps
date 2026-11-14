# FASTTRACK-142A — checkpoint 12: notification center and read markers

Baseline `1b12113d96759b6066413ebfd5e93d1a953a0ed1`; canonical `integration/dev1-dev2-dev3-unblock-20261114`. Previous checkpoint was progress; sprint remains ACTIVE.

## Implemented

Reviewed SRS UC129/130, GBR157/159/160 and SYS-PAR14=365 days, current Notification/NotificationRecipient/DbContext and existing provisional delivery. Added recipient-only GET `/api/notifications`, POST `/api/notifications/{id}/read`, POST `/api/notifications/read-all`. Authenticated unrestricted session required; no invented role permission. Service rechecks persisted session/user/expiry inside SERIALIZABLE transaction; absolute lifetime uses existing SqlSessionPolicy. Existing global API CSRF middleware protects writes.

Inbox contains only caller IN_APP DELIVERED recipients, created no later than now, within retention and not expired. Type/priority/text/read/date filters, scoped count, overall unread badge, stable newest-first pagination. DTOs exclude recipient/user IDs, failure reason and raw source/action URLs; source opening is not authorized by receiving a notification. Foreign/missing mark-read both404. ReadAt set only when null; repeated read leaves original timestamp. Read-all affects eligible own inbox only, no source records or delivery state changes.

Retention configured as `NotificationCenter:RetentionDays`, SRS default365, startup validation1..3650; no guessed system_settings key and no database cleanup job. No generated EF/schema/migration changes. Schema lacks acknowledgment timestamp; DismissedAt is not repurposed as acknowledgment. Critical acknowledgment/audit remains unimplemented.

Canonical `/notifications`, links from dashboard/operations, filters, pagination, unread count and individual/all read buttons. UI escapes plain notification text; no source links. CSRF bootstrap is request-local, never persisted; before POST checks captured authority, aborts old bootstrap on replacement. No automatic mutation replay. Unconfirmed writes display refresh guidance; successful write triggers a fresh list. Data disappears when authority changes. Separate module files hold DTO transport and UI.

## Direct verification

- Backend solution build PASS0warnings/errors: `checkpoint12-build-final.log.txt`.
- `dotnet test HotelOps.slnx`: exit0/no discovery; not counted as suite acceptance (`checkpoint12-solution-test.log.txt`).
- Focused existing test filter `FullyQualifiedName~Permission|FullyQualifiedName~Session`: 6PASS0fail/skip (`checkpoint12-security-tests.log.txt`). This is a small matching subset, not the full security suite. Initial shell quoting attempt did not execute tests; corrected PowerShell quoting produced this result.
- Frontend typecheck/build PASS (`checkpoint12-typecheck.log.txt`, `checkpoint12-frontend-build.log.txt`); existing use-client and bundle>500kB warnings remain. npm ci not rerun; dependencies unchanged.
- Auth/router browser regression147PASS0fail/cancel/skip (`checkpoint12-router.log.txt`). New route denies reads in four unauthenticated/restricted/unavailable states; notification mutation verifies CSRF+refresh, authority replacement during bootstrap prevents old POST, uncertain mutation is not replayed and401expires authority. Fetch is mocked; not browser-to-SQL acceptance.
- Real trusted localhost HTTPS/SQL26checks PASS, cleanup=true (`notifications-verified.json`). Includes anonymous401, own-only visible count, expired/366-day-old/pending exclusion, hidden raw ActionUrl, missing CSRF400, foreign/missing404, idempotent ReadAt, unread/search filters, invalid period/page size400, read-all isolation, unchanged notification source content and auth/logout persistence.

First runtime fixture lacked required User.Email and failed before endpoint assertions; cleanup=true (`notifications-first.json`). Added synthetic example.invalid contact and ephemeral synthetic credential, rerun25PASS (`notifications-second.json`). Final build adds in-transaction absolute-session checks; final run26PASS includes anonymous notification route check. No schema relaxation or changed permission grant.

Safety: outer auth harness verifies exact server meita / database HotelOps_DEV3_142A_20261114_cc78c13f / GUID1D5A9B53-7D19-435A-96AB-9FDFF447B6E0 / marker cc78c13ffc124efcad67b3d804baa84c before mutations. Random-prefix synthetic recipients/notifications; compensating cleanup transaction deletes only owned rows, then outer cleanup verifies empty users/sessions/audits. API process stopped. No shared HotelOpsDB/ImportReview mutation. HTTPS default certificate validation retained; no secret values in evidence. Runtime still reports existing RoomTypeBranchV1 mapping warnings and Skip/Take without ordering warning.

## Remaining acceptance

UC129/130 remain PARTIAL: source-resource permission recheck/navigation, acknowledgment storage+critical audit, production dispatcher/producer completeness, retention cleanup job, concurrent read-marker stress and real browser-to-SQL E2E are missing. This checkpoint does not grant FE13 history permissions or establish notification publication graphs for FE05 conclusions. No whole-UC COMPLETE claim.

TASK_ID=DEV3-FASTTRACK-142A
CHECKPOINT=12_ACTIVE
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
INITIAL_SHA=8d2f1e0375c8654cdfaffa2ebc8c9c2e99a3292e
SOURCE_IMPLEMENTED=RECIPIENT_NOTIFICATION_API_CANONICAL_UI_AND_IDEMPOTENT_READ_MARKERS
CHANGED_PATHS=src/HotelOps.Api/{Program.cs,Controllers/NotificationsController.cs,DTOs/Notifications/NotificationCenterContracts.cs,Services/Notifications/NotificationCenterService.cs}; frontend/src/{auth/AuthRouteHost.tsx,shared-host/SharedAuthHost.tsx,insights/DashboardPage.tsx,notifications/NotificationCenter.tsx,notifications/inboxApi.ts}; frontend/tests/shared-auth-host.test.mjs; scripts/verification/CanonicalDatabase/ModelProbe/{Program.cs,SprintAuthProbe.cs,SprintNotificationScenario.cs}; docs/verification/DEV3-FASTTRACK-142A/{CHECKPOINT-12.md,UC_STATUS_MATRIX.md,checkpoint12*,notifications*.json}
TESTS_EXECUTED=147_AUTH_ROUTER_PASS;6_SECURITY_FILTER_PASS;26_HTTPS_SQL_CHECKS_PASS
BUILD_STATUS=BACKEND_TYPECHECK_FRONTEND_PASS
FE05_STATUS=PARTIAL_UNCHANGED
UC006_UC010_STATUS=PARTIAL_UNCHANGED
FE11_STATUS=PARTIAL
FE13_STATUS=HISTORY_QUERY_COMPONENT_ONLY;READ_BINDING_UNRESOLVED
FE14_STATUS=INBOX_AND_READ_MARKERS_FULLSTACK_IMPLEMENTED;SOURCE_OPEN_ACKNOWLEDGMENT_PRODUCERS_PENDING
FE15_STATUS=AUDIT_READ_PARTIAL;REPORTS_PENDING
ISOLATED_SQL_STATUS=SYNTHETIC_HTTP_FIXTURES_CLEANED
HTTPS_API_STATUS=26_CHECKS_PASS
E2E_VERIFIED_WORKFLOWS=HTTPS_SQL_INBOX_READ_MARKERS;MOCK_BROWSER_SEPARATE
UC_MATRIX_TOTAL=144
UC_COMPLETE_VERIFIED=0
UC_SOURCE_READY=0
UC_PARTIAL=144
UC_BLOCKED=0
CRITICAL_BLOCKERS=FE05_PUBLIC_LIFECYCLE_COMPOSITION;FE13_READ_BINDING;FE11_13_WRITERS;ACKNOWLEDGMENT_STORAGE;BROWSER_SQL_E2E
NEXT_IMPLEMENTATION=Real browser-to-SQL notification/dashboard acceptance; FE13 read authorization contract; FE11 workflows and FE05 lifecycle composition

CURRENT_SHA/COMMIT_SHAS/PUSH_STATUS supplied after delivery, avoiding self-reference.
