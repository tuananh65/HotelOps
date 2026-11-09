using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

// Runnable isolated SQL GRAPH COMPONENT probe, not FE05 business/recipient acceptance.
// Does not invoke DEV1 writers, commit domain changes, create fixtures or install schema.
internal static class N01SqlComponentProbe
{
    internal sealed record Fixture(string Prefix,long BranchId,long InspectionId,long ReworkId,long[] RecipientIds);
    internal static void RequireLease(string lease)
    {
        if(Path.GetFileName(lease)!="SQL_TRIAL_EXCLUSIVE.lock" || !File.Exists(lease))throw new InvalidOperationException("Approved central exclusive lease required.");
        try{using var probe=File.Open(lease,FileMode.Open,FileAccess.ReadWrite,FileShare.None);throw new InvalidOperationException("Central lease not held.");}
        catch(IOException e)when((e.HResult&0xffff)is 32 or 33){}
    }
    private sealed class Execution(string lease):IFe05TrialExecutionAuthorization
    {public Task RequireAsync(CancellationToken ct){ct.ThrowIfCancellationRequested();RequireLease(lease);return Task.CompletedTask;}}
    internal static async Task RunAsync(string fixtureFile,string lease,CancellationToken ct)
    {
        RequireLease(lease); // before configuration or SQL
        var f=JsonSerializer.Deserialize<Fixture>(await File.ReadAllTextAsync(fixtureFile,ct))??throw new InvalidOperationException("Fixture required");
        if(f.Prefix.Length is <13 or >20 || !f.Prefix.StartsWith("N0106-",StringComparison.Ordinal) ||
            f.Prefix.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='-') || f.RecipientIds.Length is <2 or >200 ||
            f.RecipientIds.Any(x=>x<=0) || f.RecipientIds.Distinct().Count()!=f.RecipientIds.Length)
            throw new InvalidOperationException("Unique owned N0106- fixture and at least two owned recipients required.");
        var config=new ConfigurationBuilder().AddUserSecrets("HotelOps.Dev2.FullStack.Trial.Integration02").Build();
        var connection=IntegrationSqlGuard.ValidateConnectionString(config.GetConnectionString("HotelOpsDb"));
        await using var db=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection)
            .AddInterceptors(new IntegrationTrialConnectionInterceptor()).Options);
        await db.Database.OpenConnectionAsync(ct);
        await IntegrationSqlGuard.VerifyOpenConnectionAsync(db.Database.GetDbConnection(),ct);
        var operation=Guid.NewGuid();var publications=new List<Guid>();long actor=0;
        await using(var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct))
        {
            try
            {
                if(!await db.Branches.AsNoTracking().AnyAsync(b=>b.BranchId==f.BranchId&&b.BranchCode.StartsWith(f.Prefix),ct) ||
                    !await db.RoomInspections.AsNoTracking().AnyAsync(i=>i.InspectionId==f.InspectionId&&i.InspectionNo.StartsWith(f.Prefix)&&
                        i.Room.BranchId==f.BranchId&&i.Room.RoomNumber.StartsWith(f.Prefix),ct))throw new InvalidOperationException("Owned source fixture missing");
                foreach(var id in f.RecipientIds)
                    if(!await db.Users.AsNoTracking().AnyAsync(u=>u.UserId==id&&u.Username.StartsWith(f.Prefix),ct))throw new InvalidOperationException("Owned recipient fixture missing");
                var s=await db.Database.SqlQuery<SourceRow>($"""
                    SELECT i.inspector_user_id AS ActorId,i.room_id AS RoomId,i.cleaning_task_id AS TaskId,
                      i.version_id AS VersionId,i.row_version AS Revision,w.row_version AS ReworkRevision,i.inspected_at AS InspectedAt,w.requested_at AS RequestedAt
                    FROM dbo.room_inspections i WITH(UPDLOCK,HOLDLOCK)
                    JOIN dbo.cleaning_tasks t WITH(UPDLOCK,HOLDLOCK) ON t.cleaning_task_id=i.cleaning_task_id AND t.room_id=i.room_id
                    JOIN dbo.room_cleaning_cycle_state c WITH(UPDLOCK,HOLDLOCK) ON c.room_id=i.room_id AND c.cleaning_task_id=i.cleaning_task_id
                    JOIN dbo.cleaning_reworks w WITH(UPDLOCK,HOLDLOCK) ON w.inspection_id=i.inspection_id AND w.cleaning_task_id=i.cleaning_task_id
                    WHERE i.inspection_id={f.InspectionId} AND w.rework_id={f.ReworkId} AND i.status='COMPLETED' AND i.result='FAIL'
                      AND i.workflow_status='FAILED' AND t.workflow_status='REWORK_REQUIRED' AND w.status='PENDING' AND w.workflow_status='PENDING_ACCEPTANCE'
                    """).SingleAsync(ct);
                actor=s.ActorId;
                if(!await db.Users.AsNoTracking().AnyAsync(u=>u.UserId==actor&&u.Username.StartsWith(f.Prefix),ct))throw new InvalidOperationException("Owned actor required");
                var results=await db.InspectionResults.AsNoTracking().Where(r=>r.InspectionId==f.InspectionId&&r.Outcome=="FAIL"&&r.ChecklistItem.VersionId==s.VersionId).Select(r=>r.ResultId).ToArrayAsync(ct);
                var request=new Fe05OperationRequest(operation,f.BranchId,f.InspectionId,new string('A',64),[Fe05NotificationKind.InspectionFailed,Fe05NotificationKind.CleaningReworkRequested]);
                var hash=Fe05OperationIdentity.Fingerprint(request);
                var store=new Fe05Fs013CandidateStore(db,new Execution(lease));
                await store.RequireAvailableAsync(ct);
                if(await store.LockAsync(actor,operation,ct)is not null)throw new InvalidOperationException("New random operation already exists");
                await store.ReserveAsync(actor,operation,hash,request.RequestFingerprint,ct);
                var receipts=new List<Fe05PublicationReceipt>();
                foreach(var kind in request.RequiredEvents)
                {
                    var source=new Fe05NotificationSource(actor,f.BranchId,s.RoomId,f.InspectionId,s.TaskId,s.VersionId,
                        kind==Fe05NotificationKind.CleaningReworkRequested?f.ReworkId:null,'"'+Convert.ToHexString(s.Revision)+'"',s.TaskId,
                        "COMPLETED","FAIL","FAILED",new DateTimeOffset(DateTime.SpecifyKind(kind==Fe05NotificationKind.InspectionFailed?s.InspectedAt:s.RequestedAt,DateTimeKind.Utc)),results,
                        kind==Fe05NotificationKind.CleaningReworkRequested?'"'+Convert.ToHexString(s.ReworkRevision)+'"':null);
                    // Explicit trial-only recipient facts exercise SQL fan-out, NOT accepted source-read policy.
                    var candidate=Fe05NotificationMapping.Map(new(kind,operation,f.BranchId,f.InspectionId,source.Revision),source,actor,
                        f.RecipientIds.Select(id=>new Fe05Recipient(id,f.BranchId,true,true)).ToArray());
                    var publication=Guid.NewGuid();publications.Add(publication);
                    await store.WritePublicationAsync(candidate,publication,ct);
                    receipts.Add(new(publication,candidate.EventType,1,candidate.Fingerprint,candidate.RecipientIds));
                }
                var receipt=new Fe05OperationReceipt(Fe05OperationReceipt.ContractName,actor,operation,request.RequestFingerprint,receipts);
                await store.CompleteAsync(actor,operation,hash,receipt,ct);
                await store.VerifyAsync(actor,operation,hash,receipt,ct);
                var replay=await store.LockAsync(actor,operation,ct);
                if(replay is not{Completed:false,DomainResultJson:null,N01ReceiptJson:not null} || replay.Fingerprint!=hash)
                    throw new InvalidOperationException("N01 receipt must not replace owner domain receipt or completion");
            }
            finally{await tx.RollbackAsync(CancellationToken.None);}
        }
        if(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.notification_operation_reservations WHERE actor_user_id={actor} AND operation_key={operation}").SingleAsync(ct)!=0)
            throw new InvalidOperationException("Rolled-back operation remains");
        foreach(var p in publications)
            if(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.notification_publications WHERE publication_id={p}").SingleAsync(ct)!=0 ||
                await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.notification_delivery_intents WHERE publication_id={p}").SingleAsync(ct)!=0)
                throw new InvalidOperationException("Rolled-back publication/fan-out remains");
        Console.WriteLine(JsonSerializer.Serialize(new{pass=true,scope="Isolated FS013 N01 graph component only",events=2,recipientCount=f.RecipientIds.Length,
            receiptReread="PASS",graphRollbackAndOwnedCleanup="PASS",domainWrites=0,businessAcceptance="NOT_RUN",concurrency="NOT_RUN",committedCrashRecovery="NOT_RUN",mandatoryRecipientPolicy="NOT_ACCEPTED"}));
    }
    private sealed class SourceRow
    {public long ActorId{get;set;}public long RoomId{get;set;}public long TaskId{get;set;}public long VersionId{get;set;}
     public byte[] Revision{get;set;}=[];public byte[] ReworkRevision{get;set;}=[];public DateTime InspectedAt{get;set;}public DateTime RequestedAt{get;set;}}
}
