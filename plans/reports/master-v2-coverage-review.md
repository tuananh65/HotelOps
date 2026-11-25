# Master V2 — independent acceptance coverage review

Review date: 25 November 2026. Read-only source/evidence audit; only this report changed. Shared working tree was changing during review, so findings describe the inspected snapshot, not a frozen release. No database queries/mutations, API executions, builds, or browser E2E were performed by this reviewer. Existing artifacts below were read directly. `node --check` passed for `scripts/verification/v2-inventory-runtime.mjs` and `v2-realtime-runtime.mjs`; syntax checks are not runtime validation.

Sources: `docs/restructure-v2/OWNER_REQUEST_PART_1.md` through `_PART_4.md`, owner decisions D01–D09, current Compact source, `artifacts/restructure-v2`, and the existing P0 transfer evidence. Final verdict: **PARTIAL; not ready for a 40 CHG / 48 E2E / 8 demo completion claim.**

## Ranked actionable gaps

### P1 — New inventory items cannot enter the lifecycle workflow

- `CatalogService.cs:126–165` validates and saves a new `InventoryItem` but creates no `ItemLifecycleProfile`.
- `InventoryLifecycleService.cs:14` lists only existing profiles. `Configure` at line 20 throws `ITEM_PROFILE_NOT_FOUND` instead of provisioning an unclassified profile.
- `StockBatchAccounting.cs:43–45` requires an existing CONSUMABLE/REUSABLE profile before posting.
- The only discovered production profile insert is the one-time migration `v2-inventory-batches.sql:47`. No application `ItemLifecycleProfiles.Add` writer was present at inspection.
- Consequence: create a new item through the normal catalog UI, then attempt configuration/receipt; it is absent from the profile UI and posting fails. Existing synthetic test fixtures directly seed profiles, masking this path.
- Required: safe profile creation for new item/branch combinations, initially UNCLASSIFIED/UNKNOWN; deliberate authorized classification before stock posting. Add an HTTP test that creates the item through the actual catalog endpoint, configures it, and receives it.

### P1 — Expired/damaged consumable disposal is missing; generic adjustment selects usable stock

- `StockBatchAccounting.cs:64–70` sends every negative non-reversal movement through `Usable`, excluding EXPIRED/QUARANTINED batches.
- `DocumentService.cs:207–214` supports negative stocktake adjustments, but the subsequent batch decomposition has no selected disposal lot or separate disposal transition.
- Consequence: with only expired stock, a legitimate negative adjustment fails. With both expired and good stock, aggregate adjustment can debit good batches while leaving expired batches in the ledger. This is incompatible with traceable expired-item disposal in Master §§15/18.
- Required: explicit batch-targeted disposal/adjustment with authorized reason, immutable evidence, exact ledger reconciliation and concurrency tests. Do not weaken expiry guards on normal issues/transfers.

### P1 — Accepted asset conversion of kettle 222 is not completed

- `v2-inventory-batches.sql:48` classifies 222 as ASSET, and normal stock posting rejects ASSET. This protects it from new consumable use but does not create/reconcile actual assets.
- At inspection no kettle conversion migration or reconciliation report existed. Old warehouse and room-issued quantities are still the accounting baseline; a classification flag alone is not an asset ledger conversion.
- Required: reconcile each old room/warehouse quantity and create auditable asset mappings without duplicate ownership or invented serial numbers; preserve old documents and close the old operational balance through an authorized reclassification transaction. Verify totals before/after on TEST.

### P1 — Required acceptance evidence is incomplete

- No V2 `E2E_TEST_RESULTS.md` with all 48 cases, full preconditions/actors/branches/test data/steps/API+DB+UI evidence/verdict exists.
- Assessment and replacement artifacts explicitly state service+real SQL integration, not HTTP/UI. Inventory fixtures bypass material-request approval and initial shift allocation. These do not prove full demo chains.
- No recorded end-to-end browser walkthrough for any of the eight demos was found in the reviewed V2 artifacts.
- Required: run the missing lifecycle/security tests below, especially Start Shift → assessment → checklist → inspection → End Shift, maintenance rework → readiness, consumable transfer provenance, refill approval chain, and mixed-category reconciliation. Freeze a source/build identity for final results.

