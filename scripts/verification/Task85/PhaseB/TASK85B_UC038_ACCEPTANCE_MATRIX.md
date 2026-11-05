# Task85-B — acceptance matrix

`Implemented` does not mean SQL/API accepted. No old binary or historical receipt counted as a new PASS.

| Requirement | Current source | Actual Task85B verification |
|---|---|---|
| Room cycle list / DTO / empty result | restored TurnoverHistoryAsync | PASS real production HTTPS/SQL, known empty room vs missing room |
| Date/result filters, stable newest ties, pagination | restored explicit cycle query | PASS actual counts, page identities, normalized result/date inclusion and exclusion |
| Cleaning type/assignees/start/end | historical EF projections restored | PASS real stored fields and full assignment history |
| Inspection result/checklist version/inactive criteria/defects | exact room/cycle/version lineage | PASS real detail; cross-room corrupt link excluded |
| Rework count/history | exact inspection/task/room relationships | PASS real count/detail |
| Recorded cancellation reason | actual CANCEL audit JSON only | PASS recorded reason; missing legacy audit remains null by contract |
| Cycle detail/progress/blocker timeline | restored historical detail DTO | PASS real progress/blocker and versioned inspection timeline |
| Private attachment metadata/download/source authorization | restored path/reparse/size/MIME guards, safe DTO | PASS private PDF bytes, hidden provider fields, unauthorized/foreign/deleted/traversal denies |
| Cookie+SQL session/restricted/revoked/inactive actor | current boundary+production middleware | PASS real login/CSRF/cookie/session, restricted/revoked/inactive account+employment |
| SQL permission intersection/BRANCH/OWN/cross-branch | existing CLEANING_VIEW+INSPECTION_VIEW | PASS branch positive, own denied, missing inspection grant denied, foreign denies |
| Operational ALL positive path | actual catalog grants | NOT_APPLICABLE: zero ALL grants for these2 codes; SYS_ADMIN actual403, no catalog change |
| DI same scoped context/no cycle/participant identity | one approved DI line applied |26 integrated-source DI/boundary/translation checks PASS |
| Actual production startup | Program DI/controller integrated | PASS current compiled API + actual SQL startup gate, pinned HTTPS |
| No business mutations/read consistency | read transaction only | PASS exact business/audit fingerprints; DMV-confirmed blocked reader returns original data after rollback |
| UC037 pagination regression | source unchanged |14 boundary checks + actual HTTPS page2/limit1/totals/sliced arrays PASS |
| UC142 runtime regression | source unchanged |19 DI/static/boundary checks + actual HTTPS/SQL list/detail PASS |
| Auth/authz regression | source unchanged except one approved DI addition |199 isolated checks PASS (93+43+14+49); see postdi logs |
| Framework tests | solution contains no discovered framework cases | exit0, NO CASES / NOT AVAILABLE |
| Build/diff | current production source | build exit0,0 errors/2 NU1900; diff check recorded in validation |

Status **RESTORED & REVERIFIED** on the approved retained trial; final146 HTTPS/SQL assertions PASS,
receipt `https-sql-accepted-receipt.json`. Earlier failed harness runs are retained, not counted
as final PASS or added to146. Legacy optional V1 metadata needs distinct declared coverage;
installed Housekeeping/Checklist V1 extension branches remain NOT_RUN. No shared DB deployment claim.

Additional26 real authenticated HTTPS/SQL field-oracle checks PASS (`field-oracle-receipt.json`),
verifying scalar DTO values/identities against persisted SQL rows, not only HTTP success.
