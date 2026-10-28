# Activation primitive and blocked deactivation

Activation requires a clean/untracked SQL Server context, no ambient/caller transaction, positive target, valid input and an 8-byte expected rowversion. Expected bytes are copied before asynchronous work. It owns one ReadCommitted transaction:

1. Lock actor/target users in numeric order using UPDLOCK,HOLDLOCK.
2. Reuse retained HOLDLOCK authority rows/ranges: actor assignments, referenced roles/grants, USER_MANAGE permission, actor/target memberships and relevant branches.
3. Validate actual actor SQL session (user-before-session locks) and approved scope.
4. Hide missing/deleted target after visibility gate; compare rowversion, require INACTIVE-to-ACTIVE.
5. Conditional UPDATE only account_status and updated_at; SQL produces new rowversion.
6. Insert narrow UPDATE audit: actor, target, server time, old/new accountStatus and reason. No generic entity serialization.
7. Read new token; repeat SQL-session and authorization checks under held locks immediately before commit. Return success only after confirmed commit.

Any failure disposes/rolls back the owned transaction. Audit tracking is detached in finally. Tests inject audit failure, precommit failure and postcommit exception; real postcommit outcome stays committed and old-token retry returns Stale. Two admin activation attempts with one token yield one Success and one Stale. This is not an idempotency/recovery contract or real network-loss verification.

Activation never resets password/lock, restores sessions, grants roles or rewrites memberships/profile. Existing revoked sessions remain revoked. SqlSessionPolicy rejects INACTIVE/deleted users and reactivation cannot revive a revoked/expired session.

Deactivation is explicitly refused before any mutation. No last-admin count, fake handover, assignment deletion or partial status/session commit is provided. Existing SessionRevocationService is composable with caller transaction and its regression passes; UC-006 deactivation+revoke+audit atomicity is NOT VERIFIED because the workflow is blocked.

Proposed shared protocol, NOT integrated/approved by DEV2: every responsibility-creating/reassigning/reopening writer locks affected assignee users before work resources, checks ACTIVE/nondeleted eligibility while holding that lock through commit, and follows one reviewed lock order. Deactivate holds target lock, checks authoritative current dependencies and their relevant key ranges, changes status/revokes/audits. A later writer must then see INACTIVE and reject. Checking only existence, relying on FK locks, or locking only from deactivate cannot establish this invariant. Capability-removal writers also need a common serialized last-admin protocol; counting under an isolated read is insufficient.