### P1 — SignalR reconnect has recorded failure, not merely missing evidence

- `realtime-runtime-first-attempt.json`: 10 checks PASS, then `server restart triggers automatic reconnect and resubscription` FAIL (`Timeout: automatic reconnect`).
- `realtime-runtime-results.json`: later rerun FAIL at fixture startup (`TEST API exited`), no successful replacement run.
- Existing 10 passes prove routing, branch isolation, duplicate suppression, rollback behavior, read-state update, role revocation and first-login restriction for that run. They do not establish reconnect or logout/multitab behavior.
- Required: distinguish harness restart failure from client reconnect defect, rerun restart/reconnect/offline replay/logout/session revocation/multiple tabs, retain separate successful and failed artifacts instead of overwriting evidence.

### P2 — Transfer discrepancy is retained numerically but final resolution remains open

- `TransferStatusProjection.cs:35–37` provides IN_TRANSIT/PARTIALLY_RECEIVED/RECEIVED only. `DocumentService.Transfers.cs` supports request, source approval, shipment and actual receipt; no final discrepancy resolution command was present.
- B07 deliberately avoids unauthorized lost-stock/write-off policy. Retaining the unresolved amount is correct; claiming the full completion scenario after a permanent shortage would not be correct.
- `DocumentService.Inventory.cs:89` requires a derivation reason, but direct `Create` + line + `Post` of a partial TRANSFER_RECEIPT has no specific discrepancy-reason guard in the inspected posting path. Test the direct API path, not only the derivation UI.
- Required: mandatory discrepancy report/reason independent of UI path, linked history and resolution queue; a clearly defined approved policy before permanently losing/returning the remainder. Existing normal partial receipts can continue safely.

### P2 — Expiry warnings and availability commitments are partial

- `InventoryLifecycleService.cs:40` hardcodes a 30-day warning horizon; there is no configuration field for the threshold requested by §13.
- No ExpiryWarning producer was found in the inspected Compact services; lifecycle UI labels a batch “Sắp hết hạn”, which is not a persistent targeted notification.
- `StockBatch.ReservedQuantity` is read by availability but no C# writer was found. Approved transfer/material requests do not reserve batches; subsequent approval can promise the same available quantity. Posting still checks stock and prevents negative inventory, but operational commitments are not represented by the availability displayed elsewhere.
- Required: expiry warning configuration + deduplicated notification; document and enforce commitment semantics, then test overlapping approved requests and low-stock calculation. Do not count a column that remains zero as implemented reservation accounting.

### P2 — Unified request workspace excludes new domains

- Existing `requests.tsx` integrates ordinary requests, consumable refill, replenishment and room-remediation families.
- New replacement operates through `WorkService.Replacement.cs` work events and the asset queue; reusable write-off through its own service/UI. No replacement/write-off/reusable-request projection or navigation was present in the inspected unified request workspace.
- Master §26 permits separate domain entities but requires a common workspace and appropriate routing. Separate tables are not the issue; discoverability, linked timeline and correct “Cần tôi xử lý” entries are.
- Required: project/link these actionable domains into the common workspace without granting OM access to unrelated ordinary request mutations or flattening transactional entities into JSON.

### P2 — Final documentation, schema release contract and UI consistency remain

- Only audit, decision register and owner-source files were present under `docs/restructure-v2`; 18 of the 20 named final deliverables were missing at inspection.
- `SchemaGate.cs:13–14` requires version 3.0.0 and **57** tables. Earlier Compact 3.0.0 was 49 tables. This delta is intentional TEST work but needs an explicit schema/migration manifest so the old 3.0.0 backup cannot be mistaken for the new runtime contract.
- Report 3/4/5 alignment and UC_CHANGE_MAPPING were not present. Do not renumber historical UCs or claim report alignment from the prompt archive alone.
- New lifecycle table lacks the usual search/filter/sort/page controls and silently caps at 1000; batch history endpoint exists but the inspected lifecycle UI does not expose it. Recheck new screens against accepted Compact UX.

### Active implementation observed during review

