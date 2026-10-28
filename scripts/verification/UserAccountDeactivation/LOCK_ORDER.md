# Lock order

Required eventual mutation order: caller-owned SQL transaction -> global-admin
application lock -> canonical Users locks -> feature/resource locks -> authoritative
state/authority reread -> write, session revocation and audit -> LastAdmin poststate
check -> commit. Roll back on any rejection/error/cancellation; no automatic reassignment.

GlobalAdminSerialization.ReadSnapshotAsync currently retains TABLOCKX on Users before
catalog reads. Integrators must preserve this existing conservative serialization;
do not acquire target U locks then upgrade a separately acquired user universe lock.
Participant User locks must use ascending IDs and UPDLOCK,HOLDLOCK and be retained
through transaction end. Resource discovery before locking cannot authorize a write.

Current diagnostic helper acquires no retained eligibility locks and offers no race
guarantee. It must not be reused as a standalone check-then-update gate.

DEV1 current business writers remain absent/partial per Task40 matrix. Activation and
branch assignment already lock users in ascending order, but this does not certify
future cleaning/task/shift/handover writers. DEV2 paths listed in integration dependencies
remain resource-first/ordinary eligibility reads. Reverse-order review is not closed.
No SQL lock-manager evidence was collected in this task.
