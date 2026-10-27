# Approved Task 38 authorization

Source priority: explicit Task 38 approval, approved Authorization Foundation, then historical SRS/Task 21/24 descriptions. Earlier UC-004/005/006 approvals did not independently authorize UC-007. This implementation follows the separate approval supplied for Task 38.

- Actor comes from the authenticated SQL session and must remain ACTIVE, nondeleted and unrestricted.
- A valid unbound `USER_MANAGE/ALL` path permits management across Branches.
- Otherwise, an eligible `USER_MANAGE/BRANCH` path must authorize the destination, and an independently eligible Branch management path must cover an existing effective common Branch with the target. Both grants must satisfy the existing role/assignment/membership/time predicates. Invalid or branch-bound ALL paths cannot be combined into a valid path.
- OWN, self-assignment and role-name bypass are denied. ROLE_MANAGE does not override membership safety conflicts.
- Target must be ACTIVE and nondeleted; destination Branch must exist and be active.

Authorization is evaluated after retained SQL authority locks and again immediately before commit, with session validation at both gates. The final target-visibility query excludes the row inserted by this request. Thus source authority expiring during the workflow cannot be replaced by the newly created destination membership to authorize that same workflow.

Any destination UserRole row causes 409, including inactive, expired or currently nongranting rows. This conservative conflict prevents latent privilege activation without reconciling dormant role intentions. No role/grant is created or changed. An active, nonexpired OPERATIONS_MANAGER assignment also causes 409 when adding a new Branch: this slice cannot prove it safe under the approved single-Branch rule, including scheduled/inconsistent assignment cases.

The existing PermissionService and SQL data source recalculate grants on each request. Adding a membership does not proactively revoke sessions; rowversion advancement also invalidates any existing target reauthentication proof bound to its previous credential/concurrency version, as defined by the current proof validator.