`DetectedByUserId` and related-person UI were being added to room incidents while this audit ran. They must be built and tested; do not report them as absent based on earlier snapshots. The existing investigation service already preserves cause/history and RelatedUserId with branch validation. The latest forecast source now includes issue/return history, 7/30-day trends, room count/capacity and sanitized incident summaries, superseding earlier missing-data findings. Replacement now has 15 passing service/SQL checks, and reusable transfer now has API evidence. These fixes are recognized below.

## Evidence register

| Ref | Artifact | What it actually proves |
|---|---|---|
| B | `artifacts/restructure-v2/replacement-final-build.log` | Recorded backend solution build: 0 warnings, 0 errors; not necessarily current uncommitted snapshot after later edits. |
| T | `artifacts/restructure-v2/advisory-final-tests.log` | 120 passed, 3 skipped, 0 failed. Skips: maintenance worker, account management integration, worker integration. |
| F | `artifacts/restructure-v2/forecast-frontend-build.log` | Recorded frontend build. No visual/browser acceptance implied. |
| R | `artifacts/restructure-v2/maintenance-recovery.json`, `inventory-classification.json` | TEST recovery/audit evidence; inventory audit records 10 maintenance staff, 20 HK staff, zero changed credentials, zero bad FK/check. |
| A | `artifacts/restructure-v2/assessment-runtime-results.json` | 11 PASS, real SQL/service, rolled-back synthetic fixtures. Reserve/Consume invoked by reflection; initial allocation and HTTP/UI not exercised. |
| I | `artifacts/restructure-v2/inventory-runtime-results.json` | 34 named PASS rows at inspection, including 2 advisory checks and 5 reusable-transfer checks. HTTP posting/custody/lot validation exercised. Approved material requests/return approval fixture-seeded. Cleanup says active fixture items 0, history retained. |
| X | `artifacts/restructure-v2/replacement-runtime-results.json` | 15 PASS, real SQL/service, rollback fixtures, no HTTP/UI. Includes auth, stale version, history snapshots, install guards and queue. |
| E | `artifacts/restructure-v2/invitation-runtime-results.json` | 15 PASS; actual HTTP plus local SMTP sink only, 4 captured messages. Synthetic account password changes; retry/expiry timing accelerated in fixture. |
| S | `artifacts/restructure-v2/realtime-runtime-first-attempt.json` | 10 PASS + reconnect FAIL. Later `realtime-runtime-results.json` is harness startup FAIL. |
| P0 | `artifacts/p0-workflow/transfer-api-results.json` | 8 historical transfer check descriptions from pre-V2 run. Useful regression baseline, not current batch/expiry V2 proof. |

No tests were re-executed by this audit. External OpenAI and real SMTP remain **BLOCKED_EXTERNAL_CONFIGURATION**, as authorized. Local mail-sink delivery is real local integration, not external production delivery.

## Forty-change mapping

Implementation codes: **I** source implemented; **P** partial/gap; **B** retained baseline; **M** missing deliverable. Verification is deliberately narrower than implementation.

