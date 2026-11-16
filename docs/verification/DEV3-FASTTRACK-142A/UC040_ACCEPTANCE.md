# UC040 acceptance ledger

Status: **COMPLETE_VERIFIED in the disposable runtime**, checkpoint156. One newly completed UC; this does not certify UC039 or deploy the checklist capability to shared SQL.

Source: SRS section 5.2, UC040 and GBR001/002/005/007/008/056/057. The current approved decisions do not change this checklist version contract.

| Acceptance | Direct checkpoint156 evidence |
|---|---|
| Authorized supervisor, existing checklist | Canonical browser login and branch admission; HTTPS denies the initial manager, foreign branch update and revoked supervisor. Fixture grants use existing catalog codes only in the identity-checked clone. |
| Show version and whether used | GET reports usedByInspection after an inspection references the published version. Existing detail UI renders that response. |
| Save edits and validate | Browser edits published criteria into a new Draft; API rejects empty criteria. Editing the existing Draft preserves its version ID and does not add another version. |
| Publish replacement | UI and HTTPS publish version2 and retire version1. SQL retains the assigned inspection's version1 and original criterion. New inspection selection accepts published version2. |
| Exact historical version | Public inspection-history HTTPS reads return version1 / No dust for the original assignment and version2 / No dust or stains for the new assignment. Inspection records are synthetic preconditions; this checkpoint does not re-certify UC041's assignment producer or execute inspection conclusion. |
| Atomic changes and audit | Injected audit failures for edit, Publish and Retire return errors and preserve the complete detail body, ETag and audit count. Six successful operations produce exactly six audits despite failures/races. No original FK or trigger is disabled. |
| Concurrency | Concurrent edit, Publish and Retire each yield one 200 and one 412. Missing If-Match returns428; stale revision returns412; missing CSRF returns400. |
| Success message | Product displays MSG08 after successful save/transition; browser asserts the message after Retire. |
| Retire alternative | Browser asserts MSG33 and disabled confirmation until checked. API rejects confirmation=false. Retired version selection fails with the specific existing SQL51721 Published-checklist guard, with no inserted inspection. |
| Cleanup | All three trial clones and backups removed; independent master query returns none of their exact names, backup paths absent, listeners7142/7143 absent. |

Evidence: [final runtime, 166 records](checkpoint156-runtime.json), [checkpoint receipt](CHECKPOINT-156.md). Browser evidence is nested in checklistBrowser and records seven stages through logout. The runtime's inherited top-level scope label is generic; the listed individual HTTPS/browser checks are the evidence for this UC. Historical C155 normal-flow integration evidence remains separate and is not counted as a new execution.

Limits: isolated acceptance only; existing reviewed schema capability is required and must fail closed when unavailable. Shared/production DB and generated EF entities were untouched. Completed-inspection conclusion and evidence-history acceptance retain their separate UC042/044/048 ledgers; this trial verifies checklist version retention on assigned inspections, as required by UC040 step7.
