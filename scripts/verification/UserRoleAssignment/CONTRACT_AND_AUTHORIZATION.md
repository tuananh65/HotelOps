# Task44 - technically approved UC010 v1 implementation contract

Approval authority: the developer's explicit APPROVAL - TASK44 / UC-010 ASSIGN ROLE V1 in this
conversation, 2026-10-28 Asia/Saigon. P1-P12 are approved for Task44 technical implementation.
This supersedes the earlier CONTRACT PENDING decision and proposal labels below. It does NOT
claim formal SRS/PO approval or finalize the draft Permission Matrix for other features.

Implemented route: POST /api/users/{userId}/roles. Strict JSON {roleCode, branchId}; strong
If-Match User.RowVersion. SQL-backed full session, effective unbound USER_MANAGE/ALL AND
ROLE_MANAGE/ALL, no self assignment or role-name bypass. P2's explicit delegation allowlist,
not runtime permission subset or hierarchy, authorizes exactly the five pinned envelopes.

Allowed pairs: HK_STAFF/HK, HK_SUPERVISOR/HK, MAINTENANCE_STAFF/MT, MAINTENANCE_LEAD/MT,
STOREKEEPER/WH. The compiled UserRoleDelegationPolicy contains the exact permission/scope pairs
in the approved appendix of PRIVILEGE_ESCALATION_REVIEW.md. Set equality is ordinal and exact;
missing/extra/changed grants, inactive/system-flagged roles and all other codes fail closed.

Target must be ACTIVE/nondeleted, requested Branch must equal its existing PrimaryBranchId,
Branch and Department active, Department in that Branch and matching the role's Department,
with an existing unrevoked membership GrantedAt <= current UTC. No membership creation/regrant.
Every existing target UserRole row is a v1 conflict, including inactive, expired, future,
unbound or OM paths; no dormant path is inferred safe. Only a fresh first-role INSERT occurs.

Server supplies AssignedBy/AssignedAt UTC, IsActive=true, ExpiresAt=null. Update only User.UpdatedAt
(to advance its aggregate rowversion), INSERT UserRole and CREATE audit, all in one transaction.
Target credentials/status/PrimaryBranch/Department/sessions and all prior roles remain unchanged.
Actor middleware retains its existing session-touch behavior; the Assign service only validates
sessions and performs no session mutation or revocation.

HTTP outcomes: 201 + committed ETag/DTO; 401 invalid session; 403 insufficient authority/self/
nondelegable role/envelope drift; 404 authorized missing/deleted target; 409 inactive target,
primary/membership/Department safety or existing-role conflict; 412 stale; 428 missing tag;
400 malformed body/tag; 503 global serialization unavailable. Existing sanitized middleware
handles provider/audit/deadlock failures. No-store and CSRF remain the existing API mechanisms.

UserRoleAssignmentService rechecks session, both permissions, target state/current version,
Role flags/envelope, active primary scope and membership, and absence of other assignments at
commit boundary under retained SQL locks. Its own inserted row is excluded only from the
first-role conflict check and separately verified before commit. No newly inserted assignment
can authorize its own actor because self assignment is prohibited and pre-existing actor paths
are retained/rechecked. See TRANSACTION_AND_CONCURRENCY.md and VERIFICATION_RESULTS.md.

The original reviewed proposal is retained below for traceability. Its approval-request wording
is historical; its exact bounded policy is now technically approved by the developer.

---
# Proposed UC-010 delegation contract - NOT APPROVED

## Evidence and decision

SRS `docs/requirements/HotelOps_SRS_DRAFT.md:2535` (UC-010) requires an Active target,
active available roles, business validation, rowversion, atomic assignment/audit and refreshed
effective permissions. Section 4.4 at line 1177 is explicitly **NOT APPROVED**; its historical
Configure/Assign row says Administrator Full and other actors No, without a delegation ceiling.
`SRS_CHANGE_NOTES.md` sections 2-4/7 leave revised Reception/OM/DH permissions pending.

