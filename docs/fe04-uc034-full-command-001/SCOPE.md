# FE04-UC034-FULL-COMMAND-001 scope

Authorization: Project Leader directive supplied on 2026-11-11.
Branch: `feature/dev1-fe04-uc034-full-command`.
Exact base: `95f7422e284bc81bcfcf9f460dace52022de5cc7`.
Required ancestor: `d37e99f42eaaf3089eb510c6298296a0f0743faa` (verified).

Source edit allowlist recorded before editing:
- `src/HotelOps.Api/Services/Housekeeping/CleaningClosureBoundary.cs`
- `tests/HotelOps.Dev1.RegressionTests/CleaningCompletionCommandTests.cs`
- `docs/fe04-uc034-full-command-001/SCOPE.md`
- `docs/fe04-uc034-full-command-001/RESULT.md`
- `docs/fe04-uc034-full-command-001/source-hashes.json`

Reuse R05 completion/capture/reader; no replacement of Notification or Work-Time.
Focused no-SQL verification only. SQL mutation / HTTPS business acceptance:
NOT_RUN (no approved target, execution identity, exclusive lease or fixtures).
No gates, EF entities/mappings, schema, Program.cs, FE05 writers, Notification
infrastructure, main or PR #40 source changes. No merge or activation.

Review finding: reject incomplete/unsupported/corrupt durable replay receipts
without returning success or leaking JSON parse exceptions through the API.
Preserve fingerprint conflicts, mandatory staging verification and outer transaction.