| CHG | Implementation / source anchor | Verified evidence / remaining acceptance |
|---|---|---|
| 01 | B: OM/DH consolidation baseline, approved actor recovery scripts | R is supporting audit; dedicated before/after OM/DH permission report and current role E2E needed. |
| 02 | B: workday/task planning under OM | Current V2 schedule/resource regression not recorded. |
| 03 | I: RequestWorkflowService.Query/List filters OM ordinary requests, separate replenishment | Need current negative API tests for OM ordinary processing and other branch. |
| 04 | I: v2-recover-maintenance.sql exact P0 provenance recovery | R supports 10 staff restored, credentials preserved; runtime team-flow still needed. |
| 05 | I: maintenance supervisor assignment/review; recovered permissions | X tests supervisor replacement authority; ordinary team assignment E2E missing. |
| 06 | I: RoomIssueActions internal assignee / external repair | No complete current HTTP internal + external repair lifecycle evidence. |
| 07 | I: CreateRoomIssue rejects OM with OM_REQUEST_READ_ONLY | No dedicated recorded V2 negative API test. |
| 08 | P: Ghi nhận sự cố phòng label + detected/reporter fields being added | Source change recognized; all surfaces/reports and UI proof incomplete. |
| 09 | I/P: WorkService.Investigation cause history + RelatedUserId, UI being completed | No real role/branch/cause-history runtime proof. |
| 10 | I: execution checklist snapshot and completion guard | Needs complete cleaning HTTP/UI test and edited-template snapshot regression. |
| 11 | I: maintenance checklist and independent technical review | X is replacement evidence, not full maintenance/checklist/technical review E2E. |
| 12 | I: WorkService assignment/accept/start/complete, request child links | Whole lifecycle current actor matrix not exercised. |
| 13 | I: completion/review reasons and history | Test required notes, rejection and successful optional-note cases per current Master. |
| 14 | I: WorkService.Replacement + replacement panel/asset queue | X 15 PASS service/SQL; HTTP/UI and continuation through technical/HK inspection missing. |
| 15 | I: RoomReadinessService delegates shared database readiness view/trigger | Multiple blockers/safety/occupancy/checklist current V2 E2E missing. |
| 16 | I: room inspection failure resets technical checklist and routes rework | No current full repair-rework/HK-rework regression artifact. |
| 17 | I: StaffSupplyService initial consumable allocation and reusable snapshot | A fixture bypasses start allocation; new mixed allocation runtime needed. |
| 18 | I: RoomSupplyAssessmentRules + actual Consume | A proves actual deficit/partial refill/reassignment settlement; HTTP/UI still absent. |
| 19 | I: assessment DTO/service/UI, required counts | A validates SQL/service behavior; integrated work page needs browser proof. |
| 20 | I: reservation max-before-assessment, deficit-after, snapshot preserved | A supports reassignment and insufficient/missing-item guards; multi-room concurrency E2E still needed. |
| 21 | I/P: batch tables/entries/custody; new-item profile provisioning gap | I receipt/batch decomposition PASS; normal new item creation not tested. |
| 22 | P: safe expiry checks + quarantine; disposal/warning/config gaps | I expiry rejection/unknown policy/immutable issued lot PASS. |
| 23 | I: StockBatchAccounting FIFO/FEFO | I HTTP FIFO/FEFO PASS; mixed shift and consumable transfer provenance need proof. |
| 24 | I/P: SUPPLY_ISSUE consumer; reusable physical custody path | I verifies generic reusable ISSUE/RETURN accounting; UI/document naming and all caller paths need acceptance. |
| 25 | I: ReusableAccounting positions and ownership | I READY→IN_USE ownership preserved. |
| 26 | I: READY/STAFF_HOLDING/IN_USE/DIRTY/LAUNDRY/DAMAGED lifecycle | I DIRTY→LAUNDRY→READY PASS; full employee replacement/shift chain absent. |
| 27 | I: Storekeeper proposal, independent same-branch OM approval | I DAMAGED availability/ownership, reject/reason/approve/idempotency PASS. |
| 28 | P: kettle222 profile ASSET, operational conversion incomplete | No asset/legacy stock reconciliation evidence. |
| 29 | P: availability-based LowStockService; commitments/warnings incomplete | I available stock query PASS; low-stock event regression skipped in T. |
| 30 | I/P: batch-origin consumable return + reusable condition return | A validates reclassified arithmetic only; full End Shift and batch expiry/dirty/damaged return E2E absent. |
| 31 | I: transfer request/approval/shipment/receipt + reusable custody | I reusable transfer API PASS; P0 baseline helps but current consumable full flow unverified. |
| 32 | I: requested/approved/actual quantities and receipt projection | I reusable partial receipt PASS; UI requested/shipped/read-only fields not verified. |
| 33 | P: partial receipt retained, no final resolution; direct-post reason needs guard | I two partial reusable receipts and over-receipt guard; B07 unresolved permanent discrepancy. |
| 34 | I: transfer-specific limited cross-branch authority | I some positive roles + P0 negative scope baseline; current both-branch negative matrix needed. |
| 35 | I: invitation token/outbox/retry/admin UI | E local SMTP 15 checks; live external delivery blocked by configuration. |
| 36 | I: first-login backend restrictions/setup/session revocation | E API tests + S first-login hub restriction; browser/multitab/direct URL tests absent. |
| 37 | P: persistent authenticated SignalR stream/client | S 10 PASS + reconnect FAIL; final rerun startup FAIL. |
| 38 | I/P: provider abstraction + statistical forecast/API/UI | I verifies labeled fallback, scope and invalid horizon; newer forecast changes only T/F so far; external runtime blocked. |
| 39 | I/P: advisory transfer + sanitized incident analysis | Source updated; tests for full data/quantity/cross-branch calculations and actual UI missing; external runtime blocked. |
| 40 | M: external-system architecture/report alignment | No required final design documents found. |

