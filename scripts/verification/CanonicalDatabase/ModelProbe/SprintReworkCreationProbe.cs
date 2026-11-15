using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

internal static class SprintReworkCreationProbe
{
    // Called only inside the identity-guarded synthetic fixture, after its original round is verified.
    internal static async Task VerifyAsync(HotelOpsDbContext db,long actor,string password,long branch,long inspection,long cleaner,long original,CancellationToken ct)
    {
        var credentials=new PasswordCredentialService(Options.Create(new PasswordHasherOptions
        {CompatibilityMode=PasswordHasherCompatibilityMode.IdentityV3,IterationCount=PasswordCredentialService.BaselineIterationCount}));
        var sessions=new SqlSessionService(db,new SessionRevocationService(db),TimeProvider.System);
        SessionReference ticket;long sid;
        await using(var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct))
        {
            var verified=await new CredentialLockoutService(db,credentials,TimeProvider.System).VerifyAsync(actor,password,
                (u,_)=>Task.FromResult(u.EmploymentStatus=="ACTIVE"&&u.AccountStatus=="ACTIVE"),ct);
            var login=await sessions.CreateSessionAsync(verified,ct);ticket=login.Ticket;sid=login.Session.SessionId;
            // Fixture-only historical round, so new requests exercise the real creation transaction.
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_reworks SET status='COMPLETED',workflow_status='COMPLETED',completed_at=SYSUTCDATETIME() WHERE rework_id={original}",ct);
            await tx.CommitAsync(ct);db.ChangeTracker.Clear();
        }
        var revision=await db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.room_inspections WHERE inspection_id={inspection}").SingleAsync(ct);
        var defects=await db.InspectionDefects.Where(d=>d.InspectionId==inspection).Select(d=>d.DefectId).ToArrayAsync(ct);
        var request=new RequestReworkRequest(cleaner,DateTime.UtcNow.AddHours(1),defects,"Synthetic atomic creation probe");
        var tag='"'+Convert.ToHexString(revision)+'"';
        async Task<RoomTypeBatchStatus> Create()
        {
            await using var context=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()).Options);
            var identity=new Identity(new(actor,sid,false));
            var service=new RoomTypeCreationService(context,identity,new PermissionService(identity,new EfPermissionDataSource(context),new UnresolvedPermissionResourceResolver(),TimeProvider.System),
                new SqlSessionService(context,new SessionRevocationService(context),TimeProvider.System),TimeProvider.System);
            return (await service.RequestCleaningReworkAsync(ticket,sid,branch,inspection,tag,request,ct,SprintReworkFixture.EvidenceRoot)).Status;
        }
        async Task<string> Snapshot()=>string.Join(":",await db.Database.SqlQueryRaw<int>("""
            SELECT COUNT(*) AS Value FROM dbo.cleaning_reworks
            UNION ALL SELECT COUNT(*) FROM dbo.cleaning_rework_defects
            UNION ALL SELECT COUNT(*) FROM dbo.fs016_rework_source_proofs
            UNION ALL SELECT COUNT(*) FROM dbo.notification_operation_reservations
            UNION ALL SELECT COUNT(*) FROM dbo.audit_logs WHERE entity_type='cleaning_reworks'
            UNION ALL SELECT COUNT(*) FROM dbo.notifications WHERE source_type='CLEANING_REWORK'
            """).ToArrayAsync(ct));
        var before=await Snapshot();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER dbo.TR_DEV3_ReworkAuditFault ON dbo.audit_logs AFTER INSERT AS
            BEGIN
              IF EXISTS(SELECT 1 FROM inserted WHERE entity_type='cleaning_reworks' AND action='CREATE')
                THROW 51989,'Synthetic rework audit failure',1;
            END
            """,ct);
        try
        {
            try{await Create();throw new Exception("Expected creation audit fault was not raised");}
            catch(SqlException error) when(error.Number==51989){}
            if(await Snapshot()!=before)throw new Exception("Creation rollback leaked source, links, authority, receipt, audit or notification");
        }
        finally{await db.Database.ExecuteSqlRawAsync("DROP TRIGGER dbo.TR_DEV3_ReworkAuditFault",ct);}
        var results=await Task.WhenAll(Create(),Create());
        if(results.Count(x=>x==RoomTypeBatchStatus.Success)!=1||results.Count(x=>x==RoomTypeBatchStatus.Duplicate)!=1)
            throw new Exception("Concurrent creation mismatch: "+string.Join(",",results));
        var created=await db.CleaningReworks.AsNoTracking().SingleAsync(w=>w.InspectionId==inspection&&w.ReworkId!=original,ct);
        if(created.Status!="PENDING"||created.AssigneeUserId!=cleaner
            ||await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.fs016_rework_source_proofs WHERE rework_id={created.ReworkId}").SingleAsync(ct)!=1
            ||await db.AuditLogs.CountAsync(a=>a.EntityType=="cleaning_reworks"&&a.EntityId==created.ReworkId.ToString()&&a.Action=="CREATE",ct)!=1)
            throw new Exception("Concurrent winner did not persist exactly one authority and audit");
    }
    private sealed class Identity(CurrentUserInfo value):ICurrentUserAccessor
    {public bool TryGetCurrentUser(out CurrentUserInfo user){user=value;return true;}}
}
