# Business query transport correction

2026-11-16. Source bug found during UC092 implementation. No new completed UC or runtime acceptance claim.

createHttpClient previously validated the entire URL against a path-only regex. Six canonical FE09/10 callers pass queries directly: MaterialCatalogFields, MaterialApproverField, MaterialReviewHistory, ReplenishmentCreateFields, ReplenishmentReceiptFields and ReplenishmentPostFields. Those calls failed locally before fetch. The separate Dev2 API wrapper appended its query after validation, so its older tests did not cover these direct callers. Prior builds could not prove these flows worked.

The shared client now validates the path independently with its unchanged strict /api/ route restrictions, then validates query syntax, percent encoding, UTF-8/control characters and unique case-insensitive scalar parameter names. Query values remain separate from path/origin resolution. Cookies, no-store, redirect rejection, cancellation, fresh-token requirements and error handling remain unchanged.

Checks executed: node --test --test-concurrency=1 tests/auth-http.test.mjs tests/dev2-api.test.mjs: 69 PASS, 0 failed/skipped. Added positive tests for actual business query routes and negative cases for absolute/protocol-relative paths, encoded traversal/separators, fragments, malformed encoding, controls, duplicate keys and missing mutation CSRF. npm run build (TypeScript + Vite) PASS. Existing React Router/bundle warnings; test runner module.register deprecation warning. Backend unchanged; no SQL/database mutation. Browser/HTTPS/SQL acceptance remains deferred to Human QA.

Counters remain source 39/144 and historical QA 27/144 after repairing this blocker. Do not use these unit tests as whole-UC verification. Resume UC092 typed linking work next.
