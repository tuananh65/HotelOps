using System.Data;
using Microsoft.Data.SqlClient;

namespace HotelOps.Schema002;

public sealed record PublicationProbe(string CaseId,string OwnedFixtureId,string RowsJson,int? ExpectedSqlError);
public sealed record ProbeReceipt(string CaseId,string FixtureId,string Status,int? SqlError,bool RolledBack,bool Preserved);

public sealed partial class Schema002Executor
{
    // Fixture/row hashes must be included in the real authorization's approved probe plan.
    // Each case has a separate transaction because trigger THROW can doom its transaction.
    public async Task<ProbeReceipt> ProbeAsync(ExecutionScope scope,PublicationProbe probe,
        string artifactPath,string approvedArtifactHash,string installReceiptPath,string approvedInstallReceiptHash,CancellationToken ct)
    {
        if(probe.ExpectedSqlError is 2601 or 2627 || probe.CaseId is "n01-duplicate-event" or "legacy-duplicate-assignment" or "legacy-cross-family-duplicate")
            throw new InvalidOperationException("EXPLICIT_NAMED_INDEX_CONTRACT_REQUIRED_USE_ProbeNamedIndexAsync");
        if(!scope.OwnedFixtureIds.Contains(probe.OwnedFixtureId)||string.IsNullOrWhiteSpace(probe.CaseId)||
           (probe.ExpectedSqlError.HasValue && probe.ExpectedSqlError is not (51915 or 51962 or 547 or 2601 or 2627)))
            throw new InvalidOperationException("OWNED_PROBE_PLAN_REQUIRED");
        var planHash=Hashes.Utf8(System.Text.Json.JsonSerializer.Serialize(probe));
        var artifact=await ImmutableArtifacts.ReadApprovedAsync<RestoreArtifact>(artifactPath,approvedArtifactHash,ct);
        var receipt=await ImmutableArtifacts.ReadApprovedAsync<InstallationReceipt>(installReceiptPath,approvedInstallReceiptHash,ct);
        CatalogProtocol.RequireArtifact(artifact,scope);
        if(receipt.Contract!="SCHEMA002_INSTALL_RECEIPT_V1"||receipt.State!="COMMITTED_CATALOG_REREAD"||receipt.ArtifactHash!=approvedArtifactHash||receipt.SourceBundleHash!=scope.SourceBundleHash)
            throw new InvalidOperationException("COMMITTED_INSTALL_RECEIPT_REQUIRED");
        await authorization.RequireAsync(scope,"PROBE_PLAN:"+probe.CaseId,planHash,approvedInstallReceiptHash,ct);
        await using var c=await OpenAsync(scope,"PROBE:"+probe.CaseId,approvedArtifactHash,approvedInstallReceiptHash,ct);
        await using var tx=await c.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var before=await PreservationAsync(c,tx,ct);int? actual=null;
        try
        {
            var installed=await CaptureCatalogAsync(c,tx,ct);
            if(installed.CatalogHash!=Hashes.SqlUnicode(receipt.InstalledCatalogJson)||installed.TriggerHash!=receipt.InstalledTriggerHash||
               installed.TriggerHash!=Hashes.SqlUnicode(await File.ReadAllTextAsync(Path.Combine(sqlDirectory,"09_PublicationSourceTrigger.sql"),ct)))
                throw new InvalidOperationException("EXACT_INSTALLED_PROBE_CATALOG_REQUIRED");
            await using var command=c.CreateCommand();command.Transaction=tx;
            command.CommandText=await File.ReadAllTextAsync(Path.Combine(sqlDirectory,"12_PublicationProbe.sql"),ct);
            var p=command.CreateParameter();p.ParameterName="@RowsJson";p.DbType=DbType.String;p.Size=-1;p.Value=probe.RowsJson;command.Parameters.Add(p);
            try{await command.ExecuteNonQueryAsync(ct);}
            catch(SqlException e)when(e.Number!=1205){actual=e.Number;}
        }
        finally{await tx.RollbackAsync(CancellationToken.None);}
        if(actual!=probe.ExpectedSqlError)throw new InvalidOperationException($"PROBE_FAILED:{probe.CaseId}: expected {probe.ExpectedSqlError}, actual {actual}");
        // Reacquire preserved catalog/data under the same externally held exclusive lease.
        await authorization.RequireAsync(scope,"PROBE_PRESERVATION:"+probe.CaseId,planHash,null,ct);
        await using var verify=await c.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        CatalogProtocol.RequirePreserved(before,await PreservationAsync(c,verify,ct));
        await verify.RollbackAsync(ct);
        return new(probe.CaseId,probe.OwnedFixtureId,"PASS",actual,true,true);
    }
}
