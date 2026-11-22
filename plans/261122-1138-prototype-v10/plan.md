# HotelOps V10 — implementation contract

Register: Product.
Scene: Ten hotel roles working at a desk or on a phone while moving between rooms; calm, clear, practical.
Direction: Operational workspace, because users need to identify the next valid action quickly.
Color: Restrained; background oklch(0.975 0.006 180), text oklch(0.29 0.025 190), accent oklch(0.45 0.075 185). Keep Duc Vuong logo supplied with V9.
Type: Segoe UI / system sans-serif; hierarchy by size and weight, not decorative fonts.
Signature: Status-grouped room tiles connected to an actionable side panel; weekly capacity calendar.
Dials: variance 3, motion 2, density 6.

## Accepted scope

Upgrade the supplied V9 ZIP into a separate frontend-only V10 folder and standalone ZIP. Preserve original files, role scopes, shared simulated workflows, weekly shifts and audit histories. No production source, database, API, commits or deployment. Single-agent execution. User explicitly requests Markdown deliverables inside V10.

## Source audit complete

Read all three V9 JavaScript files, stylesheet, HTML, README and five test scripts. V9 contains 10 actors, state stored under a V7 key, 12 rooms, operational workflows, asset warehouse, 46 demo users and weekly rota. Existing tests use a mocked DOM, with historical browser execution blocked. A real Edge run succeeded in this environment before installation of AgentKit.

Observed defects: Reception actions hidden behind a second detail step; housekeeping cannot directly assign checkout rooms without a task; no reassignment for assigned tasks; fake temporal grouping by array index; filters on grouped lists do not rerender; employee overview includes other staff; no overlay focus trap; schedule dialog lacks Escape handling; wizard cannot go back and writes intermediate partial records; account/room text small on mobile; stock history omits replenishment entries; maintenance closure clears other incident blocks. Preserve valid rules while correcting these defects in the standalone copy.

## Execution

1. Reuse V9 model and command handlers in isolated compatibility modules; add guarded V10 operations.
2. Implement shared tokens, shell, accessible overlays, context actions, all 10 role workspaces and responsive weekly calendar.
3. Run real browser workflows, permission/validation regressions, 360/390/768/1366/1920 viewport matrix, keyboard checks and AgentKit render checker. Inspect screenshots and repair.
4. Write requested audit, before/after, workflow and test reports. Package ZIP; verify its contents and unchanged V9 hash.

AgentKit owners actually loaded: ak-agentkit, ak-frontend-design, ak-web-testing. No delegation, publishing or production mutation.

## Completion evidence — 2026-11-22

All four execution steps completed. Standalone folder and ZIP: HotelOps_UX_Prototype_V10. Original V9 archive SHA256 unchanged.

- Browser regression: 38/38 PASS; extended browser cases: 14/14 PASS.
- Ten roles, 16 explicitly defined workflow groups, 111 distinct clicked selectors in the test manifest.
- Responsive: 44 role/page combinations × five widths = 220 checks, no page overflow or missing control names.
- AgentKit render-check: five login viewports with zero findings. Unmodified pageChecks used after login: 220 workspace combinations, zero findings after contrast/touch repairs.
- Keyboard focus/Tab/Escape and mobile room drawer passed. Full assistive-technology and physical-device verification remains unperformed; accessibility reported PARTIAL.
- Direct file:// mode passed; local HTTP mode passed; JavaScript syntax checks passed for 13 files. No .NET/API/database tests applicable to this standalone scope.
- Package: 55 entries, each SHA256 matches its source file; ZIP 2,428,120 bytes, SHA256 41C09C2C3F7FBEAC0B38AD64EE5D3C01B52E8CB154C6BF1D3AEA5151DE809C5B.
- Evidence: artifacts/ux-v10/package-verification.json, release-metrics.json; standalone reports and copied evidence inside the V10 folder.

Initial browser launches inside sandbox produced no useful evidence and were not counted. Successful runs used approved process execution outside sandbox. Early click failures were diagnosed as smooth-scroll test-driver races; fixed with instant scroll/reduced-motion and condition-based reload waits, then rerun successfully. No AgentKit script changes were made.
