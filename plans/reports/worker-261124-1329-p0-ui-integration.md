# P0 Compact UI integration

## Implemented

- `app.tsx`: permission-based supply configuration / staff supplies routes; actual refill callback routes to typed request creation with allocation ID. Unified request workspace replaces old standalone IR route. OM internal configuration route restored with new room configuration permission. HK/Reception room issue entry uses IR_CREATE; OM room issues remain read-only.
- `requests.tsx`, `requests.css`: actual `/requests` API inbox, server search/pagination/queue filter (collapsed with reset), ordinary IR/material/room tabs, typed request detail, queue/handler, parent link, item quantities and chronological history. Capability-driven claim/release/approve/reject/issue/cancel/receive dialogs. Refill form uses own allocation snapshot; replenishment uses authorized warehouse/items. Partial physical receipt quantities supported. Stable operation keys retained on retry; API failure does not show success. Mobile rows stack and wrap.
- `room-issues.tsx`: HK supervisor confirms/rejects reports; maintenance supervisor accepts and starts external repair supervision; store/maintenance submit remedy directly to HK review. Reinspection pass uses readiness policy wording, failure continues remediation. No Head relay or technician assignment. Confirmation actions use modal; failures preserve confirmation form. Create sends quantity, impact and cleaning work context.
- `work.tsx`, `rooms.tsx`: room reports link to specialized workflow; cleaning context included from work. Reception can enter equipment report form. `internal-settings.tsx`: room report configuration tab now checks ROOM_SUPPLY_CONFIG.

## Verification

- Frontend build `npm run build`: TypeScript phase and Vite bundling reached success (`1674 modules`, `built in 1m 5s`). Final process status collected separately; build began before last small labels/navigation patches, so integration owner should run final unified build after all workers settle.
- Transpilation followed by `node --check` passed for first integration snapshot (app, requests, room issues). Final six-module syntax check started; process 92150 must be collected if still pending at handoff.
- No backend, SQL, Production, server startup, commits or deployment performed.
- Browser/API runtime validation: NOT_RUN. Parent owns isolated database/server integration and E2E.

## Remaining integration limits

- Actual backend DTO currently returns handler/user IDs without display names. UI visibly identifies `Nhân viên <id>` instead of inventing names; parent can add authorized display-name DTOs.
- Inbox API orders newest first and has no sort parameter. UI preserves server order; no misleading global client sorting. Add backend sort contract if needed.
- Legacy incident projections only identify INCIDENT and room; special room issue links assume room workflow. Verify legacy generic incidents and add explicit discriminator before claiming every historical incident deep link supported.
- Existing supply pages retain always-visible filters from previous worker; requests inbox filters are collapsed. Parent should reconcile supply filter UX in final polish.
- Request room domain transitions must be attached by backend integration to appear in the structured timeline; UI does not fabricate history.
- Runtime RBAC, retry, physical posting and mobile screenshots still require actual isolated E2E.

No unresolved business questions for this frontend module.
