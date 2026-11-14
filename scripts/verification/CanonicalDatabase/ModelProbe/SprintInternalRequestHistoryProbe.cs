using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.InternalRequests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

internal static class SprintInternalRequestHistoryProbe
{
    internal static async Task<int> RunAsync(string report)
    {
        if(File.Exists(report))throw new IOException("Evidence already exists");
        using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(2)); var ct=timeout.Token;
        await using var db=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer("Server=localhost;Database=HotelOps_DEV3_142A_20261114_cc78c13f;Integrated Security=true;Encrypt=true;TrustServerCertificate=true").Options);
        var checks=new List<string>(); string? failure=null; bool rollback=false;
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        try
        {
            await using var command=db.Database.GetDbConnection().CreateCommand(); command.Transaction=tx.GetDbTransaction();
            // Reuse the pinned sprint fixture's exact server/database/GUID/run guard, before any mutation.
            command.CommandText=await File.ReadAllTextAsync("scripts/verification/CanonicalDatabase/ModelProbe/SprintApproveFixture.sql",ct);
            long actor,branch;
            await using(var reader=await command.ExecuteReaderAsync(ct)) {if(!await reader.ReadAsync(ct))throw new Exception("No fixture");actor=reader.GetInt64(0);branch=reader.GetInt64(1);}
            var prefix="D3IR"+Guid.NewGuid().ToString("N")[..10];
            var type=new InternalRequestType {TypeCode=prefix,TypeName="Synthetic",IsActive=true};
            var foreign=new Branch {BranchCode=prefix,BranchName="Foreign synthetic",IsActive=true};
            db.InternalRequestTypes.Add(type);db.Branches.Add(foreign);await db.SaveChangesAsync(ct);
            var now=new DateTime(2026,11,15,0,0,0,DateTimeKind.Utc);
            InternalRequest Row(string suffix,string status,DateTime? due,long? other=null)=>new() {RequestNo=prefix+suffix,BranchId=other??branch,RequestTypeId=type.RequestTypeId,RequesterId=actor,Subject=suffix=="A"?"Literal % request":"Synthetic "+suffix,Description="Synthetic",Status=status,DueAt=due,CreatedAt=now.AddDays(-2)};
            var a=Row("A","IN_PROGRESS",now.AddMinutes(-1)); var equal=Row("B","IN_PROGRESS",now);var closed=Row("C","CLOSED",now.AddDays(-1));var undated=Row("D","IN_PROGRESS",null);var hidden=Row("F","IN_PROGRESS",now.AddDays(-2),foreign.BranchId);
            db.InternalRequests.AddRange(a,equal,closed,undated,hidden);await db.SaveChangesAsync(ct);
            db.InternalRequestEvents.AddRange(new InternalRequestEvent {InternalRequestId=a.InternalRequestId,EventType="PROGRESS",ActorId=actor,OccurredAt=now,EventNote="one"},new InternalRequestEvent {InternalRequestId=a.InternalRequestId,EventType="PROGRESS",ActorId=actor,OccurredAt=now,EventNote="two"});await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
            var scoped=db.InternalRequests.Where(r=>r.BranchId==branch&&r.RequesterId==actor);
            void Check(bool pass,string name){if(!pass)throw new Exception(name);checks.Add(name);}
            var all=await InternalRequestHistoryQueries.ListAsync(scoped,new(),now,ct);
            Check(all.TotalCount==4,"caller scope excludes foreign request");
            Check(all.Items.Select(r=>r.Id).SequenceEqual(new[]{closed.InternalRequestId,a.InternalRequestId,equal.InternalRequestId,undated.InternalRequestId}),"deadline order with null last");
            var overdue=await InternalRequestHistoryQueries.ListAsync(scoped,new(Overdue:true),now,ct);
            Check(overdue.Items.Count==1&&overdue.Items[0].Id==a.InternalRequestId,"strict overdue boundary and terminal exclusion");
            var notOverdue=await InternalRequestHistoryQueries.ListAsync(scoped,new(Overdue:false),now,ct);Check(notOverdue.TotalCount==3,"non-overdue includes deadline equal null and closed");
            var search=await InternalRequestHistoryQueries.ListAsync(scoped,new(Search:"%"),now,ct);Check(search.TotalCount==1&&search.Items[0].Id==a.InternalRequestId,"keyword percent is literal");
            var page=await InternalRequestHistoryQueries.ListAsync(scoped,new(Page:2,PageSize:2),now,ct);Check(page.TotalCount==4&&page.Items.Count==2&&page.Items[0].Id==equal.InternalRequestId,"pagination retains scoped count");
            var timeline=await InternalRequestHistoryQueries.TimelineAsync(scoped,a.InternalRequestId,ct);Check(timeline?.Events.Count==2&&timeline.Events[0].Id<timeline.Events[1].Id,"timeline timestamp tie ordered by event ID");
            Check(await InternalRequestHistoryQueries.TimelineAsync(scoped,hidden.InternalRequestId,ct)==null,"foreign history withheld");
            Check(await InternalRequestHistoryQueries.TimelineAsync(scoped,long.MaxValue,ct)==null,"missing history indistinguishable");
            foreach(var filter in new[]{new InternalRequestHistoryFilter(Page:0),new InternalRequestHistoryFilter(PageSize:101),new InternalRequestHistoryFilter(Page:int.MaxValue,PageSize:100)})
            {bool rejected=false;try{await InternalRequestHistoryQueries.ListAsync(scoped,filter,now,ct);}catch(System.ComponentModel.DataAnnotations.ValidationException){rejected=true;}Check(rejected,"invalid pagination rejected");}
        }
        catch(Exception e){failure=e.GetBaseException().Message;}
        finally {await tx.RollbackAsync(CancellationToken.None);rollback=true;}
        await File.WriteAllTextAsync(report,JsonSerializer.Serialize(new {success=failure==null,checks,failure,rollback,scope="Internal query component over caller-scoped SQL, not authorization/HTTP/UC acceptance"},new JsonSerializerOptions{WriteIndented=true}),CancellationToken.None);
        Console.WriteLine($"IR history SQL: {(failure==null?"PASS":"FAIL")}; checks={checks.Count}; rollback={rollback}");return failure==null?0:1;
    }
}
