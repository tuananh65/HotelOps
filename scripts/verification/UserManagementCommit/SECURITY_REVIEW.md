# Security review

Reviewed 74 unique candidate paths, derived from the exact original 66-file classification plus 8 Task 28 files. Literal private-key/token/credential patterns and server/connection-string references were inspected. No literal credential was found. Broad Password assignment matches in five harnesses resolve to runtime-generated fixture passwords/certificate passwords, not embedded secrets.

Five verification harnesses refer to the established local SQL instance and construct integrated-security connections to guarded trial names. These are test-only connections with trial-identity/receipt checks; no machine-specific server or connection string was added to shared application configuration. Fixture passwords, cookies and CSRF tokens stay in memory; report receipts are not committed. Pattern scanning is not a full DLP certification.

Application review: DTOs exclude credentials and permission graphs; detail uses Global USER_VIEW, profile uses USER_MANAGE. ALL is unbound only; BRANCH requires common effective membership and a valid grant in that branch; OWN requires self plus grant. No role-name bypass or catalog changes. PATCH whitelists fullName/email/phone, rejects duplicate/unknown fields, uses rowversion If-Match and atomic audit. Session revocation is not added to normal profile updates.

Shared diffs are bounded: Program.cs validator registration/invocation plus two previously approved profile registrations; HotelOpsDbContext one User rowversion mapping and the approved partial property. No DEV2 source, permission catalog, original release SQL or canonical seed is included in the changed application set. Trial SQL forward/rollback guards remain intact.

Every staged group must exactly equal its manifest allowlist, pass cached whitespace checks, reject excluded extensions/paths and undergo an independent build from the index snapshot. Program.cs is staged by a checked patch for the gate, then only the remaining two profile registrations. No git add . or git add -A.

All artifacts outside the allowlist are preserved. SQL tests operate only on newly created disposable trials; owned test roles/principals and sessions are cleaned by the harness. No HotelOpsDB connection or real-session invalidation.
