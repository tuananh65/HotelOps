# Security and transaction review

No release-blocking security finding was identified in the reviewed Add-only source. No production security fix or unrelated source change was required in Task 39. This conclusion is conditional on the release verification results, not a certification of future role-management workflows.

## Requirements authority

The original SRS UC-007 includes broader scope changes and GBR-028 open-work handling for removals. The Permission Matrix and Change Notes remain draft. The developer's explicit Task 38 approval separately authorizes only new nonprimary STANDARD memberships using USER_MANAGE. It also resolves this slice's OPERATIONS_MANAGER single-Branch safety rule; the draft GBR-031 several-Branch wording is not used to widen it. No role/permission catalog changes are made.

## Authorization and privilege safety

- Actor identity comes from the protected cookie and validated SQL-backed session, never the body. Actor must remain ACTIVE/nondeleted/unrestricted; target must be ACTIVE/nondeleted and destination Branch active.
- Eligible unbound USER_MANAGE/ALL is accepted. Otherwise independently valid USER_MANAGE/BRANCH paths must cover destination and a pre-existing effective common Branch with the target. OWN, self and role-name bypass are refused.
- Existing destination UserRole rows always conflict, including inactive/expired rows. ROLE_MANAGE does not override this check. No role/grant is created or silently enabled by reconciling dormant paths.
- Active/nonexpired OPERATIONS_MANAGER assignments conservatively prevent Add when expansion cannot be proved safe. The role code is a target cardinality guard, never an actor authorization bypass.
- Existing memberships, including revoked rows, conflict under the actual unfiltered unique key. There is no update/regrant path.
- Authorization/session are checked again before commit. The final common-Branch query excludes this request's inserted row, preventing authorization bootstrap when source authority expires midtransaction.

## SQL race boundary

Ordered actor/target User UPDLOCK/HOLDLOCK precedes retained actor authority, membership/Branch, target UserRole range and referenced role/grant locks. Session validation retains session locks. Ordinary role-assignment/revocation writers conflict with these SQL locks. The harness observes actual SQL DMV lock waits at the final commit boundary and writer-first schedules; it does not rely solely on mocks or timing assertions.

A writer that assigns a role after Add commits is a separate mutation. That writer must enforce its own scope and cardinality contract. This release neither authorizes UC-010 nor claims protection against arbitrary later privileged SQL changes.

## HTTP and atomicity

POST route accepts exactly one positive Int64 branchId, rejects duplicate/unknown fields and mass assignment, requires HTTPS/cookie/CSRF and a single strong User If-Match token. Missing token is 428, stale 412, duplicate/revoked/unsafe-role conflict 409, success 201 with committed new ETag and no-store.

Conditional User UpdatedAt write advances rowversion; membership insert and narrow CREATE audit share the same SQL transaction. Final failure, audit error or precommit error rolls everything back. Success is returned only after commit. Audit carries backend actor, target, Branch and membership before/after without credential material. No password, lockout, account status, primary Branch, Department, roles or sessions are changed. Subsequent authorization uses current SQL data. Normal sessions remain valid; version-bound reauthentication proof can become stale after the aggregate token changes.

## Packaging review

Only explicit source/harness/docs paths are eligible. Staged content must pass whitespace checks, exact allowlist comparison, forbidden-extension checks and secret-pattern review. Runtime passwords, certificate passwords and session credentials in the harness are generated in memory; snapshots of credential/session values are compared in memory rather than written to receipts. Local JSON evidence is excluded from the commit.
