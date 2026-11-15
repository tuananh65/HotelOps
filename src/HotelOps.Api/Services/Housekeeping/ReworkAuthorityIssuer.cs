using HotelOps.Api.Services.Housekeeping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    private async Task<bool> ReworkAuthorityIssuerAvailable(CancellationToken ct)
    {
        // No implicit fallback to a synthetic issuer or direct INSERT into the proof table.
        if (await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN OBJECT_ID('dbo.fe04_rework_authority_issuers','U') IS NOT NULL
              AND OBJECT_ID('dbo.fe05_issue_rework_authority','P') IS NOT NULL
              AND OBJECT_ID('dbo.fs016_rework_source_proofs','U') IS NOT NULL
              AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FE04.ReworkAuthority.V1'
                AND CONVERT(nvarchar(128),value)='FE05_COMMITTED_V1')
              AND HAS_PERMS_BY_NAME('dbo.fe05_issue_rework_authority','OBJECT','EXECUTE')=1
              THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) != 1) return false;
        return await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS Value FROM dbo.fe04_rework_authority_issuers x
            JOIN sys.sql_modules m ON m.object_id=x.writer_object_id
            WHERE x.issuer='FE05.UC045.UC046' AND x.version=1 AND x.is_active=1
              AND x.writer_object_id=OBJECT_ID('dbo.fe05_issue_rework_authority')
              AND x.source_commit LIKE REPLICATE('[0-9a-f]',40) AND m.execute_as_principal_id=-2
              AND HASHBYTES('SHA2_256',CONVERT(varbinary(max),m.definition))=x.module_digest
            """).SingleAsync(ct) == 1;
    }

    internal async Task<Guid> IssueReworkAuthorityInCallerTransactionAsync(long actor, long branch, long rework,
        string? evidenceRoot, CancellationToken ct)
    {
        var tx = db.Database.CurrentTransaction ?? throw new InvalidOperationException("REWORK_CALLER_TRANSACTION_REQUIRED");
        RequireAuthorityTransaction(db.Database.GetDbConnection(), tx.GetDbTransaction());
        if (!await ReworkAuthorityIssuerAvailable(ct)) throw new InvalidOperationException("REWORK_ISSUER_UNAVAILABLE");
        var snapshot = await BuildReworkAuthoritySnapshotAsync(db.Database.GetDbConnection(), tx.GetDbTransaction(), rework, evidenceRoot, ct);
        if (snapshot == null || snapshot.Binding.BranchId != branch || !snapshot.CurrentFailedSource
            || snapshot.CurrentCycleId != snapshot.Binding.CleaningTaskId)
            throw new InvalidOperationException("REWORK_AUTHORITY_SOURCE_INVALID");
        var operation = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            EXEC dbo.fe05_issue_rework_authority @actor={actor},@branch={branch},@rework={rework},@operation={operation},
                @source_digest={snapshot.SourceDigest},@evidence_digest={snapshot.EvidenceDigest};
            """, ct);
        return operation;
    }
}
