using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

internal static class SprintReinspectionReassignmentServiceProbe
{
    internal static async Task<object> VerifyAsync(HotelOpsDbContext db, SessionReference ticket, long sid, long other,
        long branch, long inspection, CancellationToken ct)
    {
        var actor = ticket.UserId;
        var checks = new List<string>();
        async Task<HotelOpsDbContext> Context()
        {
            var context=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()+";Pooling=false").Options);
            await context.Database.OpenConnectionAsync(ct);
            await context.Database.ExecuteSqlRawAsync("EXECUTE AS USER='DEV3_Reinspector'",ct);
            return context;
        }
        RoomTypeCreationService Service(HotelOpsDbContext context)
        {
            var current = new Identity(new(actor,sid,false));
            return new(context,current,new PermissionService(current,new EfPermissionDataSource(context),new UnresolvedPermissionResourceResolver(),TimeProvider.System),
                new SqlSessionService(context,new SessionRevocationService(context),TimeProvider.System),TimeProvider.System);
        }
        async Task<ReinspectionReassignmentOptions> Options()
        {
            await using var context = await Context();
            var result = await Service(context).ReadReinspectionReassignmentAsync(ticket,sid,branch,inspection,SprintReworkFixture.EvidenceRoot,ct);
            return result.Value as ReinspectionReassignmentOptions ?? throw new Exception("Reassignment options: "+result.Status);
        }
        async Task<RoomTypeBatchResult> Call(ReinspectionReassignmentOptions source, long selected, Guid operation, string? reason=null, long? targetBranch=null)
        {
            await using var context = await Context();
            return await Service(context).ReassignReinspectionAsync(ticket,sid,targetBranch??branch,inspection,source.SourceETag,operation.ToString("D"),
                new(selected,source.CycleETag,source.ReworkETag,source.BindingETag,reason??"Synthetic reassignment"),SprintReworkFixture.EvidenceRoot,ct);
        }
        var source = await Options();
        async Task Denied(string name, RoomTypeBatchStatus expected, ReinspectionReassignmentOptions? snapshot=null, long? targetBranch=null)
        {
            var before = await Options();
            var result = await Call(snapshot??source,other,Guid.NewGuid(),targetBranch:targetBranch);
            if(result.Status!=expected || (await Options()).SourceETag!=before.SourceETag) throw new Exception(name+": "+result.Status);
            checks.Add(name);
        }
        await Denied("stale inspection",RoomTypeBatchStatus.Stale,source with { SourceETag="\"FFFFFFFFFFFFFFFF\"" });
        await Denied("stale cleaning cycle",RoomTypeBatchStatus.Stale,source with { CycleETag="\"FFFFFFFFFFFFFFFF\"" });
        await Denied("stale binding",RoomTypeBatchStatus.Stale,source with { BindingETag="\"FFFFFFFFFFFFFFFF\"" });
        await Denied("stale rework",RoomTypeBatchStatus.Stale,source with { ReworkETag="\"FFFFFFFFFFFFFFFF\"" });
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
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.cleaning_assignments(cleaning_task_id,assignee_user_id,assigned_by,assigned_at,assignment_status) VALUES({source.TaskId},{other},{actor},SYSUTCDATETIME(),'COMPLETED')",ct);
        try { await Denied("cleaner conflict",RoomTypeBatchStatus.InvalidReference); }
        finally { await db.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.cleaning_assignments WHERE cleaning_task_id={source.TaskId} AND assignee_user_id={other}",CancellationToken.None); }
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.audit_logs ADD CONSTRAINT CK_DEV3_ReinspectionReassignmentFault CHECK(entity_type<>'REINSPECTION_ASSIGNMENT' OR action<>'UPDATE')",ct);
        var faultKey=Guid.NewGuid();
        try
        {
            try { await Call(source,other,faultKey); throw new Exception("Expected reassignment audit fault"); }
            catch(SqlException e) when(e.Number==547) { }
            if((await Options()).SourceETag!=source.SourceETag || await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.notification_operation_reservations WHERE actor_user_id={actor} AND operation_key={faultKey}").SingleAsync(ct)!=0)
                throw new Exception("Failed audit leaked inspector/intent/operation");
            checks.Add("audit failure rolls back inspector, version and durable intent");
        }
        finally { await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.audit_logs DROP CONSTRAINT CK_DEV3_ReinspectionReassignmentFault",CancellationToken.None); }
        var operation=Guid.NewGuid();
        var results=await Task.WhenAll(Call(source,other,operation),Call(source,other,operation));
        if(results.Any(r=>r.Status!=RoomTypeBatchStatus.Success || r.Value is not ReinspectionReassignmentResult)) throw new Exception("Concurrent reassignment: "+string.Join(',',results.Select(r=>r.Status)));
        var result=(ReinspectionReassignmentResult)results[0].Value!;
        if(result!=((ReinspectionReassignmentResult)results[1].Value!) || result.PreviousInspectorId!=actor || result.InspectorId!=other)
            throw new Exception("Concurrent receipt mismatch");
        if((await Call(source,other,operation,"Changed payload")).Status!=RoomTypeBatchStatus.Duplicate
            ||(await Call(source,other,Guid.NewGuid())).Status!=RoomTypeBatchStatus.Stale)throw new Exception("Duplicate or stale reassignment admitted");
        var after=await Options();
        if(after.VersionId!=source.VersionId || after.DueAt!=source.DueAt || after.State!=source.State || after.InspectorId!=other
            ||after.BindingETag!=source.BindingETag || after.ReworkETag!=source.ReworkETag || after.CycleETag!=source.CycleETag || after.SourceETag==source.SourceETag)throw new Exception("Reassignment changed lifecycle/version/deadline");
        checks.Add("concurrent same operation emits one audit and immutable receipt; changed payload/stale source denied");
        await using(var context=await Context())
        {
            var denied=await Service(context).ReadReinspectionExecutionAsync(ticket,sid,branch,inspection,SprintReworkFixture.EvidenceRoot,ct);
            if(denied.Status==RoomTypeBatchStatus.Success)throw new Exception("Previous inspector retained execution access");
        }
        checks.Add("previous inspector loses execution access");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_inspections SET inspector_user_id={actor} WHERE inspection_id={inspection}",ct);
        try {
            if(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.fe05_reinspection_responsibilities_v1 WHERE inspection_id={inspection}").SingleAsync(ct)!=0)
                throw new Exception("Raw inspector substitution acquired authority");
        } finally { await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_inspections SET inspector_user_id={other} WHERE inspection_id={inspection}",CancellationToken.None); }
        var transferAudit=await db.AuditLogs.AsNoTracking().SingleAsync(a=>a.EntityType=="REINSPECTION_ASSIGNMENT"&&a.EntityId==inspection.ToString(),ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.audit_logs SET new_values='[]' WHERE audit_id={transferAudit.AuditId}",ct);
        try {
            if(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.fe05_reinspection_responsibilities_v1 WHERE inspection_id={inspection}").SingleAsync(ct)!=0)
                throw new Exception("Tampered transfer retained authority");
        } finally { await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.audit_logs SET new_values={transferAudit.NewValues} WHERE audit_id={transferAudit.AuditId}",CancellationToken.None); }
        checks.Add("raw inspector substitution and tampered event fail closed; original binding retained");
        after=await Options();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={actor}",ct);
        try { if((await Call(source,other,operation)).Status==RoomTypeBatchStatus.Success)throw new Exception("Revoked actor replay admitted"); }
        finally { await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=NULL WHERE user_id={actor}",CancellationToken.None); }
        checks.Add("replay rechecks current actor access");
        if((await Call(after,actor,Guid.NewGuid())).Status!=RoomTypeBatchStatus.Success)throw new Exception("Return assignment failed");
        if(await db.AuditLogs.CountAsync(a=>a.EntityType=="REINSPECTION_ASSIGNMENT"&&a.Action=="UPDATE"&&a.EntityId==inspection.ToString(),ct)!=2)
            throw new Exception("Reassignment audit dedup mismatch");
        return new { reassignmentServiceSqlChecks=checks,checksPassed=checks.Count };
    }
    private sealed class Identity(CurrentUserInfo value):ICurrentUserAccessor
    {public bool TryGetCurrentUser(out CurrentUserInfo user){user=value;return true;}}
}
