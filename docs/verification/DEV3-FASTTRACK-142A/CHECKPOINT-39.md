# FASTTRACK-142A checkpoint39 — Request Rework acceptance and canonical source navigation

Sprint remains active. Original deadline: 2026-11-16 17:50 UTC. No clock reset.

Newly COMPLETE_VERIFIED: **UC046**, with new browser/HTTPS/SQL evidence. **UC109** is also reclassified COMPLETE_VERIFIED after mapping its existing checkpoint27 evidence to its own acceptance criteria; its runtime was not rerun in checkpoint39. Total: **4 COMPLETE_VERIFIED (UC044, UC046, UC109, UC110), 140 PARTIAL**. This is isolated acceptance, not production readiness or completion of the whole rework/task lifecycle.

## UC046 acceptance

Sources: UC DETAIL UC046 saved under DEV3-FASTTRACK-141A/uc-v3-source.json; SRS section 5.8; GBR-014/046/064; approved no-active-shift eligibility decision.

| Requirement | Evidence |
| --- | --- |
| Failed inspection, selected failed defects, original cleaner first, eligible assignee, due time | checkpoint39-inbox-verified.json: canonical form, normal and effective-blocked rooms, real HTTPS and SQL persistence |
| No active PLANNED shift required; active role/department/branch/account still required | Same runtime: noShiftAssignee and ineligibleAssigneesDenied; actual principal/catalog, no hard-coded role IDs |
| Pending Acceptance linked to inspection and selected defects | Persisted row, links, assignee and audit assertions after browser Save; reload shows existing open rework |
| Atomic creation/audit/authority | Injected audit INSERT failure rolls back task, defect links, proof, operation receipt and audit; no notification publication. Subsequent normal creation succeeds |
| One open rework, safe replay/concurrency | Two concurrent real service calls with verified session yield one Success and one Duplicate; one proof/audit. HTTP duplicate returns409; stale412, missing ETag428 and missing CSRF400 |
| In-app assignee notification after commit | Durable intent, delivery fault rollback, revoked branch/account/role denial, concurrent/retry dedup; actual assignee login sees one notification in canonical inbox and after reload |
| Success and current-source navigation | UI reports Pending Acceptance and queued delivery honestly. Inbox Open Rework resolves through current HTTPS authorization to canonical route |

UC046 POST-01 explicitly hands off to UC035. Accept/Start/Complete and UC047 are not requirements for creating a Pending Acceptance task; they remain PARTIAL. Source detail on the current privileged HTTPS host correctly returns503, while the actual public read service succeeds with real session under a disposable SQL user denied proof DML. This limitation is recorded, not counted as successful browser source-detail acceptance.

## UC109 acceptance reassessment (historical runtime)

SRS section11.8 requires progress on an already assigned In Progress/Paused task, criterion results, notes, private evidence, same-transaction audit, version checks, and no automatic completion at100%. It does not require Create/Accept/Start/Complete to be finished first.

checkpoint27-runtime.json directly records canonical browser login/list/detail, paused progress save, PASS/FAIL notes surviving reload, uploaded evidence download bytes, wrong-branch/non-owner/revoked-access denials, missing-CSRF/ETag and invalid input, stale412, concurrent200/412 with one audit, invalid second attachment rollback of SQL/file, percent100 with status IN_PROGRESS and no completed_at. checkpoint27-cleanup.json and clone cleanup receipt prove removal of the immutable-criteria trial. WorkProgressService remains at efc51fb. Current checkpoint39-progress-regression.txt reruns only the focused client contracts; the older SQL/browser execution is not relabeled as new.

## Implemented and tested now

Added assigned rework read/evidence through existing committed-authority validation, existing CLEANING_VIEW/ownership/eligibility and full session transaction boundary. Initial read does not invent remediation storage or remove executor workday/shift guards. Added notification source resolver, canonical route and Open Rework action; neither stored ActionUrl nor a notification grants source permission. Session loss/401 removes details; unsupported capability/state cannot be mislabeled Pending Acceptance.

Real browser testing exposed missing canonical notification/rework destinations in the login return registry. Fixed only validated local destinations, preserving revalidation, positive safe IDs, branch query validation and forbidden redirect/token parameters.

Validation: backend build PASS0warnings; backend regression507PASS/11SKIP (checkpoint39-regression.txt); solution test command succeeds but solution has no test project. Focused client/auth55PASS; canonical read3PASS; final shared-router+read118PASS; typecheck and production buildPASS. Existing Vite use-client/bundle-size and EF unmapped-index/Take warnings remain. Npm ci not repeated: lockfile/dependencies unchanged.

Retained failures: initial API could not access SQL and did not start; retry with proper process access passed. Initial new browser-test bundle lacked output path for CSS and was corrected. A verifier rebuild hit the running API executable lock; stopping the owned host resolved it. Inbox attempts failed at login return navigation before the source fix; both failed JSON receipts retained. Final checkpoint39-inbox-verified.json passes normal/blocked variants, inbox persistence, source-route authorization, fail-closed privileged detail, creation rollback/concurrency, authority and delivery tests.

checkpoint39-cleanup.txt independently verifies exact disposable server/database/GUID, zero synthetic users/reworks/audits/notifications/recipients/operations and absent proof/procedure/reader/fault trigger. HTTPS host stopped after trial. No shared database mutation, generated entities, migrations, server configuration changes or security bypass. Read-only infrastructure check found contained database authentication disabled; no server setting was changed.

## Delivery and continuation

Initial remote51a53c81d0a6b1243b85d0f96b3098251d5aa2fb. Logical source commits285576d, f1ab761, ca25a93, 6f84b07. Final evidence commit/push identity is recorded by Git, not guessed in this file.

Changed application paths: Controllers/CleaningExecutionController.cs, Controllers/NotificationsController.cs, Services/Housekeeping/AssignedReworkReadService.cs, Services/Notifications/NotificationCenterService.cs; frontend api/dev1/reworkExecution.ts, features/inspectionExecution/AssignedReworkRead.tsx, notifications/{NotificationCenter.tsx,inboxApi.ts}, screens/housekeeping/ReworkExecutionPanel.tsx, shared-host/{SharedAuthHost.tsx,returnUrl.ts}. Focused client/browser tests and ModelProbe acceptance helpers accompany these changes.

Complete verified slice: Failed inspection → Request Rework → atomic Pending Acceptance → durable authorized delivery → assignee inbox/reload. Full Reject → executor remediation → Reinspection is not complete.

Next highest-impact work: compose canonical UC035 execution and proper least-privilege runtime without removing proof protection; finish UC047 binding/remediation/results and effective-readiness transaction; advance FE11 assigned-task lifecycle with existing state/version/workday contracts. FE13, UC006/010 and other modules remain within the original sprint scope.
