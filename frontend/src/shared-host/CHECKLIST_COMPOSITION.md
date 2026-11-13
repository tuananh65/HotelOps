# FASTTRACK-063 isolated Checklist host composition

Base: PR64 `aa44a0e4a0c5a5ca9f2deed47f01aa93b86082e3`.
AuthStore correction and its tests: exact PR89 `9699ca02037255348ab47cdd558c71ea848620df`.
FE05 source: eight `features/inspectionChecklists` files plus `fe05-checklists.test.mjs` and `fe05-checklist-route.test.mjs`, unchanged from develop `c4b23ed2099791d5cbd31a50629ff73cad6f6a8d` (PR87).

Proposed candidate destination: `/inspections/checklists?branchId=<canonical-positive-safe-integer>`.
`SharedAuthHost` defaults `checklistEnabled=false`; no environment/query override. Only an explicit isolated test composition passes true. `main.tsx`, Return URL allowlist, UC041/UC049 flags and existing operational routes are unchanged. This is not production mounting authorization or approval to merge PR64/89.

The single existing AuthProvider/store remains the identity authority. Unknown/restricted/anonymous identities cannot initiate checklist reads. Session-object replacement and branch changes replace the entire admitted subtree, clearing forms/snapshots and aborting old requests. Revalidation uses the PR89 synchronous invalidation fix.

No permission is inferred from a role name, ROOM_VIEW, route query or session existence. The host first calls the existing branch collection API through FE05's validated adapter; only a successful matching DTO allows mounting the checklist controls. Source authority is `ChecklistCollectionService.ListChecklistsAsync` on the pinned develop source: `BatchAsync(..., permissionCode: "CHECKLIST_MANAGE")`. Backend authorization remains mandatory for every subsequent read/write. Failure stays denied; explicit session recovery is offered without an automatic read/revalidation loop. The FE05 leaf also clears its controls on later 401/403.

Server prerequisite: deploy the separately reviewed branch collection endpoint/schema with its actual CHECKLIST_MANAGE policy. Its presence on develop does not make this stacked frontend candidate runtime-ready. There is no new entitlement or permission-resolution endpoint.

Offline Playwright tests serve a local simulated SPA fallback and simulated API responses. They exercise direct-open/reload/Back, branch/session races and delayed responses, not real HTTPS server fallback or SQL/session/grant revocation. Live origin, cookies/CSRF, SQL audit/concurrency and production route activation remain separate acceptance gates.
