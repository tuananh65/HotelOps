# Release limitations

- Only additive new STANDARD/nonprimary membership is supported. Remove, regrant, Transfer, Department replacement, primary assignment changes and Deactivate are not opened.
- Destination UserRole rows are conservatively refused even if dormant. OPERATIONS_MANAGER expansion is refused when safety cannot be proved. Future role writers need their own approved authorization/cardinality enforcement.
- Real network response loss is **NOT VERIFIED**. Injected postcommit failure and stale retry are verified separately; no response replay, idempotency key or recovery workflow is introduced.
- SQL locks may contend or deadlock under broader traffic. The tested schedules are evidence for the reviewed race cases, not a load/availability certification.
- Backend depends on the existing SQL User rowversion patch. This commit adds no migration and is not approval to apply a production schema patch or deploy.
- Reauthentication remains subject to the existing process-memory proof limitation and lacks shared multi-instance proof storage. Add Membership does not change that infrastructure. Aggregate token advancement can invalidate target proof without revoking normal sessions.
- DEV2 approval of future remove/move/open-work/deactivation protocols is still separate. No DEV2 source or catalog/schema mapping is changed here.
- Solution `dotnet test` has no framework test cases; executable verification harnesses provide the reported checks. Core authorization regressions retain their documented doubles, supplemented by real SQL/HTTPS here.
- Earlier Task 38 rebuilds of two legacy harnesses reported NU1900 because NuGet vulnerability metadata was inaccessible. Passing tests do not represent a completed dependency vulnerability scan.
- Local runtime receipts, fixture data and unrelated untracked artifacts remain local and preserved. No HotelOpsDB writes, Push, Merge, PR, rebase or reset are authorized.