## Forty-eight E2E mapping

None is marked full E2E PASS here because the required per-case API + DB + UI record has not been provided. **API** means directly recorded HTTP behavior; **SQL** means service/SQL integration; **BASE** means historical evidence only; **NT** means no current targeted runtime proof located; **FAIL** and **BLOCKED** are actual limitations.

| ID | Best existing evidence | Exact missing coverage |
|---|---|---|
| E2E-01 | R/audit | Current OM/DH roles, inherited planning permissions and no unintended role changes. |
| E2E-02 | R migration | Login/assigned maintenance access with recovered account; unchanged credentials recorded. |
| E2E-03 | NT, X partial SQL | Supervisor creates/assigns/monitors actual staff work and rejects outsider/branch. |
| E2E-04 | E API/local SMTP | Administrator UI create → delivery status → invite in local sink. |
| E2E-05 | E API | Browser first login/setup/refresh/multiple tabs. |
| E2E-06 | E API, S hub | Browser/direct URL and all protected API families after restricted login. |
| E2E-07 | E API + S role stream | Current role-change session invalidation plus browser/tab logout; reconnect rerun. |
| E2E-08 | NT | Real Start Shift API allocates configured mixed categories once, batches/custody exact. |
| E2E-09 | A SQL | Public HTTP + work UI assessment, invalid counts and authorization. |
| E2E-10 | A SQL | Full completion HTTP path posts deficit only and updates UI. |
| E2E-11 | A partial SQL | Public assignment blocked by insufficient eligible staff batches, including expiry. |
| E2E-12 | A partial SQL | Multiple concurrent rooms reserve/release correctly without overcommit. |
| E2E-13 | NT | Required checklist prevents completion through direct API and UI. |
| E2E-14 | NT | End-to-end cleaning + inspection + Ready, all blockers honored. |
| E2E-15 | NT | Rework cycle consumes only new actual refill, retries cannot duplicate. |
| E2E-16 | NT | End Shift consumer batch provenance, reusable READY/DIRTY/DAMAGED, retries. |
| E2E-17 | I API | Browser issue workflow; service allocation API FIFO rather than fixture setup. |
| E2E-18 | I API | Same for FEFO across start/refill/transfer callers. |
| E2E-19 | I API | Expired source transfer + staff held batch expiry cases. |
| E2E-20 | NT | Consumable End Shift/return retains exact origin and expiry; ineligible returns unavailable. |
| E2E-21 | I API reusable | Actual user room assignment/replacement UI; blanket-equivalent item fixture is acceptable if explicit. |
| E2E-22 | I API reusable | Staff room collection path + source quantity and branch guard. |
| E2E-23 | I API reusable | UI and repeat/concurrent laundry completion. |
| E2E-24 | I API reusable | Staff discovery/recovery path and reason/history UI. |
| E2E-25 | I API reusable | UI SK propose/OM decide + concurrent duplicate approve. |
| E2E-26 | I availability API; T worker SKIP | Real low-stock event creation/dedup/clear; commitments and reusable READY thresholds. |
| E2E-27 | NT | HK request → HK Supervisor approve → SK actual FEFO issue → staff balance. |
| E2E-28 | I insufficient posting only | Request-level SK rejection with reason; keep inventory and user balance unchanged. |
| E2E-29 | A report fixture only | Actual report API/UI from assigned HK staff, separate discoverer/cause. |
| E2E-30 | NT | Reception on-shift create; off-shift/wrong branch denied. |
| E2E-31 | NT | Cause update/history/related-person/branch permissions via API and UI. |
| E2E-32 | NT | Internal/external repair creation/assignment + notification and same-branch staff. |
| E2E-33 | NT | Accept/start/checklist/complete as assigned maintenance staff. |
| E2E-34 | NT | Independent supervisor technical review, required notes, self-review denied. |
| E2E-35 | X SQL 15 checks | Full API/UI replacement path and technical review/HK inspection after install. |
| E2E-36 | NT | HK failure returns correct repair, resets checklist, retains immutable history. |
| E2E-37 | NT | Multi-incident safety/occupancy/checklist/block conditions and last-blocker release. |
| E2E-38 | I reusable API; P0 BASE | Current consumable request, quantities, both branches and UI. |
| E2E-39 | I reusable approve; P0 BASE reject | Current direct reject/independence/scope + competing approvals. |
| E2E-40 | I reusable API | Consumer FEFO shipment with origin custody and duplicate/concurrent send. |
| E2E-41 | I reusable partial API | Consumer batch-preserving receipt and UI immutable request/shipment values. |
| E2E-42 | I partial receipts; P0 BASE | Mandatory reason through direct create/post; permanent discrepancy resolution remains B07. |
| E2E-43 | I over-receipt API; P0 BASE concurrency | Current concurrent duplicate receipt and wrong-branch source/destination guards. |
| E2E-44 | S 10 API/hub PASS + reconnect FAIL | Fix/retest restart/offline/reconnect/browser dedup and all domain event recipients. |
| E2E-45 | I fallback API + T math | Full forecast dataset/branch/UI; external provider BLOCKED_EXTERNAL_CONFIGURATION. |
| E2E-46 | Source + T unit; external BLOCKED | Current transfer/incident API factual evidence, sanitized payload, human-only action. |
| E2E-47 | Scattered E/I/X/S checks | Full nine-role/branch matrix and all unified request families incl. new domains. |
| E2E-48 | I synthetic ledger, A/X rollback, T regression | Entire migrated TEST inventory/custody/assets/held/in-transit reconciliation + current mixed workflow regression. |

