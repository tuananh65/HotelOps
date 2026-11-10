# FAST018 ? PARTIALLY_VERIFIED

Baseline: 6fcfe5529efeb027d379a4e1c1fe02cdfbfb1a7e (FAST017).
Worktree: D:/SEP490/Projects/HotelOps-DEV1-FE05-FAST018.
Branch: feature/dev1-fe05-source-consolidation.

## Source consolidation

FAST002 3502ef74e7d8ce7172823c4fe750689b4095259c and checklist 5f1cde86b5b6b730832da6207ef6eb3d3b15cfc9 both diverge from FAST017 at 6aee2a0e692a7814f41c81224709cddfbe1e2ec4. No merge/cherry-pick. composition.json records all 52 manifest entries: 50 missing files imported, one exact four-line collection controller action, one deferred InspectionBoundary.tsx because DEV3 AuthProvider is absent on this baseline. AuthRouteHost.tsx is excluded explicitly; shared auth/router/Program.cs unchanged. Imported FE05 source is exact published blob content after CRLF normalization. Historical receipts/docs and live runners retain their original task identities and are not FAST018 verification credit.

Consolidated modules: UC039/040 real checklist collection/detail/draft/version/criteria/applicability/publish/retire; UC041 committed paginated read collections, assigned-self list and closed assignment queue/options/preflight; UC042 execution/start/save/finish; UC043 active severity selector/defects/evidence; UC048 history/detail/authorized download. No historical writer/service overwrite. R03/R05, N01, D4, Users hook/prelock, issuer/proof/projection, recovery and acceptedConcreteBinding=null are unchanged.

New FE05-only development entry frontend/fe05.html and src/features/fe05Integration/entry.tsx compose assignment, execution, checklist and history. Build with npx vite build --config vite.fe05.config.ts (ignored dist/fe05). Browser paths use exact existing branch/id parameter names. Assignment passes persisted InspectionId to existing execution detail which reloads authoritative detail/ETag. Shared main.tsx stays unchanged. This entry is a development composition, not DEV3 protected-route/sign-in acceptance; it adds no mock auth/permissions. Backend remains read authority.

## UC041 actual integration

InspectionAssignmentDomainComponent is unchanged. Its internal marker-gated SQL trial component is not production authority. Public AssignInspectionAsync remains Unavailable and candidate controller remains NonController in the API. Candidate host links unchanged API Program.cs, adds only the candidate MVC type in that separate executable and verifies receipt/server/database/owner on every request. It uses real authentication/session/CSRF middleware and the same SQL services. No production dispatch is enabled.

Live browser used real API login (not SignInScreen), SQL sessions, Secure HttpOnly cookie, actual CSRF and HTTPS proxy. Queue/options retain exact task/cycle/version/revisions and explicit inspector/supervisor selection. Review/Confirm returns 503 UC041_ASSIGNMENT_NOTIFICATION_NOT_ACCEPTED; stale task tag returns 412; missing CSRF 400; anonymous 401; wrong branch/denied account 403. No mutation retries. Assigned navigation loads actual execution detail 200. This is successful live read/preflight integration, NOT successful committed notification-complete assignment E2E.

Fresh owned target: DESKTOP-JI8U50T\SQLEXPRESS / HotelOps_Canonical_20261110_140204_0ad4ee2e_Fresh, owner 0fa5d44c-2119-4e0b-8132-47332077f7e9. Identity, ownership, synthetic-only/fresh receipt and candidate schema marker checked; no shared configuration connection was used. Existing FAST017 preparer/installer and exact 22-check component suite were reused only as composition regression. Old databases/worktrees preserved. Installer reuses unchanged supervisor candidate schema; no shared/release DDL changes.

