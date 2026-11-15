using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

internal static class SprintInspectionReassignmentServiceProbe
{
    internal static async Task<object> VerifyAsync(HotelOpsDbContext db, SessionReference ticket, long sid, long other,
        long branch, long inspection, string rolePassword, CancellationToken ct)
    {
        var actor = ticket.UserId;
        var checks = new List<string>();
        RoomTypeCreationService Service(HotelOpsDbContext context)
        {
            var current = new Identity(new(actor,sid,false));
            return new(context,current,new PermissionService(current,new EfPermissionDataSource(context),new UnresolvedPermissionResourceResolver(),TimeProvider.System),
                new SqlSessionService(context,new SessionRevocationService(context),TimeProvider.System),TimeProvider.System);
        }
        async Task<InspectionReassignmentOptions> Options()
        {
            await using var context = SprintInitialInspectionProbe.Restricted(rolePassword);
            var result = await Service(context).ReadInspectionReassignmentAsync(ticket,sid,branch,inspection,ct);
            return result.Value as InspectionReassignmentOptions ?? throw new Exception("Reassignment options: "+result.Status);
        }
        async Task<RoomTypeBatchResult> Call(InspectionReassignmentOptions source, long selected, Guid operation, string? reason=null, long? targetBranch=null)
        {
            await using var context = SprintInitialInspectionProbe.Restricted(rolePassword);
            return await Service(context).ReassignInspectionAsync(ticket,sid,targetBranch??branch,inspection,source.SourceETag,operation.ToString("D"),
                new(selected,source.CycleETag,reason??"Synthetic reassignment"),ct);
        }
        var source = await Options();
        async Task Denied(string name, RoomTypeBatchStatus expected, InspectionReassignmentOptions? snapshot=null, long? targetBranch=null)
        {
            var before = await Options();
            var result = await Call(snapshot??source,other,Guid.NewGuid(),targetBranch:targetBranch);
            if(result.Status!=expected || (await Options()).SourceETag!=before.SourceETag) throw new Exception(name+": "+result.Status);
            checks.Add(name);
        }
        await Denied("stale inspection",RoomTypeBatchStatus.Stale,source with { SourceETag="\"FFFFFFFFFFFFFFFF\"" });
        await Denied("stale cleaning cycle",RoomTypeBatchStatus.Stale,source with { CycleETag="\"FFFFFFFFFFFFFFFF\"" });
        await Denied("foreign branch",RoomTypeBatchStatus.Forbidden,targetBranch:branch+100000);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_roles SET is_active=0 WHERE user_id={other}",ct);
        try { await Denied("revoked selected role",RoomTypeBatchStatus.InvalidReference); }
        finally { await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_roles SET is_active=1 WHERE user_id={other}",CancellationToken.None); }
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={other}",ct);
        try { await Denied("selected wrong branch",RoomTypeBatchStatus.InvalidReference); }
        finally { await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=NULL WHERE user_id={other}",CancellationToken.None); }
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.users SET account_status='INACTIVE' WHERE user_id={other}",ct);
        try { await Denied("inactive selected inspector",RoomTypeBatchStatus.InvalidReference); }
        finally { await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.users SET account_status='ACTIVE' WHERE user_id={other}",CancellationToken.None); }
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.cleaning_assignments(cleaning_task_id,assignee_user_id,assigned_by,assigned_at,assignment_status) VALUES({source.TaskId},{other},{actor},SYSUTCDATETIME(),'ACTIVE')",ct);
        try { await Denied("cleaner conflict",RoomTypeBatchStatus.InvalidReference); }
        finally { await db.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.cleaning_assignments WHERE cleaning_task_id={source.TaskId} AND assignee_user_id={other}",CancellationToken.None); }
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.audit_logs ADD CONSTRAINT CK_DEV3_ReassignmentFault CHECK(entity_type<>'INSPECTION_ASSIGNMENT' OR action<>'UPDATE')",ct);
        var faultKey=Guid.NewGuid();
        try
        {
            try { await Call(source,other,faultKey); throw new Exception("Expected reassignment audit fault"); }
            catch(SqlException e) when(e.Number==547) { }
            if((await Options()).SourceETag!=source.SourceETag || await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.notification_operation_reservations WHERE actor_user_id={actor} AND operation_key={faultKey}").SingleAsync(ct)!=0)
                throw new Exception("Failed audit leaked inspector/intent/operation");
            checks.Add("audit failure rolls back inspector, version and durable intent");
        }
        finally { await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.audit_logs DROP CONSTRAINT CK_DEV3_ReassignmentFault",CancellationToken.None); }
        var operation=Guid.NewGuid();
        var results=await Task.WhenAll(Call(source,other,operation),Call(source,other,operation));
        if(results.Any(r=>r.Status!=RoomTypeBatchStatus.Success || r.Value is not InspectionReassignmentResult)) throw new Exception("Concurrent reassignment: "+string.Join(',',results.Select(r=>r.Status)));
        var result=(InspectionReassignmentResult)results[0].Value!;
        if(result!=((InspectionReassignmentResult)results[1].Value!) || result.PreviousInspectorId!=actor || result.InspectorId!=other)
            throw new Exception("Concurrent receipt mismatch");
        if((await Call(source,other,operation,"Changed payload")).Status!=RoomTypeBatchStatus.Duplicate
            ||(await Call(source,other,Guid.NewGuid())).Status!=RoomTypeBatchStatus.Stale)throw new Exception("Duplicate or stale reassignment admitted");
        var after=await Options();
        if(after.VersionId!=source.VersionId || after.DueAt!=source.DueAt || after.State!=source.State || after.InspectorId!=other
            ||after.CycleETag!=source.CycleETag || after.SourceETag==source.SourceETag)throw new Exception("Reassignment changed lifecycle/version/deadline");
        checks.Add("concurrent same operation emits one audit and immutable receipt; changed payload/stale source denied");
        await using(var context=SprintInitialInspectionProbe.Restricted(rolePassword))
        {
            var denied=await Service(context).ExecuteInspectionAsync(ticket,sid,branch,inspection,after.SourceETag,"read",null,null,null,SprintReworkFixture.EvidenceRoot,ct);
            if(denied.Status!=RoomTypeBatchStatus.Forbidden)throw new Exception("Previous inspector retained execution access");
        }
        checks.Add("previous inspector loses execution access");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={actor}",ct);
        try { if((await Call(source,other,operation)).Status==RoomTypeBatchStatus.Success)throw new Exception("Revoked actor replay admitted"); }
        finally { await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=NULL WHERE user_id={actor}",CancellationToken.None); }
        checks.Add("replay rechecks current actor access");
        if((await Call(after,actor,Guid.NewGuid())).Status!=RoomTypeBatchStatus.Success)throw new Exception("Return assignment failed");
        if(await db.AuditLogs.CountAsync(a=>a.EntityType=="INSPECTION_ASSIGNMENT"&&a.Action=="UPDATE"&&a.EntityId==inspection.ToString(),ct)!=2)
            throw new Exception("Reassignment audit dedup mismatch");
        source=await Options();
        await using(var context=SprintInitialInspectionProbe.Restricted(rolePassword))
        {
            var started=await Service(context).ExecuteInspectionAsync(ticket,sid,branch,inspection,source.SourceETag,"start",null,null,"Synthetic inspecting transfer",SprintReworkFixture.EvidenceRoot,ct);
            if(started.Status!=RoomTypeBatchStatus.Success)throw new Exception("Start before inspecting transfer failed: "+started.Status);
        }
        source=await Options();
        if(source.State!="INSPECTING" || (await Call(source,other,Guid.NewGuid())).Status!=RoomTypeBatchStatus.Success)throw new Exception("Inspecting transfer failed");
        after=await Options();
        if(after.State!="INSPECTING" || after.VersionId!=source.VersionId || after.DueAt!=source.DueAt
            || (await Call(after,actor,Guid.NewGuid())).Status!=RoomTypeBatchStatus.Success)throw new Exception("Inspecting lifecycle changed");
        checks.Add("INSPECTING transfers preserve lifecycle, retired assigned version and original deadline");
        return new { reassignmentServiceSqlChecks=checks,checksPassed=checks.Count };
    }
    private sealed class Identity(CurrentUserInfo value):ICurrentUserAccessor
    {public bool TryGetCurrentUser(out CurrentUserInfo user){user=value;return true;}}
}
