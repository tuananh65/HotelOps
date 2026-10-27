> Historical Task 32 snapshot. Task 33 now implements trusted reauthentication and the gated reset API; see [current integration](../AdminReauthentication/RESET_INTEGRATION.md) and [executed results](../AdminReauthentication/VERIFICATION_RESULTS.md). Original Task 32 findings below are retained.

# Authorization decision and reauthentication blocker

## Evidence and priority

- Current Task 32 developer approval: authoritative scoped policy.
- docs/requirements/HotelOps_SRS_DRAFT.md UC-005 (line 2099 onward), alternative Reset Password (2150 onward): Administrator verifies employee identity, confirmation, temporary password, revoke all sessions, same-transaction audit and one-time display.
- SRS User Detail Reset Password (14038), GBR-005/023/024: concurrency, 24-hour temporary credential and restricted first login.
- docs/requirements/SRS_CHANGE_NOTES.md: wider matrix is pending.
- Task 21 UserManagement/AUTHORIZATION_MATRIX.md labels global profile/reset authority as proposal.
- Task 24 UserManagementMutation/DECISIONS_REQUIRED.md D1/D4 and SESSION_AND_SECURITY.md distinguish authorization, identity confirmation, stale checks and delivery.
- Task 26 UserProfileUpdate/README.md approval is profile-only; it cannot authorize resets.
- Task 30 UserAccountCreation/AUTHORIZATION.md and Task 31 UserAccountCreationRelease/SECURITY_REVIEW.md approve create only and defer reset/recovery.

The current approval closes reset permission/self/target/session/concurrency decisions. It does not supply a missing security primitive.

## Implemented scope helper

UserCredentialResetAccess.CanResetAsync checks only authority:
- Reject invalid target ID, missing/restricted actor or self target, before ALL.
- Eligible branch-unbound USER_MANAGE/ALL authorizes another target across branches.
- Otherwise query actual effective UserBranchAccess rows for actor and target in the same active branch. GrantedAt must not be future; RevokedAt must be null. PrimaryBranchId alone is insufficient.
- Filter authoritative grant paths to BRANCH, then use the existing PermissionService evaluator per shared branch. This preserves role/assignment eligibility and prevents cross-branch grant borrowing.
- OWN and branch-bound ALL do not authorize reset. ROLE_MANAGE is no substitute; SYS_ADMIN/OWNER names never bypass checks.
- No catalog permissions/assignments are added.

Scope tests combine actual SQL memberships with explicit permission-path doubles. They do not claim the official catalog now grants BRANCH to any new role.

## Blocking gate - trusted recent reauthentication

Source inspection finds credential verification for LOGIN and transaction-local credential proofs for session creation, SQL session validation/touch and change-password flows. There is no reusable recent reauthentication proof/verifier bound to an existing actor/session.

CurrentUserInfo contains userId/sessionId/restriction only. UserSession.CreatedAt and LastUsedAt represent session issuance/activity. Neither is repurposed as a verified reauthentication event. A fresh login is not silently accepted as reauthentication for the previous current session. No client boolean/timestamp/checkbox or synthetic proof is accepted.

Therefore no reset controller or DI registration is added. A future endpoint must verify a trusted actor/session-bound proof with server time, freshness, revocation and credential/session-change behavior, plus explicit employee identity confirmation. Security review/tests for that primitive and end-to-end orchestration are required before opening the route.

The transaction primitive deliberately does not accept a forged reauthentication argument or claim to validate one. Calls in the harness are technical tests, not production authorization.
