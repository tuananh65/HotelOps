# Independent review — request workspace and transfer progress

Date: 25 November 2026. Read-only source review; no database changes, servers, or implementation edits. Scope: new domain request integration, replacement queue projection, reusable inventory entry point and transfer progress. This is not a full acceptance verdict.

## Findings

### P1 — Older pending write-offs disappear before queue/search filtering

- `src/HotelOps.Api/Compact/Services/ReusableInventoryService.cs:103` orders all branch write-offs descending and takes only 200, without pagination metadata or a continuation mechanism.
- `frontend/src/compact/use-domain-requests.ts:12` loads that truncated array, then applies `mine`, `action`, search and status filters at lines 34–37. The dedicated write-off tab uses the same endpoint.
- Reproduction: keep an older write-off PENDING; create 200 newer proposals/decisions. The older actionable item is absent from both the inbox and write-off tab. Displayed totals imply completeness, and changing filters cannot recover it.
- Recommendation: paginate the authorized endpoint and consume all pages, or apply queue filters before proper server pagination. Cover more than 200 records with a pending record outside the first page. The separate history endpoint also silently caps at 500 (`:49`).

### P2 — Live notification does not refresh the new write-off rows

- `frontend/src/compact/ui.tsx:8` subscribes selected query paths to `compact:notification-change`, but excludes `reusable-inventory`.
- Both `use-domain-requests.ts:12` and `reusable-inventory.tsx` use that excluded path. A new write-off notification can arrive while the OM's action queue remains empty until a manual navigation/reload. An external approval likewise leaves an old pending action visible.
- `frontend/src/compact/use-paged-rows.ts:7` only depends on path/revision and has no notification subscription, so the normal paginated inbox and transfer list similarly remain stale even when detail queries refresh.
- Recommendation: share an appropriate notification invalidation mechanism across both hooks; scope it and avoid clearing active forms. Test two actors/browsers with an already-open queue, not only navigation after creation.

### P2 — Replacement detail exposes decisions the queue/backend already forbid

- New `WorkService.Replacement.cs:32–34` correctly excludes terminal work and the original proposer from `canDecide`.
- `ReplacementDetail` at `:53` omits both conditions. `asset-replacement.tsx` renders approval/rejection buttons directly from this detail flag. The mutation later rejects terminal work (`:66`) or a proposer approving their own request (`:91–93`).
- Reproduction: user holds proposer and OM roles, another supervisor confirms; the common queue correctly omits the item from “Cần tôi xử lý”, but opening it from “Tất cả” exposes unusable approval buttons. A confirmed proposal on terminal work has the same inconsistency.
- Recommendation: reuse one decision eligibility predicate for queue, detail and mutation. This is a misleading UI/contract issue, not a demonstrated authorization bypass; mutation checks remain intact.

## Transfer and authorization assessment

- `TransferProgress` aggregates base-unit stock movements across every posted child shipment linked to the request line, and every posted receipt linked to those shipment lines, then divides by the displayed line's conversion factor. Multiple shipments/receipts and differing document units are accounted for structurally; the new projection does not change stock or ownership.
- Reversed original movements are excluded. Cross-branch reversal is explicitly disallowed by the existing posting path, avoiding an unsupported return masquerading as receipt. Pending quantity is `max(0, shipped - received)` and remains read-only, consistent with D12.
- `TransferContext` checks `CanReadTransfer` before exposing progress. The exception is limited to participating source/destination branches with OM/Storekeeper role and INVENTORY_VIEW. Replacement queue requires branch-scoped OM + ASSET_MANAGE. Write-off reads and decisions independently enforce branch-scoped role/permission. No new cross-branch read or write bypass was found in this scope.
- `app.tsx:611` keys the main subtree by active branch, so a branch switch remounts request detail state; a hypothesized retained selection across branches is not a finding.
- Quantities currently omit unit labels in `transfer-progress.tsx`. Adding the line unit would make converted-unit reconciliation understandable, particularly when the same item appears on multiple lines with different units.

## Verification and limitations

- Source inspection only; parent reports a 43-case HTTP/SQL/browser probe passing. That result was not independently rerun here.
- No database queries, mutation, builds or server lifecycle changes were performed for this review. TypeScript/C# compilation remains the parent's build evidence.
- Required additional coverage: multiple shipments with multiple receipts and different conversion factors; more than 200 write-offs; two-browser notification invalidation; proposer/terminal detail flags.
- Unresolved business questions: none. D12 is treated as confirmed.