After component suite assigned both task fixtures, initial HTTPS run lacked a pending row. Receipt retained. One exact synthetic FAST_TASK_2/FAST2 Inspection was cancelled as fixture setup with owned identity/marker guard; no business cancellation acceptance. Subsequent run exposed an incorrect test expectation: HK fixture lacks CHECKLIST_MANAGE, so checklist collection must return 403. Corrected test, not permission or source. Final 10/10 live cases PASS. Existing imported historical browser suites requiring absent shared AuthRouteHost are not credited or rerun.

Persistence: HTTP preflight keeps 4 Inspection roots, 2 supervisor bindings, 1 assigned root; 2 Inspection CREATE and 2 supervisor-binding CREATE audits originate from the earlier component suite. Total audit count increases because real login/session activity creates users LOGIN/UPDATE audits. No claim all audit rows remain unchanged. See persistence.json. No authoritative assignment/conclusion commit, reassignment or operational Reinspection acceptance.

## Verification

- Backend solution build PASS, zero warnings/errors.
- Scoped host and write-test harness build PASS, zero warnings/errors. Initial host CS9137 OpenAPI-generator build failure fixed only in verification csproj; final receipt retained.
- dotnet test HotelOps.slnx exit 0 (API-only solution; no assertion count credited).
- Imported backend collection suite 28/28 PASS.
- Consolidated frontend API/model suites 119/119 PASS.
- Reused assignment component SQL 22/22 PASS, including separate-connection reload, rollback and concurrency. NOT authoritative assignment acceptance.
- Assignment browser regression 5/5 PASS with HTTP interception (not live).
- Separate live browser HTTPS suite 10/10 PASS without interception.
- Shared frontend build and separate FE05 entry build PASS. Existing react-router use-client warnings and npm dependency deprecations remain; no C# warning hidden.
- Historical FAST015/016 suites and N01 authoritative 14-case suite NOT_RUN; source unchanged / acceptance gates absent. Successful authoritative assignment/Reject/Rework/Approve/UC047 commit NOT_RUN.

Initial local command failures (unavailable Python, wrong relative script paths, diagnostic PowerShell overload) caused no shared source/database changes. Corrected with Node/explicit workdirs. Failed fixture/permission expectations are retained separately, never counted as PASS.

## Remaining release gates

See NOTIFICATION_AND_LIFECYCLE.md. UC041 needs accepted Inspection Assigned profile/staging/durable replay/explicit-supervisor schema and initial/reassignment public command contract. UC044 needs Passed event/profile and accepted durable/staging binding. UC045/046 retain N01 SQL acceptance, three resource-scoped recipient approvals, schema/runtime/proof and enforceable immutable private evidence gates. UC047 still lacks accepted criterion mapping and operational Failed schema/lifecycle; no next-round Rework or automatic READY.

RoomType selector contract remains CHECKLIST_MANAGE vs MASTER_DATA_MANAGE read boundary; no new permission granted. DEV3 shared protected routes/real Sign-In integration is not supplied by this baseline. Historical checklist runtime receipts are preserved, not newly rerun or accepted.

Estimates: FAST018 90%; UC039 90%, UC040 90%, UC041 85%, UC042 95%, UC043 95%, UC044 72%, UC045 80%, UC046 80%, UC047 75%, UC048 95%. UC041 gain is actual transport/auth/preflight/navigation and consolidated runnable development entry, not mandatory notification acceptance.

FE04_DEPENDENCY_STATUS: R03/R05_PINNED_UNCHANGED_R05_INTEGRATED_VERIFIED_ISOLATED; NOT_READY_FOR_AUTHORITATIVE_HANDOFF. R05 exact 95f7422e284bc81bcfcf9f460dace52022de5cc7. No UC ACCEPTED or AUTHORITATIVE_INSPECTION_HANDOFF_READY claim. Scoped private commit/push only; no shared SQL, merge/deploy/force-push or edits to other worktrees.

Manifest SHA256 covers physical UTF-8 working-tree bytes with CRLF line endings, excluding the manifest itself. Git normalizes text blobs on commit; no semantic source change is attributed to line-ending normalization.
