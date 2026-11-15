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
    internal static async Task<int> RunAsync(string report)
    {
        if(File.Exists(report)) throw new IOException("Evidence already exists");
        using var budget=new CancellationTokenSource(TimeSpan.FromMinutes(4)); var ct=budget.Token;
        await using var db=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(
            "Server=localhost;Database=HotelOps_DEV3_142A_20261114_cc78c13f;Integrated Security=true;Encrypt=true;TrustServerCertificate=true").Options);
        var checks=new List<object>(); string? failure=null;
        foreach(var blocked in new[]{false,true})
        {
            long actor=0,branch=0,inspection=0,task=0,room=0,cleaner=0; bool committed=false;
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
                var user=await db.Users.AsNoTracking().SingleAsync(u=>u.UserId==actor,ct);
                var hash=new PasswordHasher<User>().HashPassword(user,password);
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.users SET password_hash={hash} WHERE user_id={actor}",ct);
                if(blocked)
                {
                    var incident=new Incident {IncidentNo="D3-APPROVE-BLOCK",BranchId=branch,RoomId=room,IncidentTitle="Synthetic approval block",Description="Synthetic",Status="OPEN",ReportedBy=actor,ReportedAt=DateTime.UtcNow,DiscoveredAt=DateTime.UtcNow};
                    db.Incidents.Add(incident);await db.SaveChangesAsync(ct);
                    db.RoomBlocks.Add(new RoomBlock{RoomId=room,IncidentId=incident.IncidentId,Reason="Synthetic block",BlockedBy=actor,BlockedAt=DateTime.UtcNow.AddMinutes(-1)});
                    await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
                }
            }
            async Task Cleanup(CancellationToken token)
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.room_blocks WHERE room_id={room} AND blocked_by={actor}; DELETE dbo.incidents WHERE room_id={room} AND reported_by={actor} AND incident_no='D3-APPROVE-BLOCK'",token);
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
                start.ArgumentList.Add("frontend/scripts/sprintApprovalBrowser.mjs");
                using var process=Process.Start(start)??throw new Exception("Browser unavailable");
                try
                {
                    await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new{username="D3-EVIDENCE",password,branch,inspection,blocked}).AsMemory(),ct);process.StandardInput.Close();
                    var output=process.StandardOutput.ReadToEndAsync(ct);var errors=process.StandardError.ReadToEndAsync(ct);
                    await process.WaitForExitAsync(ct);var result=await output;await errors;
                    using var json=JsonDocument.Parse(result);checks.Add(new{blocked,browser=json.RootElement.Clone()});
                    if(!json.RootElement.GetProperty("success").GetBoolean())throw new Exception("Browser failed at "+json.RootElement.GetProperty("stage").GetString());
                }
                finally{if(!process.HasExited)process.Kill(entireProcessTree:true);}
                if(!await db.RoomInspections.AnyAsync(i=>i.InspectionId==inspection&&i.Status=="COMPLETED"&&i.Result=="PASS",ct)
                    || !await db.CleaningTasks.AnyAsync(t=>t.CleaningTaskId==task&&t.Status=="COMPLETED",ct)
                    || await db.Rooms.Where(r=>r.RoomId==room).Select(r=>r.OperationalStatus).SingleAsync(ct)!=(blocked?"INSPECTION":"READY")
                    || await db.RoomStatusHistories.CountAsync(h=>h.RoomId==room&&h.TriggerId==inspection,ct)!=(blocked?0:1)
                    || await db.AuditLogs.CountAsync(a=>a.EntityType=="INSPECTION"&&a.EntityId==inspection.ToString(),ct)!=1
                    || await db.NotificationRecipients.CountAsync(n=>n.UserId==cleaner&&n.DeliveryStatus=="DELIVERED",ct)!=1)
                    throw new Exception("Persisted approval/room/history/audit/original-cleaner notification mismatch");
                checks.Add(new{blocked,persistedBusinessAndNotification=true});
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
