# DEV3-NEXT-001 — UC008 Create response normalization

Status: IMPLEMENTED_SOURCE_ONLY. Independent review pending.
Base: integration/fullstack-base at 49ea094a0be619e525a76903ae8b09acba8e16e0.
Branch: feature/dev3-uc008-create-hardening.

## Change and evidence

The client previously predicted the response role code with JavaScript full uppercase. Existing .NET CustomRoleCreateRequest.Normalize uses invariant uppercase without multi-character expansion: straße becomes STRAßE, not STRASSE. Before the fix, the added frontend regressions produced 19 PASS / 4 FAIL (exit 1): three valid normalization responses were rejected and the wrong expanded code was accepted.

The client now uppercases each Unicode code point only when its uppercase is a single code point, preserving characters with expanding uppercase mappings. Exact response-code correlation remains required, alongside the existing DTO, Location, custom/active status, empty grants/assignments, name and description checks. The original request body remains unchanged. No permissive case-insensitive response matching was introduced.

Six new API cases exercise valid invariant codes and reject unrelated, noncanonical and expanded codes. One new offline browser case verifies confirmed Create followed by authoritative detail refresh, with exactly one POST. Three backend DTO checks execute actual Normalize() for straße, ß and the ﬀ ligature. These checks prove normalization only, not SQL varchar representability or database acceptance of every Unicode value.

## Executed verification

- node --test tests/custom-role-api.test.mjs tests/custom-role-browser.test.mjs tests/role-catalog-api.test.mjs tests/role-catalog-browser.test.mjs: 60/60 PASS; exit 0 (33 Create + 27 read regressions).
- dotnet run --project scripts/verification/CustomRole/CustomRole.Verification.csproj: 25/25 PASS; exit 0; no SQL.
- dotnet build HotelOps.slnx: PASS; exit 0; zero warnings/errors.
- dotnet test HotelOps.slnx --no-build: exit 0, no test cases reported by the solution; not counted as executed acceptance tests.
- npm.cmd run typecheck: PASS; exit 0.
- npm.cmd run build: PASS; exit 0; two inherited React Router use-client warnings.
- npm.cmd exec -- prettier --check src/roles/customRoleApi.ts tests/custom-role-api.test.mjs tests/custom-role-browser.test.mjs: PASS; exit 0.
- git diff --check: PASS; exit 0.

## Preserved boundaries and limits

Only the four source/test files listed below and this handoff change. No controllers, business services, schema, SQL, accepted Auth/Users, Hotel or Workday code changes. CustomRoleCandidateController retains NonController; Create dispatch defaults OFF; Update remains ROLE_VERSION_CONTRACT_REQUIRED (issue #28). No DEV2 dependency needed for this client correction; PR #25 remains unmerged and was not copied or cherry-picked.

Live HTTPS/SQL, duplicate/concurrent database Create, audit atomicity and rollback: NOT_RUN. Unicode normalization regression coverage is focused, not an exhaustive cross-runtime Unicode equivalence proof. Unknown/malformed responses remain fail-closed. No UC is newly COMPLETE_VERIFIED from these offline checks. No runtime activation, merge or deployment authorization.

## Exact source SHA-256
frontend/src/roles/customRoleApi.ts: 1EE651526C93F6425915876E30A473F02A1A6CC9B524CCB7CF4A73EDF6079D73
frontend/tests/custom-role-api.test.mjs: AEBE6399B1243ECE2790506E6551563F01A0064C3315F266D4EAF42F33BDCAD6
frontend/tests/custom-role-browser.test.mjs: 7DCF4344C7A03AC8E8755A2E950CD3C16CC1DA32BCC57856F770634DC3760D57
scripts/verification/CustomRole/Program.cs: D19E4B988604CAC7E2D9FD8FD5DF382377C1FF89140AE66BF129F4143BFDDF0B

## DEV3-NEXT-002 synchronized verification

Merged integration f219d6fc6fea5ee6ce6a375cf3a4d7347f99db1c into this feature branch without conflicts. Functional PR delta remains exactly the same five files; inherited UC001 regression commits are not new PR functionality.

Re-executed backend build (0 warnings/errors), CustomRole verification 25/25, frontend Create/catalog 60/60, Auth HTTP/state 94/94, typecheck, build and scoped Prettier: all exit 0. Build retains two inherited React Router warnings. No live SQL/HTTPS or activation. Existing normalization and mutation safety boundaries remain unchanged.
