# P0 work authorization handoff

Implemented in WorkService.cs, WorkService.Authorization.cs, WorkService.Maintenance.cs, WorkService.InternalRequestConfig.cs, InternalRequestVisibility.cs and test-p0-work-authorization.mjs.

- All generic write entry points reject Owner; OM cannot mutate Incident or ordinary Internal Request, including handover paths which originally loaded with read access. Guards remain effective if stale grants survive migration.
- Request authority no longer derives from OM/Department Head. It requires current receiving-department authority plus IR_PROCESS. Capabilities reflect readonly accounts. Removed legacy DEPT_MANAGER/ADMIN visibility bypass.
- OM may independently verify OPERATIONAL work only, with the existing event/audit pipeline. Other independent verifier rules remain.
- Maintenance writes require MAINTENANCE_SUPERVISOR; newly created maintenance is assigned to that supervisor. Assignment candidates are self only, assigning HK or other users and handover are denied. External repair remains outside the system.
- Request type configuration moved from DEPT_MANAGER+IR_PROCESS to branch OM+IR_VIEW role permission path; this permits metadata configuration without granting ordinary request processing.
- WorkService constructor now injects RoomReadinessService as readiness; pass calls Reevaluate and no longer directly overwrites room status.

Validation: node --check passed for exported isolated-harness API assertions; scoped git diff --check passed. API assertions have NOT run. C# build/test intentionally left to parent integration; no SQL applied, no Production touched.

Integration concerns:
1. Register RoomReadinessService and update any explicit WorkService constructors.
2. SQL review trigger must allow OM self-verification exclusively for OPERATIONAL; runtime change alone cannot bypass the existing independence trigger.
3. Parent RoomIssue workflow must accept Maintenance COMPLETED for report-remedy, then HK confirmation closes its child; generic Maintenance verify still requires independent reviewer. Do not require the same supervisor to self-verify.
4. IsReadOnlyWorkActor intentionally fails closed for mixed Owner accounts and mixed OM+incident/IR accounts; candidate filtering matches this policy.
5. The node test exports verifyP0WorkAuthorization for the parent's clone-only harness; requires actual role tokens and valid fixture Incident/InternalRequest/Maintenance IDs. It creates one OPERATIONAL task and preserves audit/history.
6. Pending business decisions remain owned by parent; no auto-end/supply changes in this module.
