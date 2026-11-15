using System.Data;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

internal static class SprintInspectionBrowserProbe
{
    internal static async Task<int> RunAsync(string report)
    {
        if (File.Exists(report)) throw new IOException("Evidence already exists");
        using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var ct = budget.Token;
        await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer("Server=localhost;Database=HotelOps_DEV3_142A_20261114_cc78c13f;Integrated Security=true;Encrypt=true;TrustServerCertificate=true").Options);
        var fixture = await File.ReadAllTextAsync("scripts/verification/CanonicalDatabase/ModelProbe/SprintApproveFixture.sql", ct);
        if(await db.AuditLogs.AnyAsync(ct)||await db.UserSessions.AnyAsync(ct)) throw new Exception("Empty audit/session baseline required");
        var root = Path.GetFullPath("TestResults/sprint18-evidence");
        Directory.CreateDirectory(root);
        if (Directory.EnumerateFileSystemEntries(root).Any()) throw new Exception("Empty owned evidence directory required");
        long actor=0, branch=0, inspection=0, task=0, room=0, severity=0;
        bool newSeverity=false, committed=false, cleaned=false;
        var checks=new List<object>(); string? failure=null;
        var password=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))+"Aa1!";
        async Task Seed()
        {
            // This script checks exact server/database/GUID/ownership marker and empty baseline before INSERT.
            await using var command=db.Database.GetDbConnection().CreateCommand();
            command.Transaction=db.Database.CurrentTransaction!.GetDbTransaction(); command.CommandText=fixture;
            await using(var reader=await command.ExecuteReaderAsync(ct))
            {
                if(!await reader.ReadAsync(ct)) throw new Exception("Fixture identity missing");
                actor=reader.GetInt64(0);branch=reader.GetInt64(1);inspection=reader.GetInt64(2);task=reader.GetInt64(3);room=reader.GetInt64(4);
            }
            var user=await db.Users.AsNoTracking().SingleAsync(u=>u.UserId==actor,ct);
            var hash=new PasswordHasher<User>().HashPassword(user,password);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.users SET password_hash={hash} WHERE user_id={actor}; UPDATE dbo.room_inspections SET workflow_status='INSPECTING' WHERE inspection_id={inspection}; DELETE dbo.inspection_results WHERE inspection_id={inspection}; UPDATE dbo.room_inspections SET workflow_status='ASSIGNED',status='PENDING' WHERE inspection_id={inspection};",ct);
            var existing=await db.ReferenceValues.AsNoTracking().SingleOrDefaultAsync(r=>r.GroupCode=="SEVERITY"&&r.ValueCode=="MAJOR",ct);
            newSeverity=existing==null;
            if(existing==null)
            {
                existing=new ReferenceValue{GroupCode="SEVERITY",ValueCode="MAJOR",DisplayName="Synthetic major",IsActive=true};
                db.Add(existing);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
            }
            if(!existing.IsActive) throw new Exception("Existing major severity must be active");
            severity=existing.ReferenceId;
            // The preceding fixture verifies the exact disposable identity and an empty baseline.
            await db.Database.ExecuteSqlRawAsync("""
                IF OBJECT_ID('dbo.CK_DEV3_InspectionExecutionAuditFault','C') IS NOT NULL
                    THROW 51999,'Foreign audit fault constraint; trial refused',1;
                ALTER TABLE dbo.audit_logs ADD CONSTRAINT CK_DEV3_InspectionExecutionAuditFault
                    CHECK(entity_type<>'room_inspections' OR new_values NOT LIKE '%Synthetic audit fault%');
                """,ct);
        }
        async Task Cleanup(CancellationToken token)
        {
            // Only IDs returned by the guarded fixture. Legacy templates have branch_id NULL;
            // no trigger is disabled and no application immutability rule is relaxed.
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                ALTER TABLE dbo.audit_logs DROP CONSTRAINT CK_DEV3_InspectionExecutionAuditFault;
                DECLARE @version bigint=(SELECT version_id FROM dbo.room_inspections WHERE inspection_id={inspection});
                DECLARE @list bigint=(SELECT checklist_id FROM dbo.inspection_checklist_versions WHERE version_id=@version);
                DECLARE @type bigint=(SELECT room_type_id FROM dbo.rooms WHERE room_id={room});
                DECLARE @floor bigint=(SELECT floor_id FROM dbo.rooms WHERE room_id={room});
                DECLARE @cleanType bigint=(SELECT cleaning_type_id FROM dbo.cleaning_tasks WHERE cleaning_task_id={task});
                UPDATE dbo.room_inspections SET workflow_status='INSPECTING',status='IN_PROGRESS' WHERE inspection_id={inspection};
                DELETE l FROM dbo.inspection_defect_attachments l JOIN dbo.inspection_defects d ON d.defect_id=l.defect_id WHERE d.inspection_id={inspection};
                DELETE l FROM dbo.inspection_result_attachments l JOIN dbo.inspection_results r ON r.result_id=l.result_id WHERE r.inspection_id={inspection};
                DELETE dbo.attachments WHERE entity_type='INSPECTION' AND entity_id={inspection} AND uploaded_by={actor};
                DELETE dbo.inspection_defects WHERE inspection_id={inspection};
                DELETE dbo.inspection_results WHERE inspection_id={inspection};
                DELETE dbo.room_inspections WHERE inspection_id={inspection};
                DELETE dbo.inspection_checklist_items WHERE version_id=@version;
                DELETE dbo.inspection_checklist_versions WHERE version_id=@version;
                DELETE dbo.inspection_checklists WHERE checklist_id=@list AND branch_id IS NULL;
                DELETE dbo.room_cleaning_cycle_state WHERE room_id={room} AND cleaning_task_id={task};
                DELETE dbo.cleaning_tasks WHERE cleaning_task_id={task};
                DELETE dbo.rooms WHERE room_id={room};
                DELETE dbo.room_types WHERE room_type_id=@type;
                DELETE dbo.floors WHERE floor_id=@floor;
                DELETE dbo.reference_values WHERE reference_id=@cleanType AND value_code='D3-SYNTHETIC';
                DELETE dbo.user_roles WHERE user_id={actor};
                DELETE dbo.user_branch_access WHERE user_id={actor};
                DELETE dbo.user_sessions WHERE user_id={actor};
                DELETE dbo.audit_logs WHERE actor_user_id={actor} OR (entity_type='users' AND entity_id=CONVERT(nvarchar(50),{actor}));
                DELETE dbo.users WHERE user_id={actor} AND username='D3-EVIDENCE';
                DELETE dbo.branches WHERE branch_id={branch} AND branch_code='D3-EVIDENCE';
                """,token);
            if(newSeverity) await db.ReferenceValues.Where(r=>r.ReferenceId==severity).ExecuteDeleteAsync(token);
        }
        // Prove fixture/schema/cleanup compatibility in a rollback-only transaction first.
        await using(var rehearsal=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct))
        { await Seed(); await Cleanup(ct); await rehearsal.RollbackAsync(ct); }
        checks.Add(new{cleanupRehearsalRolledBack=true});
        try
        {
            await using(var setup=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct))
            { await Seed(); await setup.CommitAsync(ct); committed=true; }
            var start=new ProcessStartInfo("node"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
            start.ArgumentList.Add("frontend/scripts/sprintInspectionBrowser.mjs");
            using var process=Process.Start(start)??throw new Exception("Browser unavailable");
            try
            {
                await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new{username="D3-EVIDENCE",password,branch,inspection,severity}).AsMemory(),ct);
                process.StandardInput.Close();
                var output=process.StandardOutput.ReadToEndAsync(ct);var errors=process.StandardError.ReadToEndAsync(ct);
                await process.WaitForExitAsync(ct); var result=await output;await errors;
                using var json=JsonDocument.Parse(result);checks.Add(new{browser=json.RootElement.Clone()});
                if(!json.RootElement.GetProperty("success").GetBoolean())throw new Exception("Browser failed at "+json.RootElement.GetProperty("stage").GetString());
            }
            finally{if(!process.HasExited)process.Kill(entireProcessTree:true);}
            var state=await db.Database.SqlQuery<string>($"SELECT workflow_status AS Value FROM dbo.room_inspections WHERE inspection_id={inspection}").SingleAsync(ct);
            if(state!="PENDING_CONCLUSION"||await db.InspectionDefects.CountAsync(d=>d.InspectionId==inspection,ct)!=1
                ||await db.InspectionResults.CountAsync(r=>r.InspectionId==inspection&&r.Outcome=="FAIL",ct)!=1)
                throw new Exception("Persisted execution results mismatch");
            if(await db.AuditLogs.CountAsync(a=>a.ActorUserId==actor&&a.EntityType=="room_inspections",ct)!=6)
                throw new Exception("Expected exactly six inspection audits including two edits");
            if(await db.InspectionDefects.Where(d=>d.InspectionId==inspection).Select(d=>d.Description).SingleAsync(ct)!="Synthetic stain final")
                throw new Exception("Edited defect not persisted");
            var attachments=await db.Attachments.AsNoTracking().Where(a=>a.EntityType=="INSPECTION"&&a.EntityId==inspection).ToArrayAsync(ct);
            if(attachments.Length!=2 || await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.inspection_result_attachments a JOIN dbo.inspection_results r ON r.result_id=a.result_id WHERE r.inspection_id={inspection}").SingleAsync(ct)!=1
                ||await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.inspection_defect_attachments a JOIN dbo.inspection_defects d ON d.defect_id=a.defect_id WHERE d.inspection_id={inspection}").SingleAsync(ct)!=1)
                throw new Exception("Criterion/defect evidence links mismatch");
            foreach(var attachment in attachments)
            {
                if(!System.Text.RegularExpressions.Regex.IsMatch(attachment.StorageKey,"^[a-f0-9]{32}\\.png$"))throw new Exception("Unexpected evidence key");
                var bytes=await File.ReadAllBytesAsync(Path.Combine(root,attachment.StorageKey),ct);
                if(!bytes.SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}))throw new Exception("Persisted evidence bytes mismatch");
            }
            if(Directory.EnumerateFiles(root).Count()!=attachments.Length) throw new Exception("Rolled-back evidence left orphan files");
            checks.Add(new{persistedState=state,inspectionAudits=6,defects=1,defectEditsPersisted=true,evidenceBytesVerified=true,criterionAndDefectEvidenceLinked=true});
        }
        catch(Exception e){failure=e.GetType().Name+": "+e.Message;}
        finally
        {
            if(committed)
            {
                var keys=await db.Attachments.Where(a=>a.EntityType=="INSPECTION"&&a.EntityId==inspection&&a.UploadedBy==actor).Select(a=>a.StorageKey).ToArrayAsync();
                await using(var cleanup=await db.Database.BeginTransactionAsync()) {await Cleanup(CancellationToken.None);await cleanup.CommitAsync();}
                foreach(var key in keys)
                {
                    if(!System.Text.RegularExpressions.Regex.IsMatch(key,"^[a-f0-9]{32}\\.png$"))throw new Exception("Unsafe cleanup key");
                    File.Delete(Path.Combine(root,key));
                }
            }
            cleaned=!await db.Users.AnyAsync()&&!await db.Branches.AnyAsync()&&!await db.RoomInspections.AnyAsync()&&!await db.Attachments.AnyAsync()&&!await db.AuditLogs.AnyAsync()&&!Directory.EnumerateFileSystemEntries(root).Any();
            await File.WriteAllTextAsync(report,JsonSerializer.Serialize(new{success=failure==null&&cleaned,fixtureActor=actor,fixtureInspection=inspection,checks,failure,cleanup=cleaned,
                scope="Real canonical production browser over validated HTTPS and owned SQL; execution through Pending Conclusion only; no approval or readiness acceptance"},new JsonSerializerOptions{WriteIndented=true}));
        }
        return failure==null&&cleaned?0:1;
    }
}
