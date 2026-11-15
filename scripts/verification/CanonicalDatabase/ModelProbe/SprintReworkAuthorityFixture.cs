using System.Diagnostics;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

internal static class SprintReworkAuthorityFixture
{
    internal static async Task InstallAsync(HotelOpsDbContext db,CancellationToken ct)
    {
        var start=new ProcessStartInfo("git"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true};
        start.ArgumentList.Add("rev-parse");start.ArgumentList.Add("HEAD");
        using var git=Process.Start(start)??throw new Exception("Source identity unavailable");
        var commit=(await git.StandardOutput.ReadToEndAsync(ct)).Trim();await git.WaitForExitAsync(ct);
        if(git.ExitCode!=0 || commit.Length!=40)throw new Exception("Source identity invalid");
        await db.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_set_session_context @key=N'DEV3.SourceCommit',@value={commit}",ct);
        await db.Database.ExecuteSqlRawAsync(await File.ReadAllTextAsync("scripts/verification/CanonicalDatabase/ModelProbe/SprintReworkAuthority.sql",ct),ct);
    }

    // The parent fixture rehearses this cleanup in a rollback transaction before committing setup.
    internal static async Task CleanupAsync(HotelOpsDbContext db,long inspection,CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            IF @@TRANCOUNT<>1 OR DB_NAME()<>'HotelOps_DEV3_142A_20261114_cc78c13f'
              OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
              OR EXISTS(SELECT 1 FROM dbo.fs016_rework_source_proofs WHERE inspection_id<>{inspection})
              THROW 51996,'Owned authority cleanup only',1;
            SELECT actor_user_id,operation_key INTO #owned_authority_operations FROM dbo.fs016_rework_source_proofs;
            DROP PROCEDURE dbo.fe05_issue_rework_authority;
            DROP TABLE dbo.fs016_rework_source_proofs;
            DROP TABLE dbo.fe04_rework_authority_issuers;
            DROP ROLE hotelops_rework_writer;
            EXEC sys.sp_dropextendedproperty @name=N'HotelOps.FE04.ReworkAuthority.V1';
            DELETE o FROM dbo.notification_operation_reservations o JOIN #owned_authority_operations p
              ON p.actor_user_id=o.actor_user_id AND p.operation_key=o.operation_key;
            DROP TABLE #owned_authority_operations;
            """,ct);
    }
}