`UserManagement/AUTHORIZATION_MATRIX.md` and `OUTSTANDING_DECISIONS.md` O5, plus
`UserManagementMutation/DECISIONS_REQUIRED.md` D6, explicitly leave delegation, global binding,
self-elevation, combinations and expiry/regrant unresolved. Their old rowversion/last-admin
blockers have since been addressed by separate tasks; that does not resolve O5/D6.
The developer confirmed this missing approval in Task 44. No safe approved UC-010 subset has
been established, so no Assign Role endpoint or callable production mutation is introduced.

UC-004's six initial role/Department pairs and both global permissions were approved specifically
for a new account with one initial role (`UserAccountCreation/AUTHORIZATION.md`). Existing users
can already have roles, future paths and memberships. That approval is not transferable.
UC-007 authorizes only additive STANDARD membership, not delegation or dormant-role activation
(`UserBranchAssignment/OUTSTANDING_ISSUES.md`). It is not UC-010 approval.

## Proposal P1-P12 for business review

**Recommended first slice is deliberately narrow: first role grant to an existing roleless
account in its existing primary Branch/Department. Adding a different role to an account
already holding an effective/scheduled role stays deferred until combination policy is approved.**
This restriction is a proposed release scope, not a statement that SRS forbids multiple roles.

| ID | Proposed rule after explicit approval | Existing evidence / what still needs approval |
|---|---|---|
| P1 | Full SQL-backed actor session; require effective unbound `ROLE_MANAGE/ALL` **and** `USER_MANAGE/ALL`; no role-name bypass. | Existing evaluator/session supports this composition; UC-004 uses both. UC-010's both-permission/global-only gate is NEW and needs approval. |
| P2 | An actor satisfying P1 may delegate only the five role envelopes in the table below, to a different user. ROLE_MANAGE alone, bound ALL, BRANCH, OWN or DEPARTMENT cannot delegate in v1. | There is no catalog field or approved UC-010 grantor-to-role map. These allowed envelopes are NEW delegation authority, not inferred from P1. |
| P3 | Target ACTIVE/nondeleted; Role, Branch, Department active; existing unrevoked/time-valid membership at destination. | ACTIVE target/references: UC-010 PRE-02, GBR-004; nondeleted/membership timing: existing evaluator. Binding these checks to this write needs approval as the concrete contract. |
| P4 | Destination must equal target's current PrimaryBranchId; target's current Department must belong to that Branch and match the role pair below. No creation/regrant of membership or Department change. | Schema has one primary Department, not per-Branch departments. Pairing/primary-only restriction for existing users is proposed, not reused UC-004 approval. Secondary-Branch role assignment is deferred. |
| P5 | Deny all self-role assignment, including ordinary staff roles. | GBR-027 forbids self-admin revoke/deactivate, not self-grant. Blanket self-grant denial is NEW and needs approval. |
| P6 | Deny SYS_ADMIN, OWNER, OPERATIONS_MANAGER, DEPT_MANAGER, RECEPTION, unknown/custom/inactive roles in v1. Deny any candidate whose current grants differ from its approved envelope, contain administrative delegation authority or ALL/unknown scopes. | Actual system flags/grants and inactive roles are evidence; exact protected/deferred assignment policy and drift handling need approval. IsSystemRole alone is insufficient. |
| P7 | Reject any existing exact UserRole tuple with 409, including inactive, expired and future rows. Never update IsActive/expiry/Branch on an old assignment. | Actual unfiltered tuple uniqueness covers history; UserRole lacks RevokedAt. Reject-vs-regrant behavior and HTTP contract need approval. |
| P8 | No conflicting active or scheduled role combination; v1 allows only a first distinct role. Existing dormant conflicting paths require review rather than implicit reconciliation. | GBR-002 permits multiple roles, GBR-018 mentions combinations subject to SoD; exact incompatibility rules are missing. This first-grant restriction is NEW scope, not a permanent SoD policy. |
| P9 | Do not assign OM or add roles to an existing OM target in v1. Later OM assignment must prove exactly one applicable Branch and compatible memberships/roles, including scheduled paths. | Canonical OM is single-Branch; draft GBR-031 allows OM several Branches. Add Branch refuses OM expansion but does not implement inverse Assign Role safety. Resolve conflict explicitly before OM support. |
| P10 | Single INSERT; AssignedBy from actor, AssignedAt server UTC, IsActive=true, no client-supplied expiry/scheduling (ExpiresAt=null in v1). Strict body `{roleCode, branchId}` and strong If-Match. | Schema supports values; aggregate User.RowVersion exists. These exact request/date/duplicate choices are NEW and need approval. No route is opened now. |
| P11 | Atomic assignment + aggregate version + CREATE audit, commit-boundary authority/session/state/envelope recheck. No credential/status/Department/PrimaryBranch/session changes. | GBR-005/007/008 and task safety requirements establish atomicity; exact route/status/audit fields must be finalized in implementation contract. |
| P12 | Proposed response semantics: 201 after commit, 401 invalid session, 403 delegation/self/protected denied, 409 tuple/safety conflict, 412 stale If-Match, 428 missing If-Match; no-store/CSRF. | Existing API conventions, not a published UC-010 contract. Confirm before implementation. |

