# UC039 acceptance ledger

Status: **COMPLETE_VERIFIED in the disposable runtime**, checkpoint157. One newly completed UC; no shared deployment. Sources: SRS section5.1 and GBR001/002/003/007/008/056 in requirement appendix.

| Requirement | Evidence |
|---|---|
| PRE01, authorized supervisor | Canonical login, Home navigation and branch admission. Actual HTTPS denies manager-only actor, foreign-branch update and revoked supervisor role. Existing CHECKLIST_MANAGE/session/branch checks remain; clone-only grants are synthetic setup. |
| Open Create and enter applicable types, criteria, groups, flags and conditions | Canonical browser fills the existing form with explicit room type, criterion, group, mandatory flag and pass condition. The backend validates active applicability in the selected branch; foreign type returns400. |
| Invalid criteria, MSG03 and correction | Product now associates MSG03 with each invalid criterion through aria-invalid/aria-describedby. Browser submits whitespace, observes the field-specific reason and zero create requests, corrects it and saves successfully. HTTPS independently rejects missing criteria and whitespace text. Existing model validation still handles all other fields before submission; native form validation is replaced by that existing model validation so the localized criterion reason is visible. |
| Draft version1 and immutable input | HTTPS creation returns201 with exactly version1 Draft and persisted pass condition. Duplicate code returns409. ChecklistService trims text, normalizes code and requires explicit mandatory flags; existing focused model/API tests pass. |
| Transactional creation and audit | Owned audit fault causes error; checklist, version, item, applicability and audit counts remain unchanged. The trigger is removed in finally; original guards are never disabled. |
| Publish and successful feedback | Browser publishes version1 and continues the complete update/retire lifecycle, asserting MSG08 success. Public HTTPS publishes the Draft and uses it for an inspection fixture; specific SQL51721 rejects selection after Retire. Initial inspection fixture is a selection precondition, not new UC041 assignment-producer evidence. |
| Revision/security regression | Missing CSRF/If-Match, stale ETag, three rollback cases and three concurrent transition/edit races remain covered by the same final runtime. Six committed checklist operations have six audits. |
| Cleanup | Runner drops owned clone and removes backup; independent master read confirms exact database absent, backup absent and ports7142/7143 not listening. |

Direct evidence: [C157 runtime,173 records](checkpoint157-runtime.json); nested checklistBrowser records eight successful stages through logout. Generic inherited top-level runtime scope wording does not replace individual checks. [Checkpoint receipt](CHECKPOINT-157.md) distinguishes build/test execution from runtime proof.

Limits: no catalog deployment, shared database modification, generated entity change or new schema. Checklist capability remains a runtime prerequisite. UC040 has its own ledger; unrelated UC completion is not inferred from this trial.
