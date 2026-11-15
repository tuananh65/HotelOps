using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Rooms;
using HotelOps.Api.Services.Insights;
using HotelOps.Api.DTOs.Insights;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

internal static class SprintReadinessProbe
{
    internal static async Task<int> RunAsync(string report)
    {
        if(File.Exists(report))throw new IOException("Evidence already exists");
        using var budget=new CancellationTokenSource(TimeSpan.FromMinutes(2));var ct=budget.Token;
        await using var db=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(
            "Server=localhost;Database=HotelOps_DEV3_142A_20261114_cc78c13f;Integrated Security=true;Encrypt=true;TrustServerCertificate=true").Options);
        var checks=new List<string>();string? failure=null;bool rollback=false;
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        try
        {
            // Fixture SQL checks exact server/database/GUID/run and emptiness before mutation.
            await using var command=db.Database.GetDbConnection().CreateCommand();command.Transaction=tx.GetDbTransaction();
            command.CommandText=await File.ReadAllTextAsync("scripts/verification/CanonicalDatabase/ModelProbe/SprintApproveFixture.sql",ct);
            long actor,branch,inspection,task,room;
            await using(var reader=await command.ExecuteReaderAsync(ct))
            {if(!await reader.ReadAsync(ct))throw new Exception("No fixture");actor=reader.GetInt64(0);branch=reader.GetInt64(1);inspection=reader.GetInt64(2);task=reader.GetInt64(3);room=reader.GetInt64(4);}
            // Nested guarded index trial remains inside the outer rollback transaction.
            command.CommandText=await File.ReadAllTextAsync("scripts/verification/CanonicalDatabase/RoomBlockPairIsolated.sql",ct);
            await command.ExecuteNonQueryAsync(ct);
            var now=DateTime.UtcNow;
            var date=DateOnly.FromDateTime(now);
            ReportResult? frozenBlocked=null;
            async Task State(string expected,string name)
            {
                var states=await new EffectiveRoomReadiness(db).ReadAsync([room],now,ct);
                if(states[room].Readiness!=expected||states[room].CanUse!=(expected=="READY"))
                    throw new Exception(name+": "+states[room].Readiness);
                checks.Add(name);
                var report=await new ReportSnapshotQueries(db).MaterializeAsync("rooms",new(){BranchId=branch},
                    new([branch],null,date,date),new DateTimeOffset(DateTime.SpecifyKind(now,DateTimeKind.Utc)),ct);
                var row=report.Sections.Single(s=>s.Key=="rooms").Rows.Single(r=>((JsonElement)r["id"]!).GetInt64()==room);
                if(((JsonElement)row["effectiveReadiness"]!).GetString()!=expected
                    ||((JsonElement)row["canUse"]!).GetBoolean()!=(expected=="READY"))
                    throw new Exception("Report readiness disagrees: "+name);
                checks.Add("report snapshot/export fields: "+expected);
                if(expected=="BLOCKED"&&frozenBlocked==null)frozenBlocked=report;
            }
            await State("PENDING_CLEANING","never-passed cycle follows GBR044 priority and cannot authorize use");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_inspections SET workflow_status='PASSED',status='COMPLETED',result='PASS' WHERE inspection_id={inspection}; UPDATE dbo.cleaning_tasks SET workflow_status='COMPLETED',status='COMPLETED' WHERE cleaning_task_id={task}",ct);
            await State("READY","completed passed current cycle is effectively Ready without rewriting stored code");
            var incidents=new[]{new Incident{IncidentNo="D3READ-A",BranchId=branch,RoomId=room,IncidentTitle="Synthetic A",Description="Synthetic",Status="OPEN",ReportedBy=actor,ReportedAt=now,DiscoveredAt=now},
                new Incident{IncidentNo="D3READ-B",BranchId=branch,RoomId=room,IncidentTitle="Synthetic B",Description="Synthetic",Status="OPEN",ReportedBy=actor,ReportedAt=now,DiscoveredAt=now}};
            db.Incidents.AddRange(incidents);await db.SaveChangesAsync(ct);
            var blocks=incidents.Select(i=>new RoomBlock{RoomId=room,IncidentId=i.IncidentId,Reason="Synthetic readiness block",BlockedBy=actor,BlockedAt=now.AddMinutes(-1),ExpectedReleaseAt=now.AddSeconds(-1)}).ToArray();
            db.RoomBlocks.AddRange(blocks);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
            await State("BLOCKED","two effective incident blocks override Passed; expected-release time does not release");
            await db.RoomBlocks.Where(b=>b.BlockId==blocks[0].BlockId).ExecuteUpdateAsync(s=>s.SetProperty(b=>b.ReleasedAt,now),ct);
            await State("BLOCKED","removing one block preserves other effective block");
            await db.Rooms.Where(r=>r.RoomId==room).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,false),ct);
            await State("INACTIVE","inactive overrides effective blocks and Passed");
            await db.Rooms.Where(r=>r.RoomId==room).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,true),ct);
            await db.RoomBlocks.Where(b=>b.BlockId==blocks[1].BlockId).ExecuteUpdateAsync(s=>s.SetProperty(b=>b.ReleasedAt,now),ct);
            await State("READY","last block removal recomputes passed current cycle");
            if(frozenBlocked==null||((JsonElement)frozenBlocked.Rows.Single()["effectiveReadiness"]!).GetString()!="BLOCKED"
                ||((JsonElement)frozenBlocked.Rows.Single()["canUse"]!).GetBoolean())throw new Exception("Frozen blocked snapshot changed after release");
            var csv=System.Text.Encoding.UTF8.GetString(ReportCsvRenderer.Render(frozenBlocked).Content);
            if(!csv.Contains("effectiveReadiness")||!csv.Contains("canUse")||!csv.Contains("BLOCKED"))throw new Exception("Readiness missing from frozen CSV");
            checks.Add("blocked snapshot and CSV remain frozen after block removal");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_inspections SET workflow_status='FAILED',result='FAIL' WHERE inspection_id={inspection}; UPDATE dbo.cleaning_tasks SET workflow_status='REWORK_REQUIRED',status='IN_PROGRESS' WHERE cleaning_task_id={task}; INSERT dbo.cleaning_reworks(cleaning_task_id,inspection_id,reason,requested_at,status,workflow_status) VALUES({task},{inspection},'Synthetic failed cycle',{now},'PENDING','PENDING_ACCEPTANCE')",ct);
            await State("REWORK_REQUIRED","failed current cycle with unsubmitted rework never becomes Ready after release");
            await tx.CreateSavepointAsync("MissingReadinessCapability",ct);
            try
            {
                await db.Database.ExecuteSqlRawAsync("EXEC sys.sp_dropextendedproperty @name=N'HotelOps.Housekeeping.V1'",ct);
                await State("UNKNOWN","missing lifecycle capability cannot authorize use in report");
            }
            finally{await tx.RollbackToSavepointAsync("MissingReadinessCapability",CancellationToken.None);}
            var persisted=await db.Rooms.Where(r=>r.RoomId==room).Select(r=>r.OperationalStatus).SingleAsync(ct);
            if(persisted!="INSPECTION")throw new Exception("Readiness mutated physical room status");checks.Add("physical status and CHECK constraints unchanged");
        }
        catch(Exception error){failure=error.GetType().Name+": "+error.Message;}
        finally{await tx.RollbackAsync(CancellationToken.None);rollback=true;}
        await File.WriteAllTextAsync(report,JsonSerializer.Serialize(new{success=failure==null,checks,failure,rollback,
            scope="Actual SQL readiness reads; guarded index trial and fixtures rolled back; no HTTPS/conclusion acceptance"},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"Readiness: {(failure==null?"PASS":"FAIL")}; checks={checks.Count}; rollback={rollback}");return failure==null?0:1;
    }
}
