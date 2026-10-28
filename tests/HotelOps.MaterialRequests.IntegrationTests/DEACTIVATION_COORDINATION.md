# DEV2 MR coordination on the clean base

This harness is service-level SQL evidence preparation, not HTTP/Authz coverage
or the DEV1 production Deactivate endpoint. No runtime result is claimed for this
branch. Run only after independent audit, in an idle regression database.

The explicit --deactivation-coordination argument runs the dedicated harness.
Normal runner code is otherwise unchanged. It uses only the externally supplied
ConnectionStrings__HotelOpsDb and rejects every SQL target except @@SERVERNAME
KIENPC / DB_NAME() HotelOps_Canonical_DEV2_Regression. No credentials, schema,
roles or grants are provisioned.

BASE compatibility:
- MaterialCommand still carries ActorUserId.
- The production Command signature and controllers/DTOs remain unchanged.
- Approver selection remains explicit AssignedApproverUserId, with the existing
  latest-cycle fallback on content edit. No role/permission routing is imported.
- Ineligible future approvers produce BASE ValidationException/rollback, rather
  than develop's ApproverRoutingException/intentional audit commit.

Four races use separate open SQL sessions with recorded @@SPID:
A submit: probe retains User 1, waits on a test applock; submit waits on User 1.
B submit: writer retains Users then waits on Catalog; probe waits on User 1.
C request-information: probe retains User 4 at a test applock; writer waits on 4.
D request-information: writer retains Users then waits on Catalog; probe waits on 4.
DMV evidence matches waiter, blocker and the same retained resource; delays or
Task non-completion alone are never accepted as proof.

Dedicated existing fixtures:
- RC-D16-MR-DRAFT: requester 4, DRAFT, warehouse 3, one active line, zero cycles.
- RC-D16-MR-PENDING: requester 4, PENDING_APPROVAL, warehouse 3, one active line;
  exactly one revision-1 PENDING cycle, submitter 4, designated approver 1,
  resolved_at NULL, one preserved line snapshot and no decisions.
- Users 1/4 start ACTIVE/ACTIVE/nondeleted; each request has one seed
  REQUEST_CREATED audit.

The sibling fixture would otherwise be a real blocker for the other race.
The previously approved test-only isolation cancels that sibling through the
BASE service before each race. This is outside DeactivationProbe: the probe
always checks all MR requester/latest-approver predicates, without exclusions.
Unrelated MR blockers cause a preflight failure, never reassignment or deletion.

Finally paths cancel/drain/dispose workers before resetting both dedicated
request names and Users 1/4. Only captured service-generated row IDs are deleted;
seed audits and the original pending snapshot/cycle are retained. Scalar state
is compared after restoration, excluding SQL-generated request rowversion.
SQL hashes additionally detect unrelated MR/User/fulfillment changes. All runtime
proof and restoration behavior must still be audited/executed on this branch.
