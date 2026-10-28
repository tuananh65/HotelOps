# Security and transaction review

## Authorization and initial scope

PASS by source review plus actual HTTP/SQL cases: controller requires both Global USER_MANAGE and Global ROLE_MANAGE policies; service re-evaluates both in its transaction. PermissionService only grants global requests through eligible branch-unbound ALL paths. No actor role-name bypass or automatic catalog grant. USER_MANAGE-only and ROLE_MANAGE-only requests deny. Actual cookie, SQL session, CSRF and restricted-session behavior is exercised.

The approved Task 30 policy recorded in ../UserAccountCreation/AUTHORIZATION.md is the scoped approval source: HK_STAFF/HK, HK_SUPERVISOR/HK, MAINTENANCE_STAFF/MT, MAINTENANCE_LEAD/MT, STOREKEEPER/WH, RECEPTION/FO. This review preserves that policy; it does not finalize pending SRS room lifecycle/Reception/Department Head permissions. Historical UC-004 permits multiple assignments; this approved slice accepts exactly one active branch, department and role.

Department must be active, belong to the selected active branch and match the role map. Catalog IDs are resolved, never hard-coded. Role assignment is bound to the selected branch; one STANDARD primary membership records the server actor. No extra role/grant is added.

## Transaction and backward compatibility

PASS: one ReadCommitted transaction owns User INSERT, membership, initial role, account CREATE audit, credential UPDATE and credential audit. Master rows use UPDLOCK/HOLDLOCK until commit. All database operations are asynchronous with cancellation. Caller saves/detaches entries before StageInitialAsync. Credential/audit faults roll back all account data.

ApplyAsync retains its signature, independent transaction requirement, clean tracker/ambient guards, one-time preparation, unchanged Identity V3 hashing, 24-hour validity and legacy reset behavior. Participant stages InitialProvisioning only, requires caller SQL transaction and cannot overwrite an existing credential. Legacy reset tests exercise existing behavior; no reset implementation was added.

Service awaits CommitAsync before PendingInitialCredential.ReleaseAfterCommit. Failure/uncertainty disposes pending plaintext without a result. Internal release is trusted orchestration, not a database certificate.

## Secret handling

PASS within reviewed source and tested Information logging: generated passwords stay in memory until the success-only DTO; audits exclude passwords and hashes. Ordinary read DTOs never replay credentials. Prepared/pending/issued objects redact ToString and consume once. No plaintext cache, retrieval endpoint or retry replay store exists. Error responses omit handoff. Runtime JSON writes only labels/counts/target metadata. No body logging is enabled.

Managed strings are not guaranteed zeroized. Future body logging/APM must exclude credential payloads. Production TLS and telemetry configuration are outside certification.

## Race conditions and SQL backstops

Actual two-connection races independently confirm unique username, email and employeeCode enforcement. Concurrent HTTP duplicate requests yield one 201 and one 409. INSERT errors 2601/2627 are caught only at User insertion, transaction is rolled back before duplicate-field lookup with SQL equality/collation. Soft-deleted/inactive rows remain subject to uniqueness.

Master locks can serialize requests in one branch/role; no throughput SLA is claimed. Authorization recheck does not globally serialize concurrent permission revocation. Permission state changing after the in-flight evaluation remains an existing foundation limitation.

## Commit/response failure

Real SQL post-commit interceptor failure is covered by the expanded participant harness. It throws from TransactionCommittedAsync before release, leaves the complete committed account, and a fresh-context retry conflicts with no credential. Hash, expiry, account count and audit count remain unchanged.

Actual network disconnect at commit, lost HTTP response bytes, or provider commit-acknowledgement loss: NOT VERIFIED. The interceptor is a deterministic service-boundary simulation, not transport fault injection. Existing HTTP exact retry checks must not be interpreted as proof of a disconnected response.
