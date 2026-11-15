using System.Data;
using System.Text.RegularExpressions;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

internal static class SprintReinspectionRecovery
{
    internal static async Task<int> RunAsync()
    {
        await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(
            "Server=localhost;Database=HotelOps_DEV3_142A_20261114_cc78c13f;Integrated Security=true;Encrypt=true;TrustServerCertificate=true").Options);
        if (await db.Database.SqlQueryRaw<int>("""
            SELECT CASE WHEN CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))='meita'
              AND DB_NAME()='HotelOps_DEV3_142A_20261114_cc78c13f'
              AND EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
              AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='DEV3.Sprint142A.Run' AND CONVERT(nvarchar(128),value)='cc78c13ffc124efcad67b3d804baa84c')
              AND (SELECT COUNT(*) FROM dbo.users)=2 AND (SELECT COUNT(*) FROM dbo.users WHERE username IN('D3-EVIDENCE','D3-CLEANER'))=2
              AND (SELECT COUNT(*) FROM dbo.branches)=1 AND EXISTS(SELECT 1 FROM dbo.branches WHERE branch_code='D3-EVIDENCE')
              AND (SELECT COUNT(*) FROM dbo.cleaning_tasks)=1 AND (SELECT COUNT(*) FROM dbo.cleaning_reworks)=1
              AND (SELECT COUNT(*) FROM dbo.fe05_reinspection_bindings_v1)=1 THEN 1 ELSE 0 END AS Value
            """).SingleAsync() != 1) throw new Exception("Exact stranded synthetic fixture required");
        var actor = await db.Users.Where(u => u.Username == "D3-EVIDENCE").Select(u => u.UserId).SingleAsync();
        var cleaner = await db.Users.Where(u => u.Username == "D3-CLEANER").Select(u => u.UserId).SingleAsync();
        var branch = await db.Branches.Where(b => b.BranchCode == "D3-EVIDENCE").Select(b => b.BranchId).SingleAsync();
        var inspection = await db.RoomInspections.Where(i => i.InspectionNo == "D3-EVIDENCE" && i.InspectorUserId == actor).Select(i => i.InspectionId).SingleAsync();
        var keys = await db.Attachments.Select(a => a.StorageKey).ToArrayAsync();
        if (keys.Any(k => Path.GetFileName(k) != k || k.Contains(':'))) throw new Exception("Owned evidence names required");
        var schema = await File.ReadAllTextAsync("scripts/database/release/housekeeping/01_Housekeeping.sql");
        var guards = Regex.Split(schema, @"(?m)^GO\s*\r?$").Where(b => b.TrimStart().StartsWith("CREATE TRIGGER dbo.TR_hk_results_v1 ")
            || b.TrimStart().StartsWith("CREATE TRIGGER dbo.TR_hk_defects_v1 ")).ToArray();
        if (guards.Length != 2) throw new Exception("Canonical original guards required");
        async Task Clean()
        {
            foreach (var definition in guards) await db.Database.ExecuteSqlRawAsync(definition.Replace("CREATE TRIGGER", "ALTER TRIGGER"));
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                IF EXISTS(SELECT 1 FROM dbo.fe05_reinspection_bindings_v1 WHERE original_inspection_id<>{inspection} OR branch_id<>{branch})
                  THROW 51999,'Foreign reinspection source',1;
                DECLARE @owned TABLE(id bigint PRIMARY KEY);
                INSERT @owned SELECT inspection_id FROM dbo.fe05_reinspection_bindings_v1;
                UPDATE dbo.room_inspections SET workflow_status='INSPECTING',status='IN_PROGRESS',result=NULL WHERE inspection_id IN(SELECT id FROM @owned);
                DROP TABLE dbo.fe05_reinspection_defect_attachments_v1;
                DROP TABLE dbo.fe05_reinspection_defect_results_v1;
                DROP TABLE dbo.fe05_reinspection_bindings_v1;
                DELETE dbo.audit_logs WHERE entity_type='REINSPECTION' AND TRY_CONVERT(bigint,entity_id) IN(SELECT id FROM @owned);
                DELETE dbo.notification_operation_reservations WHERE actor_user_id={actor} AND JSON_VALUE(result_json,'$.contract') IN('FE05_REINSPECTION_ASSIGNMENT_V1','FE05_REINSPECTION_CONCLUSION_V1');
                DELETE dbo.room_status_history WHERE trigger_type='INSPECTION' AND trigger_id IN(SELECT id FROM @owned);
                DELETE l FROM dbo.inspection_defect_attachments l JOIN dbo.inspection_defects d ON d.defect_id=l.defect_id WHERE d.inspection_id IN(SELECT id FROM @owned);
                DELETE dbo.attachments WHERE entity_type='INSPECTION' AND entity_id IN(SELECT id FROM @owned);
                DELETE dbo.inspection_defects WHERE inspection_id IN(SELECT id FROM @owned);
                DELETE dbo.inspection_results WHERE inspection_id IN(SELECT id FROM @owned);
                DELETE dbo.room_inspections WHERE inspection_id IN(SELECT id FROM @owned);
                DROP PROCEDURE dbo.fe05_bind_reinspection;
                DROP PROCEDURE dbo.fe05_commit_reinspection;
                ALTER ROLE hotelops_reinspection_writer DROP MEMBER DEV3_Reinspector;
                DROP USER DEV3_Reinspector;
                DROP ROLE hotelops_reinspection_writer;
                DELETE dbo.fe04_rework_authority_issuers WHERE issuer IN('FE05.UC041','FE05.UC047');
                EXEC sys.sp_dropextendedproperty @name=N'HotelOps.Reinspection.V1';
                DELETE dbo.fs016_rework_evidence;
                DELETE dbo.attachments WHERE entity_type='CLEANING_TASK' AND uploaded_by={cleaner};
                DELETE dbo.notification_operation_reservations WHERE actor_user_id={cleaner} AND JSON_VALUE(result_json,'$.contract')='FE04_REWORK_SUBMISSION_V1';
                DROP PROCEDURE dbo.fe04_commit_rework_submission;
                DROP ROLE hotelops_rework_submission_writer;
                DELETE dbo.fe04_rework_authority_issuers WHERE issuer='FE04.UC036';
                DROP TRIGGER dbo.TR_rework_evidence_revision_v1;
                DROP TRIGGER dbo.TR_rework_remediation_revision_v1;
                DROP TABLE dbo.fs016_rework_evidence;
                ALTER TABLE dbo.cleaning_rework_defects DROP CONSTRAINT CK_rework_remediation_v1,FK_rework_remediator_v1;
                ALTER TABLE dbo.cleaning_rework_defects DROP COLUMN remediation_note,remediated_by,remediated_at;
                EXEC sys.sp_dropextendedproperty @name=N'HotelOps.ReworkSubmission.V1';
                """);
            await SprintReworkFixture.CleanupAsync(db, cleaner, branch, inspection, CancellationToken.None);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_inspections SET workflow_status='INSPECTING',status='IN_PROGRESS',result=NULL WHERE inspection_id={inspection}; DELETE dbo.inspection_defects WHERE inspection_id={inspection};");
            await SprintNotificationCommitProbe.CleanupAsync(db, actor, cleaner, branch, inspection, CancellationToken.None);
        }
        await using (var rehearsal = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
        { await Clean(); await rehearsal.RollbackAsync(); }
        await using (var cleanup = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
        { await Clean(); await cleanup.CommitAsync(); }
        foreach (var key in keys) File.Delete(Path.Combine(SprintReworkFixture.EvidenceRoot, key));
        Console.WriteLine("PASS exact disposable recovery: rollback rehearsal, original guards restored, synthetic fixture cleaned");
        return 0;
    }
}
