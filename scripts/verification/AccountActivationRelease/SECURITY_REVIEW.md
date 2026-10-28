# Task37 security review

Review outcome for the approved activation-only contract: no unresolved source security blocker found. No production C# fix was needed during Task37; Task35–36 implementation is reviewed/tested as-is.

## Authorization and revocation boundary

PASS by source and new trial HTTP/SQL execution: valid USER_MANAGE/unbound ALL, or exact BRANCH grant with effective actor/target membership in a common active branch. OWN, self, bound ALL widening, inactive/revoked/future/expired paths, restricted actors and OWNER/no-grant callers cannot activate. No role-name bypass or automatic grants. UserAccountStatusAccess reuses only the identical approved reset scope evaluator/authority locks; it does not reuse reset target-state/proof rules.

Actor comes from protected authentication plus SQL-backed CurrentUserAccessor; reference user/session must match. Actual SQL session is validated inside the activation transaction, and permission/session checks repeat immediately before commit. Deterministic actor/target UPDLOCK,HOLDLOCK plus retained authority HOLDLOCK rows/key ranges protect user roles, referenced roles/grants, USER_MANAGE permission, actor/target membership and relevant branches. Session validation retains user-before-session locks. Actual7-writer races observe LCK_M waits on the activation SPID after its final gate; revocation then serializes and the next request denies. This does not cancel an action that serialized first or freeze time-based expiry.

## Contract and transaction

PASS: dedicated PATCH activate route; strict single reason property >=10 trimmed characters; unknown/duplicate/type/status/actor fields rejected;16KiB transport limit; HTTPS/cookie/CSRF/no-store. Strong quoted uppercase16-hex If-Match required, missing428, stale412, fresh ACTIVE no-op409. Missing/deleted targets are not disclosed outside scope. Only INACTIVE -> ACTIVE may execute.

One owned ReadCommitted SQL transaction, clean/untracked context and no ambient/outer transaction. Expected rowversion is copied. Conditional status+UpdatedAt update advances actual SQL rowversion; narrow UPDATE audit records backend actor, target, server time, INACTIVE/ACTIVE and trimmed reason. Audit/commit failures roll back, result follows confirmed commit. Actual audit fault and primitive pre/postcommit injection verify outcomes/stale retry.

Credential/hash/expiry/counters/LockedUntil, sessions, roles/grants, branch/department/profile and operational assignment state are preserved. Revoked sessions are not restored; new sessions are not created. Actual login still rejects activated locked/expired-credential fixtures. No plaintext credential is generated or replayed by activation. Audit/response whitelist and Information logs are checked in memory without persisting password/hash/cookie/CSRF/certificate values.

Route-local exception filter translates only body-limit BadHttpRequestException413. Existing sanitized middleware500/503/cancellation contract remains unchanged. No general audit framework, new permission, hashing, EF entity/mapping or DEV2 edit.

## Deactivation and scope

PASS exclusion: `/status` and `/deactivate` are absent and return404 to a valid caller. Client status INACTIVE is invalid input; direct service deactivation is hard-blocked before writes. The general Task35 transition DTO can describe INACTIVE for validation/history but is not a mutation API. Last-admin/open-work/multi-writer handover is not certified and is explicitly excluded, not marked PASS. Handoff records unresolved predicates and owner decisions without new lifecycle states or claimed DEV2 approval.

Secret review: allowlist excludes runtime/config/keys/binaries; source uses generated random fixture passwords and temporary certificates, not literal credentials. Verification's local integrated-auth server is not shared application configuration or a secret; appsettings/User Secrets are untouched. Pattern scan of the exact index is combined with manual review of generated credential/SQL/log paths. See VERIFICATION_RESULTS for final index checks and warning limitations.
