# UC037 actor and current-session display correction

Source progress; PARTIAL_IMPLEMENTATION. No completion count increase.

SRS UC037 names Housekeeping Supervisor. CleaningMonitorService previously evaluated CLEANING_VIEW branch access without proving the actor role. It now joins the permission to the same current HK_SUPERVISOR role assignment, checks branch/global scope and explicit current branch access, at both admission and final disclosure. OWN never becomes branch-wide visibility. Existing session validation and permission service remain in place; no grants are created.

CleaningTaskListScreen binds fetched data to current authorityGeneration. CleaningMonitorDetail additionally binds detail/timeline to branch, kind, ID and page. Changing session/authority immediately masks previous data and unmounts the cancellation panel; old requests are aborted. Cancellation state/page reset for the new record/authority. These changes fix stale UI exposure and state carry-over without changing server mutation rules.

Current-source reconciliation: CleaningCancellationService already restores the latest non-cancelled cycle and evaluates effective readiness when the versioned capability is installed. C180 records this source change; the C179 first-cycle-only gap is historical. This is not installation or business acceptance evidence.

Validation: dotnet build HotelOps.slnx --no-restore PASS, 0 warnings/errors; npm.cmd run build PASS, existing React Router directive and >500kB bundle warnings. Focused CleaningMonitorActorTests/CleaningMonitorRouteTests/CleaningMonitorFs016Tests: 4 PASS, 1 SQL SKIP, 0 failed. No new SQL/HTTPS/browser execution or database mutation. Frontend authority behavior was source-reviewed/build-checked, not browser-certified.

Remaining: verify exact installed cancellation/restoration capability, Cancel transaction and effective readiness, durable notification delivery, approved own cancellation receipt, current actor revocation and end-to-end list/detail/filter/cancel acceptance. Do not mark COMPLETE_VERIFIED from this patch.
