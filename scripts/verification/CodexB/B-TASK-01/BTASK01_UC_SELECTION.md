# B-TASK-01 — UC selection

Status: BLOCKED. No UC selected for implementation.

Reviewed exactly three candidates against the SRS detailed use cases, Change Notes, Task16 coverage matrix, current entities, mappings, controllers and Program.cs.

| Candidate | Evidence and mandatory dependency | Decision |
|---|---|---|
| UC-013 / FE-02 View Branch Structure | SRS §2.2 requires scoped branch/floor/area/room/department tree, active filter, node details and empty results. Task16 matrix explicitly leaves its read permission mapping unresolved. Catalog has MASTER_DATA_MANAGE for SYS_ADMIN but no STRUCTURE_VIEW; this cannot establish Operations Manager access. Change Notes leave revised permissions pending. | Requires approved read permission/scope mapping; cannot invent or change RBAC. |
| UC-113 / FE-11 Review Task Workload and Time Logs | SRS §11.11 requires closed work interval effort, provisional open time and separate shift time. OperationalTask, OperationalTaskAssignment and OperationalTaskProgress have no dedicated work interval model. Assignment duration is not execution effort. Task16 matrix identifies this model dependency. | Requires approved interval storage/mappings and time recording baseline; cannot invent schema or substitute assignment/shift durations. |
| UC-123 / FE-13 View Internal Request History | SRS §13.10 includes list/detail/timeline and withdrawal by requester while Pending Acceptance, atomic audit and notification to receiving Department Head. No Internal Requests controller/service or notification publisher is present in B. Task16 matrix also leaves read permission mapping unresolved. | Requires approved scoped read/withdrawal permissions and activated publisher; read-only subset would be PARTIAL. |

Global selection gates: current DEV1 completion ledger, Task82–86 receipts and Codex A active UC reservation are absent from B. Task16 coverage matrix is historical and contradicted by newer user account source; it cannot prove current uncredited status. Task86 totals (26/75 UC, 60/256 points) are user-supplied history only.

No access to other worktree source was used. Exact uncommitted Task82–86 implementations cannot be identified from B; their absence is not evidence of which files A changed. No copy/merge was attempted.

Recommendation: unblock UC-013 first by supplying ledger/reservations, approved read authorization and a separately authorized B trial database. Review DI registration before implementation; Program.cs changes remain forbidden.
