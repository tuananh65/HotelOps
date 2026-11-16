# UC049 similar Incident review

2026-11-16. PARTIAL_IMPLEMENTATION; no new completed UC.

Follow-up: scoped review responses now carry a Data Protection receipt bound to current user, session, branch and exact room/area/asset selection, expiring after ten minutes. Creation validates the receipt before data access/writes, after existing command admission. UI confirmation forwards it; missing/foreign/expired/location-mismatched receipts fail closed. Similar review rejects unrelated narrowing filters. Existing clients, including demo clients, must obtain a receipt through the admitted scoped list and pass it on creation. This is proof of a scoped response, not proof of human attention, a snapshot/concurrency lock, mutation permission or an idempotency key. No extra grants or read access are introduced.

Follow-up verification: backend build PASS (zero warnings/errors); frontend TypeScript/Vite build PASS (existing warnings); five focused backend receipt/location tests and three frontend transport tests PASS. No SQL/HTTPS/browser acceptance run. Node test loader deprecation remains. Uses the existing authentication Data Protection provider/key configuration; no database or configuration changes.

GBR-067 source increment: the existing scoped Incident list accepts SimilarOpenOnly. It requires a positive branch and at least one location; matches any selected room, area or asset, while normal list filters retain intersection semantics. It composes on the authorization-filtered population. Physical OPEN, IN_PROGRESS and RESOLVED are included; CLOSED and CANCELLED are excluded. This does not resolve the pending mapping of the full SRS Incident lifecycle.

The canonical creation form displays a paginated review with links to existing visible Incidents and requires confirmation before sending creation. Changing location, reloading or changing pages clears the review. Failures never appear as an empty successful search. The existing current-session/action admission remains unchanged. No permission, role, grant or database changes.

Direct verification: `dotnet build HotelOps.slnx --no-restore` PASS, zero warnings/errors; frontend TypeScript/Vite build PASS with existing React Router directives and chunk-size warnings. Three focused IncidentLocationQueryTests PASS: any-location matching/terminal exclusion/current population preservation, normal-filter intersection, and invalid lookup rejection. These are in-memory query tests; no new SQL/HTTPS/browser acceptance result.

Remaining: production creation admission; full Incident lifecycle mapping; evidence upload/append; transactional notifications. Review does not prove absence of records outside the caller's independent INCIDENT_VIEW scope or prevent new matches after review. UC049 and dependent UC118 remain partial. Source-complete/QA counts remain 41/144 and 27/144 historical respectively.
