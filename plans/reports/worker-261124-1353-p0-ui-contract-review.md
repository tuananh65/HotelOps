# P0 frontend contract review

## Changes

- `frontend/src/compact/requests.tsx`: translates typed room request types/states; specialized domain actions stay in the room workflow and generic request commands are limited to claim/release. Child requests open their parent room issue. Historical unstructured INCIDENT opens the generic incident detail rather than assuming a room issue snapshot exists. Refill completion uses the API's fulfilled state rather than displaying a misleading zero receipt quantity from the warehouse receipt DTO.
- `frontend/src/compact/room-issues.tsx`: reads request capabilities and current handler. Domain submission buttons are disabled unless backend grants claim/release authority for the current queue/shift; actual domain validation remains server-side.
- `frontend/src/compact/staff-supplies.tsx`, `supply-configuration.tsx`: filters initially collapsed with an explicit Reset. Existing responsive stacked tables and wrapping retained.
- `frontend/src/compact/p0-error-messages.ts`, `api.ts`: shared Vietnamese messages for supply shortage/reservation, shift closure blockers, request routing/concurrency, classification, approval and fulfillment errors.

## Verified / limits

- `workday.tsx` already consumes `active.canEnd` both to display the End button and disable the confirmation; dashboard attendance summary links to this workflow without offering an unchecked end action. No changes needed there.
- Six changed modules transpiled with TypeScript and passed `node --check`. No authenticated API/browser test was available to this worker.
- First frontend build session 49635 failed on missing `State.children` in room issue error rendering. Fixed explicitly with null children; final rebuild started. Final request/room modules rechecked after the last edits.
- No backend, database, server, production, git commit or deployment changes.

## Integration observations

- Room object options endpoint still returns all assigned assets/items without configuration group metadata, while create correctly validates active B/C classification. UI reports a precise classification error; filtering options requires backend metadata rather than inventing groups.
- Actor display names remain absent from request DTO; IDs are visibly identified as employee IDs, not fabricated names.
- A room workflow without a migrated structured header is now read-only in this UI because capability lookup fails. Migration must attach headers to legacy room issues before enabling processing.
- Current supplier request receipt counts do not represent staff refill quantities. UI avoids treating that field as staff delivery truth.

Unresolved business questions: none.
