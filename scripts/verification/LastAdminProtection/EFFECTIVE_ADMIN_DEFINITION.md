# Effective administrator definition

Authority: explicit Task 35 minimum capability policy and Task 41 clarification. SRS GBR-026/027 remains background; the draft Permission Matrix does not authorize role-name shortcuts or new permissions.

| Layer | Implemented definition / boundary |
|---|---|
| Capability candidate | User ACTIVE/nondeleted under the same SQL comparison as current PermissionDataSource; effective active UserRole and Role; AssignedAt <= server time; ExpiresAt absent/future; UserRole.BranchId NULL; exact ordinal ALL scope and permission codes USER_MANAGE and ROLE_MANAGE. Both capabilities intersect per principal and can come from different independently valid roles. |
| Durable eligible | Candidate with a supported existing Identity V3 hash, no TemporaryPasswordExpiresAt marker, and at least one usable unambiguous Username or Email accepted by the current authentication identifier resolver. Finite LockedUntil does not remove durable eligibility. No employment_status exclusion is invented: current authentication does not enforce it. |
| Immediately login-capable | Durable eligible with LockedUntil NULL or <= evaluated server time. This means structural eligibility to authenticate with the correct password, not proof that somebody knows that password, network/service availability, or successful current login. |
| Temporary credential | Any nonnull temporary expiry is excluded, whether expired or future: login would fail or yield a restricted session, which cannot perform global administration. No temporary credential is extended or converted. |
| Permanent lockout/disable | INACTIVE/deleted is excluded. Current schema/Auth has only a nullable finite datetime LockedUntil, no permanent-lockout state or documented sentinel. No sentinel is invented. A future permanent flag/policy needs explicit implementation; it cannot be counted by assuming finite lockout semantics. |
| Session | No existing session required; logout/session/proof expiry does not itself remove a regular credential's re-login path. No client-supplied actor/proof is used by this server-side snapshot. It is not endpoint authorization. |
| Future eligibility | Return whether a current durable principal has nonexpiring paths for both capabilities; also report earliest known capability loss among currently eligible principals. This is not a guarantee against later mutation, identifier collision or external changes. |

Identifier check mirrors `AuthenticationLoginService.Candidates`: trim the supplied Username/Email with .NET semantics, reject whitespace/length >150, then query SQL `Username == identifier OR Email == identifier`, Take(2). Exactly one match must be this principal. Inactive/deleted colliders still make that identifier ambiguous. SQL uses the database collation; in-memory case folding is not substituted. One remaining unique login path is enough. Identifier/account namespace is held stable with retained SQL locks.

Hash support reuses the existing private validator made internal; no duplicated parser or changed hash algorithm. A structurally unsupported/null hash cannot count. Supported stored format does not prove password knowledge or approve a credential migration.

For each capability, the latest expiry across currently effective paths determines its survival horizon; any nonexpiring path wins. A principal loses the intersection at the earlier capability horizon. Redundant earlier-expiring paths therefore do not falsely define its loss. Future scheduled assignments are not counted early or assumed to rescue it. Sampling occurs after SQL locks are acquired; if a qualifying path expires during the read, the snapshot is recomputed.

Current RolePermission has GrantedAt but no expiry field; the existing PermissionService does not impose a separate GrantedAt clock gate. This primitive mirrors that approved foundation rather than inventing grant-expiry semantics. A future credential/grant expiry mechanism must be incorporated explicitly before it is supported.

`EnsurePreservedAsync` certifies only a mutation-time nonzero durable count under the original held transaction. A surviving finite-only principal is reported as **FUTURE EXPIRY NOT GUARANTEED**; no scheduler, auto-renewal or recovery policy is created. Even nonexpiring path coverage is not a claim of a system-wide invariant while existing writers are unprotected.
