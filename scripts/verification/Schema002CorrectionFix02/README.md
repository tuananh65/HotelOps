# Schema002 Fix02 — review-only candidate, owner pending

This candidate supersedes Correction01 evidence flow. It never changes or activates frozen Schema002 nineteen files, DECISION08, or ENV001's different assignment-only index variant.

ActualEvidence.cs separates expected hash, actual raw signed fixture receipt, verification result and provenance. The rich validator requires externally accepted IActualEvidenceTrust. No production trust implementation, owner key, owner receipt or provider observer is shipped. Default absent source/trust fails closed. OfflineEvidence is compiled only in the explicit NoSQL runner; ephemeral unit-test keys have no owner authority.

The actual fixture packet must bind contract, issuer, receipt ID, probe, fixture, table, rows, capture script and expiry. Hashes are computed from supplied bytes. The trust adapter must separately authenticate accepted issuer/key/purpose and owner/lane/lease provenance; supplying a record is not authorization. The executor snapshots the verified fixture envelope before source phases.

Provider packets must come from an independently accepted SqlClient observer, bind this execution ID and capture hash, and contain the actual provider collection. SQL TRY/CATCH returns rows and normally suppresses SqlException.Errors. That source alone cannot satisfy independent provider evidence. No SQL ERROR_NUMBER/MESSAGE conversion is used to construct provider errors. Missing provider evidence blocks acceptance after rollback; missing trusted fixture receipt blocks before SQL. Owner approval of a real capture strategy is still required.

Legacy Require(string,IReadOnlyCollection<ProviderError>) remains SQL09-compatible, but cannot establish rich probe acceptance. All three rich probes require exact 2601/native unique-index/table identity, trusted receipts, actual provider evidence, rollback and preservation. 2627 is rejected for these standalone indexes; 1205 always fails.

Exactly three baseline copies under baseline/Correction01 are affected Correction01 implementation sources: NamedIndexEvidence.cs, Schema002NamedIndexProbes.cs and Schema002Probes.proposed.cs. The three logical cases share these sources; there are not three separate original probe C# files. Numeric-only guard and SQL capture artifact are byte-identical to Correction01. Canonical proposed patch paths are repository-relative and target only this review candidate, never frozen sources automatically.

NoSQL commands (.NET10; pass installed ScriptDom DLL and preserved evidence workspace):

    dotnet run --project scripts/verification/Schema002CorrectionFix02/NamedIndexCorrection.NoSql.csproj -p:HotelOpsRoot=<evidence-workspace> -p:ScriptDomPath=<installed-ScriptDom.dll> -- --offline <evidence-workspace> <candidate-dir>
    dotnet run --project scripts/verification/Schema002CorrectionFix02/SQL09LegacyCompatibility.NoSql.csproj -p:HotelOpsRoot=<evidence-workspace> -p:ScriptDomPath=<installed-ScriptDom.dll> -- --self-test <evidence-workspace>/.n01sql09

Preserved compiler/test inputs required at evidence-workspace: .fe04schema002/worktree, .n01sql09/consumer, .n01sql09/harness, .n01decision08/source and original manifests. They are intentionally not full snapshots in this handoff. No SQL switch exists in these NoSQL CLIs. Command-only -p:NuGetAudit=false was used for disconnected cached-package restore; vulnerability audit was not performed. Do not bypass execution policy or machine security.

Integration and accepted source/assembly/fixture/target/central lease/catalog approval require separate schema-owner decisions. Review publication never authorizes SQL execution.
