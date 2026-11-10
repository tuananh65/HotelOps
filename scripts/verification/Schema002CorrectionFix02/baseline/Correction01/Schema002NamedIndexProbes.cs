using System.Data;
using System.Text.Json;
using HotelOps.Sql09;

namespace HotelOps.Schema002;

public sealed record NamedIndexProbeReceipt(ProbeReceipt Original,NamedIndexOutcome Evidence,
    IReadOnlyList<ProbeReceipt> SingleRowSourceChecks,string Validation);

// Proposed partial extension, compiled against original executor helpers. No alternate graph.
public sealed partial class Schema002Executor
{
    public async Task<NamedIndexProbeReceipt> ProbeNamedIndexAsync(ExecutionScope scope,PublicationProbe probe,
        NamedIndexContract expected,string captureScriptPath,string artifactPath,string approvedArtifactHash,
        string installReceiptPath,string approvedInstallReceiptHash,CancellationToken ct)
    {
        NamedIndexEvidence.RequireContract(expected);
        if(probe.CaseId!=expected.ProbeId||probe.OwnedFixtureId!=expected.FixtureId||!scope.OwnedFixtureIds.Contains(expected.FixtureId)||probe.ExpectedSqlError!=expected.ExpectedErrorNumber||
           !StringComparer.OrdinalIgnoreCase.Equals(Hashes.Utf8(probe.RowsJson),expected.RowsSha256))throw new InvalidOperationException("EXACT_NAMED_INDEX_FIXTURE_BINDING_REQUIRED");
        var individualRows=NamedIndexEvidence.RequireFixture(probe.CaseId,probe.RowsJson);
        var captureBytes=await File.ReadAllBytesAsync(captureScriptPath,ct);
        if(!StringComparer.OrdinalIgnoreCase.Equals(Hashes.Bytes(captureBytes),expected.CaptureSqlSha256))throw new InvalidOperationException("CAPTURE_SCRIPT_HASH_MISMATCH");
        var planHash=Hashes.Utf8(JsonSerializer.Serialize(new{expected,probe}));
        await authorization.RequireAsync(scope,"NAMED_INDEX_PLAN:"+probe.CaseId,planHash,approvedInstallReceiptHash,ct);
        // Each row must pass real FK/CHECK/source trigger independently. A duplicate index
        // can reject before a trigger runs; index failure alone cannot prove valid sources.
        // Same accepted exclusive lease/no competing writers must span every phase.
        var checkedRows=new List<ProbeReceipt>();
        for(var i=0;i<individualRows.Length;i++)
        {
            await authorization.RequireAsync(scope,"NAMED_INDEX_SOURCE_PHASE:"+probe.CaseId+":"+i,planHash,approvedInstallReceiptHash,ct);
            checkedRows.Add(await ProbeAsync(scope,probe with{CaseId="named-index-source-phase:"+probe.CaseId+":"+i,RowsJson=individualRows[i],ExpectedSqlError=null},
                artifactPath,approvedArtifactHash,installReceiptPath,approvedInstallReceiptHash,ct));
        }
        var artifact=await ImmutableArtifacts.ReadApprovedAsync<RestoreArtifact>(artifactPath,approvedArtifactHash,ct);
        var installedReceipt=await ImmutableArtifacts.ReadApprovedAsync<InstallationReceipt>(installReceiptPath,approvedInstallReceiptHash,ct);
        CatalogProtocol.RequireArtifact(artifact,scope);
        if(installedReceipt.Contract!="SCHEMA002_INSTALL_RECEIPT_V1"||installedReceipt.State!="COMMITTED_CATALOG_REREAD"||installedReceipt.ArtifactHash!=approvedArtifactHash||installedReceipt.SourceBundleHash!=scope.SourceBundleHash)
            throw new InvalidOperationException("COMMITTED_INSTALL_RECEIPT_REQUIRED");
        await authorization.RequireAsync(scope,"NAMED_INDEX_BATCH:"+probe.CaseId,planHash,approvedInstallReceiptHash,ct);
        await using var c=await OpenAsync(scope,"NAMED_INDEX_BATCH:"+probe.CaseId,approvedArtifactHash,approvedInstallReceiptHash,ct);
        await using var tx=await c.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var before=await PreservationAsync(c,tx,ct);NamedIndexCapture? capture=null;var rolledBack=false;Exception? commandFailure=null;
        try
        {
            var installed=await CaptureCatalogAsync(c,tx,ct);
            if(installed.CatalogHash!=Hashes.SqlUnicode(installedReceipt.InstalledCatalogJson)||installed.TriggerHash!=installedReceipt.InstalledTriggerHash||
               installed.TriggerHash!=Hashes.SqlUnicode(await File.ReadAllTextAsync(Path.Combine(sqlDirectory,"09_PublicationSourceTrigger.sql"),ct)))
                throw new InvalidOperationException("EXACT_INSTALLED_NAMED_INDEX_CATALOG_REQUIRED");
            await using var command=c.CreateCommand();command.Transaction=tx;command.CommandText=System.Text.Encoding.UTF8.GetString(captureBytes);
            void Parameter(string name,string value){var p=command.CreateParameter();p.ParameterName=name;p.DbType=DbType.String;p.Size=-1;p.Value=value;command.Parameters.Add(p);}
            Parameter("@RowsJson",probe.RowsJson);Parameter("@ProbeId",probe.CaseId);Parameter("@ExpectedTargetTable",expected.TargetTable);Parameter("@ExpectedIndexName",expected.ExpectedIndexName);
            await using var reader=await command.ExecuteReaderAsync(ct);
            if(!await reader.ReadAsync(ct))throw new InvalidOperationException("SQL_ERROR_CAPTURE_MISSING");
            string S(string n)=>reader.GetString(reader.GetOrdinal(n));
            int? N(string n)=>reader.IsDBNull(reader.GetOrdinal(n))?null:reader.GetInt32(reader.GetOrdinal(n));
            capture=new(S("ProbeId"),S("TargetTable"),S("ExpectedIndexName"),S("ExecutionResult"),N("ErrorNumber"),
                reader.IsDBNull(reader.GetOrdinal("ErrorMessage"))?null:S("ErrorMessage"),reader.IsDBNull(reader.GetOrdinal("ErrorProcedure"))?null:S("ErrorProcedure"),
                N("ErrorLine"),N("XactStateAtCatch")??throw new InvalidOperationException("SQL_STATE_MISSING"),N("TransactionsAtCatch")??throw new InvalidOperationException("SQL_TRANSACTION_COUNT_MISSING"));
            if(await reader.ReadAsync(ct)||await reader.NextResultAsync(ct))throw new InvalidOperationException("AMBIGUOUS_EXTRA_SQL_RESULTS");
        }
        catch(Exception error){commandFailure=error;throw;}
        finally
        {
            // Doomed (-1) transactions must be fully rolled back; no savepoint/commit.
            // Deadlock auto-abort or rollback failure never yields a successful receipt.
            try{if(tx.Connection is not null){await tx.RollbackAsync(CancellationToken.None);rolledBack=true;}}
            catch(Exception cleanup)
            {
                if(commandFailure is null){cleanup.Data["NamedIndexCaptureJson"]=JsonSerializer.Serialize(capture);throw;}
                commandFailure.Data["RollbackFailure"]=cleanup.ToString();
            }
        }
        await using var state=c.CreateCommand();state.CommandText="SELECT @@TRANCOUNT";
        var transactionsAfter=Convert.ToInt32(await state.ExecuteScalarAsync(ct));
        if(!rolledBack||transactionsAfter!=0)
        {var error=new InvalidOperationException("NAMED_INDEX_ROLLBACK_NOT_CONFIRMED; SQL_ERROR="+capture?.ErrorNumber);error.Data["NamedIndexCaptureJson"]=JsonSerializer.Serialize(capture);throw error;}
        await authorization.RequireAsync(scope,"NAMED_INDEX_PRESERVATION:"+probe.CaseId,planHash,approvedInstallReceiptHash,ct);
        await using var verify=await c.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var after=await PreservationAsync(c,verify,ct);CatalogProtocol.RequirePreserved(before,after);
        await verify.RollbackAsync(ct);
        var observed=capture??throw new InvalidOperationException("SQL_ERROR_CAPTURE_MISSING");
        var providerErrors=observed.ErrorNumber.HasValue&&observed.ErrorMessage is not null?new[]{new ProviderError(observed.ErrorNumber.Value,observed.ErrorMessage)}:[];
        var evidence=new NamedIndexOutcome(observed,providerErrors,rolledBack,transactionsAfter,
            Hashes.Utf8(JsonSerializer.Serialize(before)),Hashes.Utf8(JsonSerializer.Serialize(after)),expected.FixtureOwnershipReceiptSha256);
        try{NamedIndexEvidence.Require(expected,evidence);}
        catch(Exception error){error.Data["NamedIndexOutcomeJson"]=JsonSerializer.Serialize(evidence);throw;}
        return new(new(probe.CaseId,probe.OwnedFixtureId,"PASS",observed.ErrorNumber,true,true),evidence,checkedRows,"EXACT_NAMED_INDEX_TABLE_SCENARIO_ROLLBACK_VERIFIED");
    }
}
