using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Diagnostics;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

internal static class SprintReworkSubmissionProbe
{
    internal static async Task VerifyAsync(HotelOpsDbContext db,SessionReference ticket,long sid,long branch,long id,CancellationToken ct,Func<Task>? browser=null)
    {
        var actor=ticket.UserId;
        var task=await db.CleaningReworks.Where(w=>w.ReworkId==id).Select(w=>w.CleaningTaskId).SingleAsync(ct);
        var initialTag='"'+Convert.ToHexString(await db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.cleaning_reworks WHERE rework_id={id}").SingleAsync(ct))+'"';
        var defects=await db.Database.SqlQuery<long>($"SELECT defect_id AS Value FROM dbo.cleaning_rework_defects WHERE rework_id={id}").ToArrayAsync(ct);
        var png=new EvidenceUpload("remediation.png","image/png","iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aS1cAAAAASUVORK5CYII=");
        var body=new ReworkActionRequest("Synthetic rework submission",defects.Select(d=>new ReworkRemediation(d,"Defect remedied",[png])).ToArray());
        var start=new ProcessStartInfo("git"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true};
        start.ArgumentList.Add("rev-parse");start.ArgumentList.Add("HEAD");using var git=Process.Start(start)??throw new Exception("Source commit unavailable");
        var commit=(await git.StandardOutput.ReadToEndAsync(ct)).Trim();await git.WaitForExitAsync(ct);
        if(git.ExitCode!=0||commit.Length!=40)throw new Exception("Actual source commit required");
        async Task Install()
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_set_session_context @key=N'DEV3.SourceCommit',@value={commit}",ct);
            // Execute the reviewed batch verbatim: its JSON literals are not composite-format placeholders.
            await using(var command=db.Database.GetDbConnection().CreateCommand())
            {
                command.Transaction=db.Database.CurrentTransaction!.GetDbTransaction();
                command.CommandText=await File.ReadAllTextAsync("scripts/verification/CanonicalDatabase/ModelProbe/SprintReworkSubmission.sql",ct);
                await command.ExecuteNonQueryAsync(ct);
            }
            await db.Database.ExecuteSqlRawAsync("""
                ALTER ROLE hotelops_rework_submission_writer ADD MEMBER DEV3_ReworkExecutor;
                GRANT UPDATE ON dbo.cleaning_rework_defects TO DEV3_ReworkExecutor;
                GRANT UPDATE ON dbo.work_intervals TO DEV3_ReworkExecutor;
                GRANT INSERT ON dbo.attachments TO DEV3_ReworkExecutor;
                GRANT INSERT ON dbo.fs016_rework_evidence TO DEV3_ReworkExecutor;
                """,ct);
        }
        async Task Cleanup()
        {
            var keys=await db.Database.SqlQuery<string>($"""
                SELECT a.storage_key AS Value FROM dbo.fs016_rework_evidence e JOIN dbo.attachments a ON a.attachment_id=e.attachment_id
                WHERE e.rework_id={id} AND a.entity_type='CLEANING_TASK' AND a.entity_id={task} AND a.uploaded_by={actor}
                """).ToArrayAsync(CancellationToken.None);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                DELETE dbo.fs016_rework_evidence WHERE rework_id={id};
                DELETE dbo.attachments WHERE entity_type='CLEANING_TASK' AND entity_id={task} AND uploaded_by={actor};
                DELETE dbo.notification_operation_reservations WHERE actor_user_id={actor}
                  AND JSON_VALUE(result_json,'$.contract')='FE04_REWORK_SUBMISSION_V1' AND TRY_CONVERT(bigint,JSON_VALUE(result_json,'$.reworkId'))={id};
                DELETE dbo.audit_logs WHERE actor_user_id={actor} AND entity_type='cleaning_reworks' AND entity_id=CONVERT(nvarchar(50),{id})
                  AND JSON_VALUE(new_values,'$.contract')='FE04_REWORK_SUBMISSION_V1';
                DROP PROCEDURE dbo.fe04_commit_rework_submission;
                ALTER ROLE hotelops_rework_submission_writer DROP MEMBER DEV3_ReworkExecutor;
                DROP ROLE hotelops_rework_submission_writer;
                DELETE dbo.fe04_rework_authority_issuers WHERE issuer='FE04.UC036' AND version=1;
                DROP TRIGGER dbo.TR_rework_evidence_revision_v1;
                DROP TRIGGER dbo.TR_rework_remediation_revision_v1;
                DROP TABLE dbo.fs016_rework_evidence;
                ALTER TABLE dbo.cleaning_rework_defects DROP CONSTRAINT CK_rework_remediation_v1,FK_rework_remediator_v1;
                ALTER TABLE dbo.cleaning_rework_defects DROP COLUMN remediation_note,remediated_by,remediated_at;
                EXEC sys.sp_dropextendedproperty @name=N'HotelOps.ReworkSubmission.V1';
                """,CancellationToken.None);
            foreach(var key in keys)
            {
                if(Path.GetFileName(key)!=key||key.Contains(':'))throw new Exception("Owned evidence filename required");
                File.Delete(Path.Combine(SprintReworkFixture.EvidenceRoot,key));
            }
        }
        await using(var rehearsal=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct))
        {await Install();await Cleanup();await rehearsal.RollbackAsync(ct);}
        await using(var setup=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct))
        {await Install();await setup.CommitAsync(ct);}
        async Task<RoomTypeBatchResult> Call(Guid operation,ReworkActionRequest request,string? tag=null)
        {
            await using var context=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()).Options);
            await context.Database.OpenConnectionAsync(ct);await context.Database.ExecuteSqlRawAsync("EXECUTE AS USER='DEV3_ReworkExecutor'",ct);
            try
            {
                var current=new Identity(new(actor,sid,false));
                var service=new RoomTypeCreationService(context,current,new PermissionService(current,new EfPermissionDataSource(context),new UnresolvedPermissionResourceResolver(),TimeProvider.System),
                    new SqlSessionService(context,new SessionRevocationService(context),TimeProvider.System),TimeProvider.System);
                return await service.SubmitAssignedReworkAsync(ticket,sid,branch,id,tag??initialTag,operation.ToString("D"),request,SprintReworkFixture.EvidenceRoot,ct);
            }
            finally{await context.Database.ExecuteSqlRawAsync("REVERT",ct);await context.Database.CloseConnectionAsync();}
        }
        async Task AssertUnchanged()
        {
            if(await db.WorkIntervals.CountAsync(w=>w.UserId==actor&&w.WorkItemKind=="REWORK"&&w.WorkItemId==id&&w.ClosedAt==null,ct)!=1
                ||await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.cleaning_rework_defects WHERE rework_id={id} AND remediation_note IS NOT NULL").SingleAsync(ct)!=0
                ||await db.Attachments.AnyAsync(a=>a.EntityType=="CLEANING_TASK"&&a.EntityId==task&&a.UploadedBy==actor,ct)
                ||'"'+Convert.ToHexString(await db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.cleaning_reworks WHERE rework_id={id}").SingleAsync(ct))+'"'!=initialTag)
                throw new Exception("Failed submission leaked task/interval/remediation/evidence");
        }
        try
        {
            if(browser!=null){await browser();return;}
            if((await Call(Guid.NewGuid(),body,"\"FFFFFFFFFFFFFFFF\"")).Status!=RoomTypeBatchStatus.Stale)throw new Exception("Stale submission admitted");
            try{await Call(Guid.NewGuid(),body with{Remediations=[]});throw new Exception("Missing remediation admitted");}
            catch(ValidationException){}
            var bad=body with{Remediations=defects.Select(d=>new ReworkRemediation(d,"Fixed",[png,new("invalid.png","image/png","bad")])).ToArray()};
            var filesBefore=Directory.GetFiles(SprintReworkFixture.EvidenceRoot).Length;
            try{await Call(Guid.NewGuid(),bad);throw new Exception("Invalid second photo admitted");}catch(ValidationException){}
            await AssertUnchanged();if(Directory.GetFiles(SprintReworkFixture.EvidenceRoot).Length!=filesBefore)throw new Exception("Rollback leaked first uploaded file");
            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE dbo.audit_logs ADD CONSTRAINT CK_DEV3_ReworkSubmitAuditFault CHECK
                  (ISNULL(JSON_VALUE(CASE WHEN ISJSON(new_values)=1 THEN new_values ELSE N'[]' END,'$.contract'),'')<>'FE04_REWORK_SUBMISSION_V1');
                """,ct);
            try
            {
                try{await Call(Guid.NewGuid(),body);throw new Exception("Expected submission audit failure missing");}
                catch(SqlException e) when(e.Number==547){}
                catch(DbUpdateException e) when(e.InnerException is SqlException {Number:547}){}
                await AssertUnchanged();if(Directory.GetFiles(SprintReworkFixture.EvidenceRoot).Length!=filesBefore)throw new Exception("Audit rollback leaked uploaded file");
            }
            finally{await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.audit_logs DROP CONSTRAINT CK_DEV3_ReworkSubmitAuditFault",ct);}
            var operation=Guid.NewGuid();var results=await Task.WhenAll(Call(operation,body),Call(operation,body));
            if(results.Any(r=>r.Status!=RoomTypeBatchStatus.Success||r.Value is not ReworkCommandResult{State:"PENDING_REINSPECTION",Contract:"FE04_REWORK_SUBMISSION_V1"}))
                throw new Exception("Concurrent submission failed: "+string.Join(',',results.Select(r=>r.Status)));
            if(await db.WorkIntervals.AnyAsync(w=>w.UserId==actor&&w.ClosedAt==null,ct)
                ||await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.notification_operation_reservations WHERE actor_user_id={actor} AND operation_key={operation} AND completed=1").SingleAsync(ct)!=1)
                throw new Exception("Submission did not close interval/create one durable receipt");
            if((await Call(operation,body with{Note="Different request"})).Status!=RoomTypeBatchStatus.Duplicate)throw new Exception("Submission operation key reused with changed request");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={actor}",ct);
            try
            {
                if((await Call(operation,body)).Status==RoomTypeBatchStatus.Success)throw new Exception("Submission replay bypassed revoked branch access");
            }
            finally{await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=NULL WHERE user_id={actor}",ct);}
            await db.Database.ExecuteSqlRawAsync("UPDATE dbo.fe04_rework_authority_issuers SET is_active=0 WHERE issuer='FE04.UC036' AND version=1",ct);
            try
            {
                if((await Call(operation,body)).Status!=RoomTypeBatchStatus.Duplicate)throw new Exception("Submission replay trusted disabled writer");
            }
            finally{await db.Database.ExecuteSqlRawAsync("UPDATE dbo.fe04_rework_authority_issuers SET is_active=1 WHERE issuer='FE04.UC036' AND version=1",ct);}
            if((await Call(operation,body)).Status!=RoomTypeBatchStatus.Success)throw new Exception("Valid submission replay not restored");
            if((await Call(Guid.NewGuid(),body)).Status!=RoomTypeBatchStatus.Stale)throw new Exception("Old submission ETag reused");
            if(!await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.cleaning_rework_defects WHERE rework_id={id} AND remediation_note='Defect remedied' AND remediated_by={actor}").AnyAsync(n=>n==defects.Length,ct))
                throw new Exception("Remediation attribution missing");
            await SprintReinspectionAssignmentProbe.VerifyAsync(db,branch,id,commit,ct);
        }
        finally
        {
            await using var cleanup=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,CancellationToken.None);
            await Cleanup();await cleanup.CommitAsync(CancellationToken.None);db.ChangeTracker.Clear();
        }
    }
    private sealed class Identity(CurrentUserInfo value):ICurrentUserAccessor
    {public bool TryGetCurrentUser(out CurrentUserInfo user){user=value;return true;}}
}
