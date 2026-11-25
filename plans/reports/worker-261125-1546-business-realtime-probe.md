# Business-triggered realtime probe

Implemented verification helper and opt-in integration only; no production source changes.

- `scripts/verification/v2-business-realtime-probe.mjs`: real destination OM TRANSFER_REQUEST submission using the existing two-unit statistical suggestion, source OM approval, four live Edge pages (OM/SK in both branches), persisted notification ownership, distinct per-recipient IDs, actual SignalR LongPolling, automatic list/status/badge/popup refresh, stale retry deduplication, own/foreign read checks.
- `scripts/verification/v2-transfer-runtime.mjs`: runs helper only with `HOTELOPS_BUSINESS_REALTIME_PROBE=1`. Retains final incoming-commitment exclusion assertion. Confirms approval does not ship physical batches. Default path preserved.
- Result artifact: `artifacts/restructure-v2/business-realtime-results.json`; four screenshots named `business-realtime-{role}.png`.

Both scripts pass `node --check`. No DB fixture or browser executed by this worker: parent owns fixture port 5779. Runtime assertions are pending actual execution, not PASS evidence.

Parent command after its current fixture cleanup, fresh required backup, latest binary selection, and Vite 5174 available:

```powershell
$env:HOTELOPS_BUSINESS_REALTIME_PROBE='1'
node scripts/verification/v2-transfer-runtime.mjs
```

Retain parent's `HOTELOPS_V2_TEST_BUILD` setting. Runner owns setup/start/stop/cleanup. No credentials are logged or embedded; sessions stay in private existing fixture metadata. No SQL-injected notification is used.

Limitations: advice reviewed through preceding existing advisory browser; submit/approve actions use actual HTTP, with four independent live browsers proving updates. Existing fixture sessions rather than password login. Statistical fallback explicitly remains non-AI; external provider is not exercised. Physical shipment remains the earlier transfer tests' responsibility. Negative dedup uses a bounded 6.5-second observation, not an SLA claim.

Unresolved questions: none; runtime results remain pending parent execution.
