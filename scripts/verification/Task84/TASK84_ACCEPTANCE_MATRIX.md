# Task84 — UC142 acceptance matrix, final

131 actual production HTTPS/SQL assertions =125main+6supplemental. Historical Task70 không cộng lại.

| SRS/contract | Result | Evidence |
|---|---|---|
| List/detail và actual startup/DI | PASS | Production health ready, successful real credentials và GET200 |
| Scoped code/name/branch/status | PASS | global4, automatic branchA2/branchB1, explicit branchA2 |
| Active-room counts | PASS | owned2 excludes inactive room, legacy global2, branch/source-scoped detail |
| Code default order, ties, page/count | PASS | same code across branches, page1/typeA page2/typeB, total4 |
| Branch/status/code-name filters | PASS | stored inactive, keyword code2/name1/empty0 |
| Empty list/page | PASS | empty keyword and beyond-last page, matching total/filter contract |
| Invalid page/size/overflow/sort/search/branch/ID | PASS | real HTTP400; isolated boundaries also executed |
| Detail/action metadata/revision | PASS | CanUpdate true for active owned type, false legacy, strong ETag |
| Anonymous/missing permission/restricted | PASS | real401/403, no role-name bypass |
| ALL and effective branch restriction | PASS | existing MASTER_DATA_MANAGE ALL + global/branch-bound assignments |
| OWN master grant path | N/A in canonical catalog | No MASTER_DATA_MANAGE OWN grant invented; HK_STAFF OTHER-module OWN grants denied403; core OWN regression separate |
| Foreign branch/type, legacy ownership | PASS |403/404, NULL remains global-only; no branch inferred from rooms |
| Revoked role/membership/session, expired session | PASS | SQL mutations on owned fixtures reflected in next real request |
| Inactive employment/account | PASS | actual401, account probe separately verifies active200 then inactive401 |
| V1 schema guard/startup compatibility | PASS | guarded historical05/06 install/verify on new target; real query works without global EF model changes |
| SQL read consistency/rollback | PASS | DMV lock wait, writer rollback, no uncommitted name leakage |
| Read-only business/audit/catalog | PASS | fingerprints unchanged through GETs; canonical grants unchanged |
| Production writer/audit-write fault rollback | N/A | UC142 read-only; no writer reimplemented |

Not claimed: shared/production deployment, wholly legacy-schema read success (intentionally503),
actual MASTER_DATA_MANAGE OWN/BRANCH grant records absent from fixed catalog, or unrelated FE-02
writer acceptance. No fake SQL sessions/permissions. Supplemental/main counts include setup/security
assertions and are not framework cases. Full UC142 SRS read flow accepted on scoped V1 trial.
