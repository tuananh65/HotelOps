# Authority and transaction-bound revocation verification

Authority is the explicit Task35 UC-006 approval, reaffirmed by Task36. Historical SRS/matrix proposals and UC005 approval do not substitute for it:

- Eligible USER_MANAGE/unbound ALL, or eligible USER_MANAGE/BRANCH in an active branch shared by effective actor/target UserBranchAccess.
- No OWN, self-activation, branch-bound ALL widening, role-name bypass or automatic grants.
- Actor ID/session derived from the authenticated protected cookie plus CurrentUserAccessor's SQL-backed snapshot; the activation service validates that exact SQL session again inside its own transaction.
- Active/nondeleted actor, eligible active role/assignment/time/grants and normal session required. Restricted actors are rejected.

`UserAccountStatusAccess` reuses the identical approved UC005 scope composition and `LockAuthorityAsync`; it does not call credential reset, use a proof cache, or apply the reset target-ACTIVE rule. PermissionService remains the evaluator. No RBAC catalog, foundation or DEV2 changes.

Activation locks actor/target users in deterministic numeric order with UPDLOCK,HOLDLOCK. It retains HOLDLOCK authority rows/key ranges for actor UserRoles, referenced Roles/RolePermissions, USER_MANAGE Permission, actor/target memberships and related Branches. SQL session validation holds user-before-session locks. Scope/session/time are checked under these locks and again immediately before commit. Credential/session state changes following the same user lock conflict with this transaction; failures/deadlock victims roll back rather than permit.

## Actual SQL race evidence

ActivationRaceSuite creates owned BRANCH-grant fixtures and runs independent activation/revocation connections. A DbTransactionInterceptor pauses activation at TransactionCommittingAsync, **after the final gate and before actual COMMIT**. Seven revokers update:

1. Actor role assignment active flag.
2. Referenced role active flag.
3. RolePermission scope BRANCH -> OWN.
4. Actor branch membership revocation.
5. Target branch membership revocation.
6. Actor SQL session revocation.
7. Actor AccountStatus ACTIVE -> INACTIVE.

An independent SQL observer checks sys.dm_exec_requests: the writer SPID has `LCK_M_%` with blocking_session_id equal to activation SPID. This is an observed SQL lock wait, not a mocked result or inference from a delay. Releasing activation permits its authorized commit, then the revoker commits. The next activation request is denied at the gate (Forbidden or SessionInvalid) before stale/no-op/target disclosure. **21 assertions PASS across7 races.** ALL positive authority and other invalid grants are also exercised over real HTTPS/SQL.

This proves ordering of the tested writers against activation, not cancellation of a transaction that serialized before revocation. Locks may cause contention/deadlock victims under other lock orders; no automatic replay is added. Existing foundation evaluates time-based validity at the final gate; SQL locks do not freeze wall-clock time. Privileged out-of-protocol writes are outside this guarantee.

Audit is one narrow UPDATE record in the same transaction: backend actor, target ID, server occurrence time, INACTIVE/ACTIVE and trimmed reason. Audit failure rolls back status/token; response never precedes commit. The route-local filter translates only body-limit BadHttpRequestException to sanitized413. All other failures retain AuthenticationExceptionMiddleware's existing500/503/no-store contract; it logs trace ID and exception type only, without exception text, SQL/provider details or request/cookie values. Actual HTTP logs are checked in memory against passwords/hashes, cookies, CSRF and certificate secrets. No plaintext credential is created by activation.

Last-admin/open-work coordination is a **deactivation** gate under the approved Task35 minimum policy. Activation does not remove capability or responsibility; no last-admin/dependency bypass or deactivation path is introduced. See DEV2 handoff for unresolved deactivation requirements.