## Actor/scope -> proposed assignable role table

All ALLOW entries below are conditional on **P1-P12 being approved together**. Currently none
is executable through UC-010. IDs are resolved from SQL; no numeric role/Branch IDs are policy.

| Actor authority | Role proposed for delegation | Destination / Department | Proposed outcome |
|---|---|---|---|
| P1 plus approved P2 envelope | HK_STAFF | Existing primary Branch, HK | Allow first role only |
| P1 plus approved P2 envelope | HK_SUPERVISOR | Existing primary Branch, HK | Allow first role only; independent inspection constraints remain mandatory |
| P1 plus approved P2 envelope | MAINTENANCE_STAFF | Existing primary Branch, MT | Allow first role only |
| P1 plus approved P2 envelope | MAINTENANCE_LEAD | Existing primary Branch, MT | Allow first role only; independent verification constraints remain mandatory |
| P1 plus approved P2 envelope | STOREKEEPER | Existing primary Branch, WH | Allow first role only; no additional approval/adjustment authority |
| P1, or any weaker authority | SYS_ADMIN / OWNER / OPERATIONS_MANAGER | Any / null Branch | Deny/defer v1; no global/system-role delegation is proposed |
| P1, or any weaker authority | DEPT_MANAGER / RECEPTION | Any Branch | Defer pending role-specific UC-010 approval/revised requirements |
| Any actor | HK_INSPECTOR / STAFF | Any | Deny inactive catalog roles |
| Any actor | Unknown/custom role, or allowlisted code with unexpected grants | Any | Deny until envelope explicitly reviewed |
| ROLE_MANAGE alone; branch-bound ALL; BRANCH; OWN; DEPARTMENT | Any role | Any | Deny v1; no implicit delegation or scope borrowing |
| Restricted/invalid session, or actor=target | Any role | Any | Deny |

**Delegation ceiling:** SYS_ADMIN's current runtime grants are administrative, not the union
of operational grants. P2 would explicitly authorize delegation of the five bounded operational
envelopes without giving the grantor operational bypass. This is a new business decision.
If P2 is not approved, do not silently let P1 assign those roles: keep endpoint BLOCKED.
A same-permission-subset alternative would deny many assignments by current SYS_ADMIN; numeric
role hierarchy and naive permission-code subset checks are not approved substitutes for P2.

The role envelope must include the exact approved permission/scope pairs and destination
binding, not only role name. Any catalog drift or unsupported scope fails closed; this review
does not freeze/change the shared catalog or give this service permission to edit it.
