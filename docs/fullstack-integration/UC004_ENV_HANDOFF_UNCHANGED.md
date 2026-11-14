# UC004 FINAL OWNER HANDOFF ? FASTTRACK-096

SOURCE_HANDOFF=COMPLETE_LOCAL; EXECUTION=NOT_AUTHORIZED; SQL_HTTPS=NOT_RUN.
Composition b9ed872ec68f88384ffdda9fc65398ba98e0154e, tree a7542db029bde58dad0f1b31639b5e65efb96e00, worktree D:/HotelOps/worktrees/dev3-uc004-compose089. Local-only composition, not a remotely approved commit. No new runner/source implementation.

Published chain verified from GitHub: PR90 25d85e2468b7dadcef440adaa9f1511ed5702208 -> PR92 9d46a6d08bab3481068e2af7084fb1619a4249b0 -> PR93 8b863a87e14bd26a2ed34ba272e18d7f250ee3c4 -> PR94 5b3312b05c6d55fc4bc2c382c13f0af56b4a056d. All OPEN/unmerged; PR92 is not Draft, the other three are Draft. Source acceptance/integration is separate from ENV permission.

The adjacent UC004_FINAL_OWNER_COMPOSITION_MANIFEST.json records all 12 changed paths, Git blobs and actual SHA256 for Git bytes and worktree bytes. Existing API/HTTP-runner DLL hashes are observational local hashes only; their source-to-build reproducibility and deployment identity are not independently attested. Trial HTTPS host is external and remains unassigned. No fault host is registered. Previous 103 offline checks are reused as historical receipts, not rerun or counted as live acceptance.

## Three distinct gates
1. SOURCE: DEV1/API/Security review exact stack/composition, schema/fixture/principal design and separate fault contract. Source approval is not permission to connect.
2. READ-ONLY PREFLIGHT: needs actual owned server/database GUID/isolation marker, observer identity/SID/effective rights, valid exclusive lease and exact externally approved receipt. Existing --entity-reads has its own ENTITY_READ_ONLY mode and retained Task17/zero-row model guards; it is not interchangeable with UC004_HTTP. Do not execute either mode from this document.
3. MUTATION TRIAL: separately approve UC004_HTTP origin/deployment, application identity, synthetic actors, allowed operations, lease and rollback/custody. Neither read-only permission nor a local receipt string authorizes POST.

## Exact owner handoff still required
- DEV1 ENV: NEW disposable canonical-named DB, actual machine/server/GUID; HotelOps.UC004.Trial isolation proof; no HotelOpsDB/shared target/M0 reuse. Exclusivity must cover external clients, not just participating applocks.
- Schema/Fixture owner + DEV3 API/Security: approved schema/catalog installation artifacts and binary hashes, EF compatibility, two active Branch/HK Department pairs, existing HK_STAFF, synthetic prefix UC004_<12 lowercase hex>_, distinct Allowed/Denied/BranchBound actors. Allowed has accepted global USER_MANAGE+ROLE_MANAGE; denied lacks admission; branch-bound denied at other branch. IDs/GUID/schema/fixture hashes are UNKNOWN, not invented. Install approved schema/catalog before approved fixtures; never replay historical runner DDL.
- ENV/Security: distinct application and observer principals/SIDs/effective object rights; observer SELECT on required read-back mappings plus accepted session applock rights. App gets only accepted auth/UC004 writer rights. Forbid sysadmin/db_owner/db_ddladmin/db_securityadmin, DDL/grants, ALTER/CONTROL, CONTROL SERVER, IMPERSONATE ANY LOGIN. Current runner checks observer; app identity needs separate deployment attestation.
- Runtime owner: valid SQL chain/hostname with encryption and no TrustServerCertificate; HTTPS normal chain/hostname AND SHA256 leaf pin; API deployment hash tied independently to exact DB GUID (health endpoint alone cannot prove binding). Disable redirects/retries. Synthetic credentials only in process-local UC004_SECRET_* variables, no historical sessions.
- Lease issuer: fresh owner/approval/mode/GUID/fixture/hash/identity/epoch/start/expiry bindings, allowed AUTH_LOGIN, UC004_CREATE, UC004_DUPLICATE_CONCURRENCY, SQL_READ_BACK; no renewal. Runner budget ends by expiry. ENV owns cleanup/rollback/retention; separate observer reconciles unknown commit before any next attempt.

## Source admission and endpoint scope
TrialAdmission.cs Receipt fields and CreateOnlyRunner.cs HttpFixture/HttpActor are canonical shapes. UC004_EXECUTE, UC004_RECEIPT, UC004_RECEIPT_SHA256 and UC004_TRIAL_SQL must be supplied by the authorized operator only after approval. Fixture top level binds approved/databaseGuid/owner/leaseId/mode and exact case-sensitive http fields.
Only GET /api/auth/csrf, POST /api/auth/login, POST /api/users. SendAsync enforces both method and path. --uc004 returns before historical Program body; HTTP_LEGACY is rejected by TrialAdmission before connection/certificate/process/DDL. No default legacy fallback. CreateAuditFault is not registered by production or --uc004. Its proposed unexpected500 is not storage503 proof.

## Command templates ? NOT EXECUTED
Working directory: pinned local composition above.
Offline identity/build (review exact SDK/lock inputs first):
  git -c core.fsmonitor=false rev-parse HEAD "HEAD^{tree}"
  dotnet build scripts/verification/UserAccountCreation/Http/UserAccountCreation.Http.Verification.csproj
  dotnet run --project scripts/verification/UserAccountCreation/Http -- --self-test
These are reproducibility templates; this task reused unchanged receipts rather than rerunning.

Only after target-bound mutation authorization and operator-supplied approved environment:
  dotnet run --project scripts/verification/UserAccountCreation/Http -- --uc004 <approved-canonical-disposable-database> <new-evidence-path>
No example real target, credentials, approved receipt or lease is supplied. Pin final built API/runner hashes before receipt issuance; never substitute observed local hashes for deployed attestation.

## Independent persisted SQL proof
From separately verified observer after request transaction disposal: successful CREATE has exactly the expected account, credential hash (never logged), branch membership, initial role and two sanitized audits. Denied/validation/duplicate paths compare ordered domain/audit fingerprints and untouched sentinels, excluding legitimate auth session touches. Duplicate concurrency needs exactly one durable success. Session expiry requires an approved preexpired fixture, not an invalid-ticket substitute.
For separately approved fault execution: bind actor/operation/GUID/lease/API/host/runner/fixture/profile; require Fired and transaction/disposal proof, sanitized expected500, then NEW observer connection proves no partial account/credential/role/membership/session/audit. Mocked500, unchanged row counts or precommit reads are insufficient. Current separate fault host/registration and its persisted rollback path remain NOT_ACCEPTED/NOT_RUN.

## Delivery
Local handoff and manifest are complete and linked from FASTTRACK096 receipt. Component sources are publicly fetchable at the four GitHub PRs. The exact composed commit and this manifest are local-only. No remote artifact destination/publication permission for this handoff has been identified; DEV1 must nominate an approved issue/PR comment or artifact channel if remote delivery is required. No extra PR or unauthorized Git publication performed.
