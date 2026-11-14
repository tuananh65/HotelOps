# Source recovery — direct evidence, 2026-11-15

Workspace: `D:\HotelOps\HotelOps_Fullstack_Shared`. Remote: `https://github.com/tuananh65/HotelOps.git`. Initial branch and HEAD: `integration/dev1-dev2-dev3-unblock-20261114`, `3268e4628b9ef4897827ff611a2be2d221a4189d`. Initial tracked/untracked working tree was clean. No stash, reset, branch switch, merge or cherry-pick performed. Fetch required elevation because sandbox denies `.git/FETCH_HEAD`; elevated fetch succeeded. Remote baseline had not advanced at initial verification.

Live `git ls-remote` PR heads were compared with local ancestry (`git merge-base --is-ancestor <head> HEAD`, all exit 0):

| PR | GitHub head | Existing merge |
|---|---|---|
| 118 | 6a47868684548dba417b9bd96457b0b45adb32e5 | f2f65d9 |
| 119 | c461f935a8174079d3a3c79dffc31c7aef7cc8a9 | f20fbb9 |
| 120 | e997b25195b8749090c7049f7a80c50a73368b61 | 939017f |
| 121 | 6a9837c32c7f5537a58b022f85645f9c511cb3b6 | d2e2341 |
| 122 | 830362c17032ae10013bc166d8fd34aed14b9de6 | 3268e46 |

All five PR source heads are already integrated. This proves source ancestry, not acceptance of all their UCs. There is no reason to merge/cherry-pick them again.

Fetched all remote heads without pruning. `remote-source-candidates.json` records 146 fetched refs: 32 are ancestors, 114 are not ancestors of this baseline. Non-ancestry does **not** mean all code is missing: prior collection copied/reconciled source rather than preserving all branch ancestry. Each record includes exclusive commit count and source-tree difference count; counts include older branches missing newer canonical files, not just new functionality. No bulk branch replacement is safe from these counts alone.

`priority-source-candidates.json` records exact branch SHAs, recent history and added paths relative to canonical for four priority candidates. Direct inspection found:

- `feature/DEV2-fe14-fe15-scoped-review`: missing canonical `DashboardsController`, `Services/Insights/DashboardService`, scope/DTO infrastructure, plus FE15 reports/audit. Inspected `InsightAccess`: branch/department permission filtering is explicit. Separate FE14 imports from FE15 and verify permission/source contracts before integration.
- `feature/dev1-fe05-authoritative-composition`: missing conclusion controller, source preparation, rework coordination and projection candidates. Inspected `InspectionConclusionService`: compiled dependency lists still block writes; adding these files does not complete UC044–047.
- `feature/dev1-fe05-approve-reinspection-composition`: additional conclusion/reinspection/notification infrastructure, including database mappings and entities. This is a cross-module candidate requiring reconciliation with current cycle/evidence/notification schema, not a safe blind merge.
- `feature/DEV2-deactivation-writer-coordination`: maintenance cycle contracts/entity/service fragments absent from canonical. Must reconcile schema compatibility and existing action capabilities before claiming UC006/UC010 coordination acceptance.

Historical `DEV3_SOURCE_PUBLICATION/MANIFEST.json` identifies copied source from b85743a, local increments, UC004 and a held PR51 rowversion patch. These are provenance, not current test receipts. Historical standalone/prototype route compositions remain distinct from the production main/AuthRouteHost graph.

Implemented first independent recovery: canonical AuthRouteHost now enables existing Users directory/access review and Hotel branch structure read routes. APIs retain their existing authorization, DTO and session controls. Tests now mount the actual canonical host rather than passing read flags into a parallel host fixture. No production mutation gate, database mapping or permission assignment changed.

Also repaired the unified NoSQL harness: current controller authorization and shared CSRF middleware replace obsolete action-attribute assumptions; test-only action capability fixtures let the retained writer-order probes reach the actor User query. Direct final result is 101 PASS with no real SQL connections. Frontend test concurrency is bounded in package.json to avoid browser saturation. These are current execution results, not copied historical receipts.

Remaining source recovery is explicitly PARTIAL: priority candidates above are preserved on remote refs and inventoried; this run does not claim their workflows are integrated or accepted.
