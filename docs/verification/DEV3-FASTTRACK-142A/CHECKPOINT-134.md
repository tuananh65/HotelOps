# FASTTRACK-142A checkpoint134 - escalation priority/deadline E2E

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=e4fb9978317fb7e43668be9757ac0ad8dea81d75
NEW_COMPLETE_VERIFIED=[]
SHARED_DB_MUTATION=NO

Completed the priority/deadline escalation path through production UI, HTTPS API, disposable SQL, hosted notification worker and explicit acknowledgment. GET/POST /api/internal-requests/{id}/escalation require HTTPS and the existing authenticated ticket. Read capability is limited to current Operations Manager plus branch IR_OVERDUE_HANDLE and independent IR_VIEW, evaluated under serializable session/request/authority checks. Read returns current timing, creation/deadline, active priority catalog, ETag and Open state. Mutation requires acknowledgment storage as well as the action/outbox capability; existing CSRF middleware and strong ETag checks remain mandatory.

The request history screen shows the escalation form only from server capability. It accepts a new priority and/or local deadline plus GBR-012 reason, converts local date to UTC, blocks duplicate dispatch, discards data after session generation changes and requires a fresh read after stale/uncertain outcomes. On success it shows MSG08 and reloads persisted history/Open state. Notification Center now offers the authorized Open request action for IR_ESCALATED.

Direct evidence:

- checkpoint134-escalation-ui-runtime.json success=true: 11 named checks including real browser validation, priority/deadline input, one POST on double click, MSG08, persisted reason/Open state, hosted-worker alert, explicit acknowledgment, source opening and logout. SQL verifies the selected priority/deadline, unchanged CreatedAt/lifecycle, one event, recipient ReadAt/AcknowledgedAt and stale replay rejection.
- HTTPS negative checks: DH with permission denied capability, missing CSRF 400, missing ETag 428, stale ETag 412, cross-branch 403, revoked OM role 403. Fresh capability excludes a second Open escalation.
- checkpoint134-escalation-ui-cleanup.json confirms clone/backup removed. Earlier two trials stopped at priority selection; the select needed an explicit accessible name. Added aria-label and reran the real workflow successfully. Both failure and cleanup receipts retained; no relaxation of validation/authorization.
- Backend solution build PASS, 0 warnings/errors. Solution test exits zero without discovery; explicit regression project 760 PASS, 13 environment-gated SKIP. Probe builds with 3 existing credential-fixture EF1002 warnings.
- Existing history transport/browser suite 36 PASS before the new escalation case; added case then independently PASS after fixing accessibility: reason validation, CSRF/ETag, stale double click sends once and removes form. No claim of a single 37-test final-revision run.
- TypeScript and production build PASS. Existing React Router use-client and >500kB bundle warnings remain. Full frontend session37649 is still live and began before this product change; its eventual result alone cannot certify this revision.

Changed product files: InternalRequestEscalationController.cs, InternalRequestHistoryController.cs (error mapping), InternalRequestEscalationService.cs, Program.cs, EscalationPanel.tsx, HistoryPage.tsx, NotificationCenter.tsx. Validation: SprintInternalRequestEscalationUiScenario.cs, SprintDepartmentAuthorityScenario.cs, Invoke-Dev3LinkedTaskCreationTrial.ps1, sprintInternalRequestEscalationBrowser.mjs, internal-request-history-browser.test.mjs.

New verified business path: Operations Manager priority/deadline intervention -> one Open escalation + audit -> worker alert -> recipient acknowledgment -> authorized source. Whole UC122 stays PARTIAL: reroute/handler interventions, at-risk list filtering and isolated manager-only/handler-only authority cases remain. No whole UC promotion. Next: close the remaining UC122 intervention/lifecycle paths using SRS and actual assignment/handover invariants; preserve this verified demo path.
