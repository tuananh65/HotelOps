# FS-003 — DEV2 dependencies

Bàn giao local, không gửi tin nhắn hoặc sửa source DEV2. **UC032 progress-only save không cần notification producer** và không dùng no-op publisher. Không biến independent progress acceptance thành acceptance cho lifecycle UCs.

| Component/endpoint | Required contract | Current status / blocker | Requested decision | Impact DEV1 | Evidence |
|---|---|---|---|---|---|
| Assignment/inspection/rework notification | Same-transaction durable intent, immutable source/recipient facts, retry/dedup/replay | Task73 writes report NOT_INTEGRATED; B01/B02/C01 proposals still revise | Supply exact revised owner-specific diffs/approval path | Blocks UC029/041/046 complete acceptance, not UC032 | TASK73_DEV2_NOTIFICATION_HANDOFF; FS001 pending approvals |
| Recipient source read | Explicit recipient identity authority; permitted own source route, branch/ownership at delivery/read | CLEANING_VIEW monitor branch/global cannot assume HK_STAFF recipient access | Agree source-read contract with DEV1/DEV3, no actor/grant impersonation | Recovery monitor / notification deep links cannot be assumed usable | immutable DEV1_NOTIFICATION_INTEGRATION_CONTRACT.md |
| Room blocks/incidents | Effective unreleased blocks, incident-specific release, current-cycle/coordinator/transaction history | Shared schema/index/lifecycle/writer proposals not applied | Exact DEV2/shared diff review, no auto block release | Blocks readiness derivation/Room Ready confirmation | immutable DEV1_ROOM_WORKDAY_SHARED_CONTRACT.md |
| Maintenance/PM time participant | Actual occurrence/source ownership, same physical SQL transaction, lock order/rollback | Contract pending, no duplicated time model | Coordinate with DEV3 FE12 before execution integration | Does not block progress; blocks broader readiness/time workflows | DEV1_DEV2_WORKDAY_PM_CONTRACT_AUDIT.md |
| Overdue/reference-data screens | UC127 notification ownership, FE06/11 reference API scope | FS002 screen metadata mismatch/shared gap | Confirm DEV2 API/permission/source-read boundaries | Do not credit task overdue UI as UC127 completion | docs/fs002/SCREEN_API_MAPPING.md |

Không tự thay đổi Notification/Room Lifecycle/Workday proposals. Existing approved UC032 DB business/audit transaction remains untouched; file evidence is progress data, not delivered notification evidence.
