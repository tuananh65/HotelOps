using System.Data;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

internal static class SprintApprovalBrowserProbe
{
    internal static async Task<int> RunAsync(string report, bool retry = false, bool concurrentBlock = false, bool approvalRace = false, bool rejection = false, bool rework = false)
    {
        if(File.Exists(report)) throw new IOException("Evidence already exists");
        using var budget=new CancellationTokenSource(TimeSpan.FromMinutes(4)); var ct=budget.Token;
        await using var db=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(
            "Server=localhost;Database=HotelOps_DEV3_142A_20261114_cc78c13f;Integrated Security=true;Encrypt=true;TrustServerCertificate=true").Options);
        var checks=new List<object>(); string? failure=null;
        foreach(var blocked in new[]{false,true})
        {
            long actor=0,branch=0,inspection=0,task=0,room=0,cleaner=0,incidentId=0; bool committed=false;
            var password=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))+"Aa1!";
            async Task Seed()
            {
                // Exact server/database/GUID/run guard runs before any mutation.
                await using var command=db.Database.GetDbConnection().CreateCommand();
                command.Transaction=db.Database.CurrentTransaction!.GetDbTransaction();
                command.CommandText=await File.ReadAllTextAsync("scripts/verification/CanonicalDatabase/ModelProbe/SprintApproveFixture.sql",ct);
                await using(var reader=await command.ExecuteReaderAsync(ct))
                { if(!await reader.ReadAsync(ct)) throw new Exception("Fixture missing"); actor=reader.GetInt64(0); branch=reader.GetInt64(1); inspection=reader.GetInt64(2);task=reader.GetInt64(3);room=reader.GetInt64(4); }
                await db.Database.ExecuteSqlRawAsync(await File.ReadAllTextAsync("scripts/verification/CanonicalDatabase/ModelProbe/SprintCleanerAttribution.sql",ct),ct);
                cleaner=await SprintApproveProbe.SeedCleanerAsync(db,actor,branch,task,"recipient-eligible",ct);
                if(rework) await SprintReworkFixture.SeedAsync(db,actor,cleaner,branch,ct);
                if(rejection)
                    await db.Database.ExecuteSqlInterpolatedAsync($"""
                        UPDATE dbo.room_inspections SET workflow_status='INSPECTING' WHERE inspection_id={inspection};
                        UPDATE dbo.inspection_results SET outcome='FAIL' WHERE inspection_id={inspection};
                        INSERT dbo.inspection_defects(inspection_id,checklist_item_id,description,status)
                          SELECT inspection_id,checklist_item_id,'Synthetic rejection defect','OPEN' FROM dbo.inspection_results WHERE inspection_id={inspection};
                        UPDATE dbo.room_inspections SET workflow_status='PENDING_CONCLUSION' WHERE inspection_id={inspection};
                        """,ct);
                var user=await db.Users.AsNoTracking().SingleAsync(u=>u.UserId==actor,ct);
                var hash=new PasswordHasher<User>().HashPassword(user,password);
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.users SET password_hash={hash} WHERE user_id={actor}",ct);
                if(retry) await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.notification_recipients ADD CONSTRAINT CK_DEV3_142A_NotificationFault CHECK(user_id<0)",ct);
                if(approvalRace && blocked)
                {
                    // Narrow approved index trial, with original definition restored during cleanup.
                    command.CommandText=await File.ReadAllTextAsync("scripts/verification/CanonicalDatabase/RoomBlockPairIsolated.sql",ct);
                    await command.ExecuteNonQueryAsync(ct);
                }
                if(blocked || concurrentBlock)
                {
                    var incident=new Incident {IncidentNo="D3-APPROVE-BLOCK",BranchId=branch,RoomId=room,IncidentTitle="Synthetic approval block",Description="Synthetic",Status="OPEN",ReportedBy=actor,ReportedAt=DateTime.UtcNow,DiscoveredAt=DateTime.UtcNow};
                    db.Incidents.Add(incident);await db.SaveChangesAsync(ct);
                    incidentId=incident.IncidentId;
                    if(blocked) db.RoomBlocks.Add(new RoomBlock{RoomId=room,IncidentId=incidentId,Reason="Synthetic block",BlockedBy=actor,BlockedAt=DateTime.UtcNow.AddMinutes(-1)});
                    await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
                    if(approvalRace && blocked)
                    {
                        var second=new Incident{IncidentNo="D3-APPROVE-BLOCK-2",BranchId=branch,RoomId=room,IncidentTitle="Second synthetic block",Description="Synthetic",Status="OPEN",ReportedBy=actor,ReportedAt=DateTime.UtcNow,DiscoveredAt=DateTime.UtcNow};
                        db.Incidents.Add(second);await db.SaveChangesAsync(ct);
                        db.RoomBlocks.Add(new RoomBlock{RoomId=room,IncidentId=second.IncidentId,Reason="Second synthetic block",BlockedBy=actor,BlockedAt=DateTime.UtcNow.AddMinutes(-1)});
                        await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
                    }
                }
                if(concurrentBlock)
                    await db.Database.ExecuteSqlRawAsync("""
                        CREATE TRIGGER dbo.TR_DEV3_142A_ApprovalDelay ON dbo.room_inspections AFTER UPDATE AS
                        BEGIN
                          IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON i.inspection_id=d.inspection_id
                            WHERE i.workflow_status='PASSED' AND d.workflow_status='PENDING_CONCLUSION')
                          BEGIN
                            DECLARE @r int;
                            EXEC @r=sys.sp_getapplock @Resource='DEV3:ApprovalDelay',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=1000;
                            IF @r<0 THROW 51994,'Synthetic approval synchronization failed',1;
                            WAITFOR DELAY '00:00:04';
                          END
                        END
                        """,ct);
            }
            async Task Cleanup(CancellationToken token)
            {
                if(rework) await SprintReworkFixture.CleanupAsync(db,cleaner,branch,inspection,token);
                if(concurrentBlock) await db.Database.ExecuteSqlRawAsync("DROP TRIGGER dbo.TR_DEV3_142A_ApprovalDelay",token);
                if(rejection) await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_inspections SET workflow_status='INSPECTING',status='IN_PROGRESS',result=NULL WHERE inspection_id={inspection}; DELETE dbo.inspection_defects WHERE inspection_id={inspection}",token);
                await db.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.room_blocks WHERE room_id={room} AND blocked_by={actor}; DELETE dbo.incidents WHERE room_id={room} AND reported_by={actor} AND incident_no IN('D3-APPROVE-BLOCK','D3-APPROVE-BLOCK-2')",token);
                if(approvalRace && blocked)
                    await db.Database.ExecuteSqlRawAsync("DROP INDEX UX_room_blocks_incident_room_active ON dbo.room_blocks; CREATE UNIQUE INDEX UX_room_blocks_active ON dbo.room_blocks(room_id) WHERE released_at IS NULL",token);
                await SprintNotificationCommitProbe.CleanupAsync(db,actor,cleaner,branch,inspection,token);
            }
            try
            {
                // Prove complete cleanup against this schema in a rollback transaction first.
                await using(var rehearsal=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct))
                {await Seed();await Cleanup(ct);await rehearsal.RollbackAsync(ct);}
                await using(var setup=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct))
                {await Seed();await setup.CommitAsync(ct);committed=true;}
                var start=new ProcessStartInfo("node"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
                start.ArgumentList.Add(rejection ? "frontend/scripts/sprintRejectionBrowser.mjs" : "frontend/scripts/sprintApprovalBrowser.mjs");
                using var process=Process.Start(start)??throw new Exception("Browser unavailable");
                try
                {
                    await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new{username="D3-EVIDENCE",password,branch,inspection,blocked,retry,concurrentBlock,approvalRace,rework}).AsMemory(),ct);process.StandardInput.Close();
                    var output=process.StandardOutput.ReadToEndAsync(ct);var errors=process.StandardError.ReadToEndAsync(ct);
                    if(concurrentBlock)
                    {
                        var deadline=DateTime.UtcNow.AddSeconds(30);bool observed=false;
                        while(DateTime.UtcNow<deadline && !process.HasExited)
                        {
                            if(await db.Database.SqlQueryRaw<int>("SELECT APPLOCK_TEST('public','DEV3:ApprovalDelay','Shared','Session') AS Value").SingleAsync(ct)==0)
                            {observed=true;break;}
                            await Task.Delay(50,ct);
                        }
                        if(!observed)throw new Exception("Approval did not reach synchronized locked domain write");
                        // This connection races the API's retained range/row locks. It must wait
                        // for approval to commit, then the next API read must reflect the new block facts.
                        var elapsed=Stopwatch.StartNew();
                        if(blocked)
                            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_blocks SET released_at=SYSUTCDATETIME() WHERE room_id={room} AND blocked_by={actor}",ct);
                        else
                            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.room_blocks(room_id,incident_id,reason,blocked_by,blocked_at) VALUES({room},{incidentId},'Synthetic concurrent block',{actor},SYSUTCDATETIME())",ct);
                        if(elapsed.Elapsed<TimeSpan.FromSeconds(1))throw new Exception("Concurrent block write bypassed retained approval locks");
                        checks.Add(new{blocked,concurrentBlockWrite=blocked?"release":"insert",waitedMilliseconds=elapsed.ElapsedMilliseconds});
                    }
                    await process.WaitForExitAsync(ct);var result=await output;await errors;
                    using var json=JsonDocument.Parse(result);checks.Add(new{blocked,browser=json.RootElement.Clone()});
                    if(!json.RootElement.GetProperty("success").GetBoolean())throw new Exception("Browser failed at "+json.RootElement.GetProperty("stage").GetString());
                }
                finally{if(!process.HasExited)process.Kill(entireProcessTree:true);}
                if(rejection)
                {
                    await SprintRejectionDeliveryProbe.VerifyAsync(db,actor,cleaner,branch,inspection,ct);
                    if(await db.Database.SqlQuery<string>($"SELECT workflow_status AS Value FROM dbo.cleaning_tasks WHERE cleaning_task_id={task}").SingleAsync(ct)!="REWORK_REQUIRED"
                        ||await db.RoomInspections.Where(i=>i.InspectionId==inspection).Select(i=>i.Result).SingleAsync(ct)!="FAIL"
                        ||await db.Rooms.Where(r=>r.RoomId==room).Select(r=>r.OperationalStatus).SingleAsync(ct)!="INSPECTION")
                        throw new Exception("Rejected lifecycle/physical status mismatch");
                    checks.Add(new{blocked,rejection=true,atomicLifecycle=true,deduplicatedFanout=true,revocation=true,retry=true});
                    if(rework)
                    {
                        var created=await db.CleaningReworks.AsNoTracking().SingleAsync(r=>r.InspectionId==inspection,ct);
                        if(created.AssigneeUserId!=cleaner || created.Status!="PENDING"
                            || await db.Database.SqlQuery<string>($"SELECT workflow_status AS Value FROM dbo.cleaning_reworks WHERE rework_id={created.ReworkId}").SingleAsync(ct)!="PENDING_ACCEPTANCE"
                            || await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.cleaning_rework_defects WHERE rework_id={created.ReworkId}").SingleAsync(ct)!=1
                            || !await db.AuditLogs.AnyAsync(a=>a.EntityType=="cleaning_reworks"&&a.EntityId==created.ReworkId.ToString(),ct))
                            throw new Exception("Persisted rework/defect/assignee/audit mismatch");
                        await SprintReworkAuthorityProbe.VerifyAsync(db,cleaner,branch,created.ReworkId,ct);
                        checks.Add(new{blocked,reworkPersisted=true,selectedDefects=true,assignee=cleaner,audit=true,committedAuthority=true,leastPrivilegeReader=true,sourceTamperDenied=true,disabledIssuerDenied=true,unauthorizedDenied=true,noShiftAssignee=true,ineligibleAssigneesDenied=true});
                    }
                }
                else
                {
                if(retry)
                {
                    if(await db.Notifications.AnyAsync(ct)||await db.NotificationRecipients.AnyAsync(ct)
                        ||await db.AuditLogs.CountAsync(a=>a.EntityType=="INSPECTION"&&a.EntityId==inspection.ToString(),ct)!=1)
                        throw new Exception("Delivery fault lost durable event or published partial inbox");
                    // Revocation is synthetic and precedes fault removal. Hosted retry must recheck
                    // current access, not trust the eligible recipient captured during approval.
                    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={cleaner}",ct);
                    await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.notification_recipients DROP CONSTRAINT CK_DEV3_142A_NotificationFault",ct);
                    await Task.Delay(TimeSpan.FromSeconds(3),ct);
                    if(await db.NotificationRecipients.AnyAsync(ct))throw new Exception("Worker bypassed revoked recipient access");
                    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=NULL WHERE user_id={cleaner}",ct);
                    var deadline=DateTime.UtcNow.AddSeconds(15);
                    while(!await db.NotificationRecipients.AnyAsync(n=>n.UserId==cleaner&&n.DeliveryStatus=="DELIVERED",ct)&&DateTime.UtcNow<deadline)
                        await Task.Delay(250,ct);
                    await Task.Delay(TimeSpan.FromSeconds(3),ct);
                    checks.Add(new{blocked,hostedRetry=true,revokedRecipientDenied=true,restoredAccessRetried=true,noApprovalReplay=true});
                }
                if(!await db.RoomInspections.AnyAsync(i=>i.InspectionId==inspection&&i.Status=="COMPLETED"&&i.Result=="PASS",ct)
                    || !await db.CleaningTasks.AnyAsync(t=>t.CleaningTaskId==task&&t.Status=="COMPLETED",ct)
                    || await db.Rooms.Where(r=>r.RoomId==room).Select(r=>r.OperationalStatus).SingleAsync(ct)!=(blocked?"INSPECTION":"READY")
                    || await db.RoomStatusHistories.CountAsync(h=>h.RoomId==room&&h.TriggerId==inspection,ct)!=(blocked?0:1)
                    || await db.AuditLogs.CountAsync(a=>a.EntityType=="INSPECTION"&&a.EntityId==inspection.ToString(),ct)!=1
                    || await db.NotificationRecipients.CountAsync(n=>n.UserId==cleaner&&n.DeliveryStatus=="DELIVERED",ct)!=1)
                    throw new Exception("Persisted approval/room/history/audit/original-cleaner notification mismatch");
                checks.Add(new{blocked,persistedBusinessAndNotification=true});
                if(approvalRace && blocked)
                {
                    if(await db.RoomBlocks.CountAsync(b=>b.RoomId==room&&b.ReleasedAt==null,ct)!=2)throw new Exception("Expected two effective incident blocks");
                    checks.Add(new{twoEffectiveBlocks=true,approvalRemainedBlocked=true});
                }
                }
            }
            catch(Exception error){failure=error.GetType().Name+": "+error.Message;}
            finally
            {
                if(committed){await using var cleanup=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,CancellationToken.None);await Cleanup(CancellationToken.None);await cleanup.CommitAsync(CancellationToken.None);}
                db.ChangeTracker.Clear();
            }
            if(await db.Users.AnyAsync(ct)||await db.Rooms.AnyAsync(ct)||await db.RoomBlocks.AnyAsync(ct)||await db.AuditLogs.AnyAsync(ct)||await db.Notifications.AnyAsync(ct))
                throw new Exception("Approval fixture cleanup failed");
            checks.Add(new{blocked,cleanup=true});if(failure!=null)break;
        }
        await File.WriteAllTextAsync(report,JsonSerializer.Serialize(new{success=failure==null,checks,failure},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine(failure??"PASS approval browser HTTPS SQL, cleanup verified");return failure==null?0:1;
    }
}
