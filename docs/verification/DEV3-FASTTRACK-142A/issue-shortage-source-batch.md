# FE09 issue and replenishment handoff source batch

2026-11-16. UC097/098 IMPLEMENTED_PENDING_QA; no new COMPLETE_VERIFIED.

- Existing MaterialIssueContract requires HTTPS/current SQL session, STOREKEEPER, independent MR_VIEW, MR_FULFILL, STOCK_ISSUE_POST and INVENTORY_VIEW with branch scope. Issue commits stock, request status and audit atomically.
- Fulfillment notification now includes issued quantities per request line/UOM and current unissued remainder explicitly timestamped at delivery. Durable committed event, recipient revalidation, transaction and deduplication remain unchanged. Earlier notifications are not rewritten.
- Partial/full issue shortage UI links to replenishment Create with branch/warehouse/request hints. The destination reloads authorized shortages and rechecks create authority. Hints do not select lines or authorize writes.
- SourceRequestId filters only after scoped aggregate demand calculation, before pagination. Empty/foreign requests expose no additional rows.
- UC100 remains PARTIAL: alternate-unit adjustment from SRS step 3 is not implemented (current form uses base units).

Checks: dotnet build HotelOps.slnx --no-restore PASS, 0 warnings. Frontend production build PASS; explicit typecheck PASS. Existing React Router directive and large bundle warnings remain. No automated tests, HTTP, SQL or browser acceptance run in this batch; deferred under code-first directive. No schema/data changes. Notification worker remains opt-in, capability and current grants required.

Human QA: full/partial issue and rollback; notification batch quantities versus delivery-time remainder after later issues; retries/revocation; branch/warehouse/request navigation, pagination, empty/foreign request, duplicate coverage; ensure client hints cannot grant access.