## Eight demo mapping

| Demo | Current component evidence | Missing full demonstration |
|---|---|---|
| DEMO-01 normal cleaning | A, source checklist/readiness | Start Shift to Ready through actual HTTP/UI, then balances verified. |
| DEMO-02 consumable shortage | I FEFO posting, source request workflow | HK→supervisor→SK approval chain→staff receives→resume. |
| DEMO-03 linen replacement | I reusable custody/laundry | One room's clean/dirty paired replacement through staff UI; total ownership before/after. |
| DEMO-04 equipment repair | Source internal maintenance/checklist/review | Whole two-supervisor/staff pipeline, related notifications and final Ready guards. |
| DEMO-05 replacement | X SQL 15 PASS | HTTP/UI from original incident through real asset movement and final inspection. |
| DEMO-06 interbranch | I reusable actual partial receipts, P0 baseline | Current consumer+reusable UI scenario, discrepancy reason, final reconciliation. |
| DEMO-07 account invite | E local API/SMTP | Admin/browser invitation→setup→workspace demo; live mail deliberately blocked. |
| DEMO-08 AI+SignalR | I fallback, S routing partial | Honest fallback advisory→user creates real request→scoped live notification/UI; reconnect failure resolved. External AI demo blocked until configured. |

## Recommended execution order

1. Repair new-item profile creation and consumable disposal path; reconcile kettle migration before declaring inventory complete.
2. Complete discrepancy reason enforcement, expired warning configuration, unified links, and make schema delta explicit.
3. Run current mixed Start/End Shift, refill chain, cleaning/rework, repair/rework/readiness HTTP tests. Use isolated fixtures; retain failed artifacts.
4. Resolve SignalR restart harness/client failure, rerun reconnect and UI subscription scenarios.
5. Execute the eight browser demo chains with API/DB cross-checks, populate all 48 case records (BLOCKED_EXTERNAL_CONFIGURATION where appropriate).
6. Write the 18 missing source-backed deliverables, update actual Report 3/4/5 and final source/build manifest. Run final build/tests after latest edits.

Unresolved business decision: B07 permanent transfer discrepancy settlement authority/policy. No new external credentials question is needed; the owner already instructed to keep live AI/email blocked while finishing independent work.
