# Approved Task 35 authority

The user's Task 35 clarification is the authority, not Task 32/33 approval or the historical draft matrix:

- Valid unbound USER_MANAGE/ALL: any branch.
- USER_MANAGE/BRANCH: effective actor and target UserBranchAccess in the same active branch AND an eligible BRANCH grant in that exact branch.
- No OWN, self-activation, self-deactivation, branch-bound ALL widening, SYS_ADMIN/OWNER/name bypass or automatic grant.
- Active/nondeleted actor, eligible role/assignment/time/access and normal SQL-backed session required; restricted actor denied.

`UserAccountStatusAccess` deliberately delegates only the identical permission composition and transaction authority-lock mechanism of `UserCredentialResetAccess`. It does not invoke credential reset, reauthentication proof, identity checkbox or target-ACTIVE reset rules. INACTIVE target is eligible for activation. There is no new permission or catalog mutation. The adapter alone is not a mutation boundary.

Activation checks the current accessor against protected reference actor/session, validates actual SQL session, evaluates scope under retained authority locks, and repeats checks before commit. SQL tests cover ALL, unauthorized actor, branch membership/grant alignment, cross-branch, OWN, self, inactive actor, revoked assignment and revoked/mismatched session. Existing Task34 regression covers authority revocation serialization for reset. An activation-specific final-commit writer-race suite remains outstanding; do not label reset evidence as activation HTTP certification.

Minimum last-admin policy is separately APPROVED: at least one ACTIVE/nondeleted, currently login-eligible principal with effective unbound USER_MANAGE/ALL AND ROLE_MANAGE/ALL. It is a capability intersection, not a role-name count. No UC-006 last-admin mutation guard is implemented or certified. Effective login eligibility (hash support, lock and temporary expiry) and serialization against all capability writers must be proved before deactivate can run.
