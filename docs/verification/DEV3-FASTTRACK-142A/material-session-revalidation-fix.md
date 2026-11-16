# Material write session revalidation

2026-11-16. Source integration fix; no new complete UC.

The tracked-context session failure proven in replenishment also exists by direct source inspection in MaterialCreateContract, MaterialRequesterContract, MaterialApprovalContract and MaterialIssueContract: each calls SqlSessionService on the action context during revalidation while MaterialRequestService tracks new/updated aggregates and audit. SqlSessionService.RunAsync rejects any tracked entity. This affects Create, requester lifecycle, approval/decision and issue paths.

Extracted the proven replenishment correction into internal Dev2SessionValidation and used it in those four contracts and ReplenishmentWriteContract. It requires SQL Server and the existing Serializable transaction, creates a clean context over the same open connection, enlists in that transaction, and invokes unchanged SqlSessionService. Pending business changes stay tracked, unsaved and unmodified by session validation. Cookie/user/session identity, expiry, actor roles, MR grants, current approver/department authority, warehouse scope, stock-posting gates and final disclosure remain enforced by their existing contracts. No schema or authentication service change.

Backend build PASS with zero warnings/errors. Solution test command executed no tests. Regression against the actual updated HTTPS API passed the existing synthetic replenishment Create/list/detail/Close, duplicate denial, CSRF, independent MR_VIEW restriction, audit/coverage, role revocation and auth/logout checks; cleanup=true. Evidence: recovery-replenishment-session-helper-20261116.json. Temporary listener stopped. Existing runtime model/Take/multiple-collection warnings persist.

Material-specific positive Create/Submit/Approve/Issue runtime has not yet been executed; the replenishment regression does not prove those UCs. The change repairs the same source invariant violation without claiming broader acceptance. No production/shared database changes or production grants.
