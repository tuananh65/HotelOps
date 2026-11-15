# UC104 ? COMPLETE_VERIFIED (isolated acceptance, C69)

Basis: UC v3 UC DETAIL row 104, SRS ?11.3, GBR005/007/008/010/126/127/138 and approved branch-wide Department Head scope.

| Acceptance | Direct evidence in checkpoint69-trial3-runtime.json |
|---|---|
| Department Head assigns within branch; no invented single-department restriction | Manager has no primary department; executor and foreign-branch manager requests denied; cookie, CSRF and ETag enforced. |
| Active executor role, same branch and department | Active HK executor accepted without PLANNED shift; inactive, missing department, revoked branch, inactive/expired role denied. Shared executor mapping includes HK_STAFF, MAINTENANCE_STAFF, STOREKEEPER; entry regression exercises all three. |
| Consider workload | Paginated/searchable eligible executor picker; same-branch open task count and closed effective seconds today in SYS-PAR-10. SQL confirms closed effort is included. Open elapsed time is not represented as committed effort. |
| All allowed lifecycle origins | Pending Assignment after real Decline, Assigned, Accepted, In Progress and Paused become Assigned. Pending Verification/Closed denied with fresh version; same-owner reset denied. |
| Reassignment reason, audit and previous ownership | Nonblank reason required, at most4000; short reason allowed because GBR012 does not include UC104. Old row becomes REASSIGNED with end time; new ACTIVE row preserves declined and prior assignment history. |
| Atomic time handoff | Previous open interval closes at server time by authorized manager; first actual start preserved; no interval opens on reassignment. New owner must Accept before Start. Paused time remains excluded. |
| Rollback and concurrency | Same-tag concurrent assignment commits once; stale request412. Audit failure and final eligibility loss roll back interval, assignments, workflow, progress, audit and injected role changes. |
| Durable notification | Audit-backed intent verified against physical assignments/progress/interval. Revoked recipient denied; concurrent retry yields one receipt per old/new owner. Generic removal notice grants no source access; new owner source opens after fresh authorization. |
| Canonical UI and persistence | Eight dedicated browser stages: manager Assign, reload, executor Accept/reload, manager Reassign with reason, new executor Accept. Final SQL confirms ACCEPTED. |

Runtime:146 records =33 entry +41 decline +54 assignment checks, four browser sessions/29 stages in total (8 dedicated UC104 stages). These are shared-run evidence, not multiple independently executed suites. Actual hosted assignment worker also passed with two distinct recipients, one receipt each and unchanged source snapshot.

Build:0 errors/warnings. Regression588 PASS/11 SKIP. FE task-client50 PASS; Auth/Router841 PASS; TypeScript and production build PASS. Vite large-chunk warning and existing EF20602/10102 warnings remain. Solution test exits0 with no test project; it is not counted as coverage.

Trial1 retained browser timeout before UC104; trial2 retained harness ETag encoding error after notification assertions. Trial3 passed after explicit binary-to-hex conversion. No product security bypass or test expectation relaxation. Three clones/backups removed; empty source and original trusted CHECK restored; private files/listener0.

This completes UC104 only. UC102 public creation/recurrence and UC103 planning/cancellation/series remain the next work in Accelerator Batch A. Synthetic task preconditions in this receipt are not proof of UC102 or a complete FE11 demo.
