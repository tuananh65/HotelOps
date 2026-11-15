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

internal static class SprintInitialInspectionServiceProbe
{
    internal static async Task<object> VerifyAsync(HotelOpsDbContext db,long actor,long other,long branch,long task,long room,long version,
        string password,string rolePassword,CancellationToken ct)
    {
        var checks=new List<string>();
        var credentials=new PasswordCredentialService(Options.Create(new PasswordHasherOptions{CompatibilityMode=PasswordHasherCompatibilityMode.IdentityV3,IterationCount=PasswordCredentialService.BaselineIterationCount}));
        SessionReference ticket;long sid;
        await using(var loginTx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct))
        {
            var verified=await new CredentialLockoutService(db,credentials,TimeProvider.System).VerifyAsync(actor,password,
                (u,_)=>Task.FromResult(u.EmploymentStatus=="ACTIVE"&&u.AccountStatus=="ACTIVE"),ct);
            var login=await new SqlSessionService(db,new SessionRevocationService(db),TimeProvider.System).CreateSessionAsync(verified,ct);
            ticket=login.Ticket;sid=login.Session.SessionId;await loginTx.CommitAsync(ct);db.ChangeTracker.Clear();
        }
        var tag='"'+Convert.ToHexString(await db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.cleaning_tasks WHERE cleaning_task_id={task}").SingleAsync(ct))+'"';
        var cycle='"'+Convert.ToHexString(await db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.room_cleaning_cycle_state WHERE room_id={room}").SingleAsync(ct))+'"';
        var input=new InitialInspectionAssignmentRequest(other,version,cycle,DateTime.UtcNow.AddHours(1),"Synthetic initial assignment");
        async Task<RoomTypeBatchResult> Call(Guid operation,string? expected=null,InitialInspectionAssignmentRequest? body=null,long? targetBranch=null)
        {
            await using var context=SprintInitialInspectionProbe.Restricted(rolePassword);
            var current=new Identity(new(actor,sid,false));
            var service=new RoomTypeCreationService(context,current,new PermissionService(current,new EfPermissionDataSource(context),new UnresolvedPermissionResourceResolver(),TimeProvider.System),
                new SqlSessionService(context,new SessionRevocationService(context),TimeProvider.System),TimeProvider.System);
            return await service.AssignInitialInspectionAsync(ticket,sid,targetBranch??branch,task,expected??tag,operation.ToString("D"),body??input,ct);
        }
        async Task Denied(string name,RoomTypeBatchStatus status,InitialInspectionAssignmentRequest? body=null,string? expected=null,long? targetBranch=null)
        {
            var result=await Call(Guid.NewGuid(),expected,body,targetBranch);
            if(result.Status!=status)throw new Exception(name+": "+result.Status);
            if(await db.RoomInspections.AnyAsync(i=>i.CleaningTaskId==task,ct)
                ||await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.notification_operation_reservations WHERE actor_user_id={actor}").SingleAsync(ct)!=0)
                throw new Exception(name+" left mutation");
            checks.Add(name);
        }
        await Denied("stale task rejected",RoomTypeBatchStatus.Stale,expected:"\"FFFFFFFFFFFFFFFF\"");
        await Denied("stale cycle rejected",RoomTypeBatchStatus.Stale,input with{CycleETag="\"FFFFFFFFFFFFFFFF\""});
        await Denied("foreign branch rejected",RoomTypeBatchStatus.Forbidden,targetBranch:branch+100000);
        await Denied("unpublished selection rejected",RoomTypeBatchStatus.InvalidReference,input with{ChecklistVersionId=version+100000});
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_roles SET is_active=0 WHERE user_id={other}",ct);
        try{await Denied("selected inspector role revoked",RoomTypeBatchStatus.InvalidReference);}
        finally{await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_roles SET is_active=1 WHERE user_id={other}",ct);}
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={other}",ct);
        try{await Denied("selected inspector wrong branch",RoomTypeBatchStatus.InvalidReference);}
        finally{await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=NULL WHERE user_id={other}",ct);}
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.users SET account_status='INACTIVE' WHERE user_id={other}",ct);
        try{await Denied("inactive selected inspector",RoomTypeBatchStatus.InvalidReference);}
        finally{await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.users SET account_status='ACTIVE' WHERE user_id={other}",ct);}
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.cleaning_assignments(cleaning_task_id,assignee_user_id,assigned_by,assigned_at,assignment_status) VALUES({task},{other},{actor},SYSUTCDATETIME(),'ACTIVE')",ct);
        try{await Denied("cleaner inspector conflict",RoomTypeBatchStatus.InvalidReference);}
        finally{await db.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.cleaning_assignments WHERE cleaning_task_id={task} AND assignee_user_id={other}",ct);}
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.inspection_checklist_versions SET status='RETIRED' WHERE version_id={version}",ct);
        try{await Denied("retired checklist rejected",RoomTypeBatchStatus.InvalidReference);}
        finally{await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.inspection_checklist_versions SET status='PUBLISHED' WHERE version_id={version}",ct);}
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.audit_logs ADD CONSTRAINT CK_DEV3_InitialAssignmentFault CHECK(entity_type<>'INSPECTION_ASSIGNMENT')",ct);
        try
        {
            try{await Call(Guid.NewGuid());throw new Exception("Audit rollback fault missing");}
            catch(SqlException e)when(e.Number==547){}
            if(await db.RoomInspections.AnyAsync(i=>i.CleaningTaskId==task,ct)
                ||await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.notification_operation_reservations WHERE actor_user_id={actor}").SingleAsync(ct)!=0)
                throw new Exception("Failed audit leaked assignment or intent");
            checks.Add("audit failure rolls back inspection, operation and notification intent");
        }
        finally{await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.audit_logs DROP CONSTRAINT CK_DEV3_InitialAssignmentFault",ct);}
        input=input with{InspectorId=actor};
        var operation=Guid.NewGuid();var results=await Task.WhenAll(Call(operation),Call(operation));
        if(results.Any(r=>r.Status!=RoomTypeBatchStatus.Success||r.Value is not InitialInspectionAssignmentResult))
            throw new Exception("Concurrent assignment failed: "+string.Join(',',results.Select(r=>r.Status)));
        var first=(InitialInspectionAssignmentResult)results[0].Value!;
        if(first.Id!=((InitialInspectionAssignmentResult)results[1].Value!).Id
            ||await db.RoomInspections.CountAsync(i=>i.CleaningTaskId==task,ct)!=1)throw new Exception("Concurrent replay duplicated inspection");
        if((await Call(operation,body:input with{Reason="Different body"})).Status!=RoomTypeBatchStatus.Duplicate
            ||(await Call(Guid.NewGuid())).Status!=RoomTypeBatchStatus.Duplicate)throw new Exception("Duplicate assignment admitted");
        checks.Add("concurrent same operation returns one immutable assignment; new key or changed request rejected");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={actor} AND branch_id={branch}",ct);
        try{if((await Call(operation)).Status==RoomTypeBatchStatus.Success)throw new Exception("Replay retained revoked branch");checks.Add("replay rechecks current actor branch access");}
        finally{await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=NULL WHERE user_id={actor} AND branch_id={branch}",ct);}
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.inspection_checklist_versions SET status='RETIRED' WHERE version_id={version}",ct);
        try
        {
            await using var context=SprintInitialInspectionProbe.Restricted(rolePassword);
            var identity=new Identity(new(actor,sid,false));
            var service=new RoomTypeCreationService(context,identity,new PermissionService(identity,new EfPermissionDataSource(context),new UnresolvedPermissionResourceResolver(),TimeProvider.System),
                new SqlSessionService(context,new SessionRevocationService(context),TimeProvider.System),TimeProvider.System);
            var existing=await service.ReadInitialInspectionOptionsAsync(ticket,sid,branch,task,ct);
            if(existing.Value is not InitialInspectionOptions options || options.ExistingInspectionId!=first.Id || options.ChecklistVersionId!=version || options.CanAssign)
                throw new Exception("Assigned checklist was lost or remapped after retirement");
            checks.Add("existing assignment retains exact version after retirement and offers no duplicate");
        }
        finally { /* Retirement is irreversible while assigned; restore only after owned assignment cleanup below. */ }
        async Task<RoomTypeBatchResult> Execute()
        {
            await using var context=SprintInitialInspectionProbe.Restricted(rolePassword);
            var current=new Identity(new(actor,sid,false));
            var service=new RoomTypeCreationService(context,current,new PermissionService(current,new EfPermissionDataSource(context),new UnresolvedPermissionResourceResolver(),TimeProvider.System),
                new SqlSessionService(context,new SessionRevocationService(context),TimeProvider.System),TimeProvider.System);
            return await service.ExecuteInspectionAsync(ticket,sid,branch,first.Id,first.SourceETag,"start",null,null,"Synthetic current cycle guard",SprintReworkFixture.EvidenceRoot,ct);
        }
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.room_cleaning_cycle_state WHERE room_id={room} AND cleaning_task_id={task}",ct);
        try{if((await Execute()).Status!=RoomTypeBatchStatus.Inactive)throw new Exception("Obsolete cycle inspection started");checks.Add("assigned inspection cannot start after current cycle is removed");}
        finally{await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.room_cleaning_cycle_state(room_id,cleaning_task_id) VALUES({room},{task})",CancellationToken.None);}
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_roles SET is_active=0 WHERE user_id={actor}",ct);
        try{if((await Execute()).Status==RoomTypeBatchStatus.Success)throw new Exception("Lost supervisor executed inspection");checks.Add("execution rechecks current supervisor role");}
        finally{await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_roles SET is_active=1 WHERE user_id={actor}",CancellationToken.None);}
        if(await db.Database.SqlQuery<string>($"SELECT workflow_status AS Value FROM dbo.room_inspections WHERE inspection_id={first.Id}").SingleAsync(ct)!="ASSIGNED"
            ||await db.AuditLogs.AnyAsync(a=>a.EntityType=="room_inspections"&&a.EntityId==first.Id.ToString(),ct))throw new Exception("Denied execution changed state/audit");
        var reassignment=await SprintInspectionReassignmentServiceProbe.VerifyAsync(db,ticket,sid,other,branch,first.Id,rolePassword,ct);
        await using(var reset=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct))
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                DELETE dbo.audit_logs WHERE actor_user_id={actor} AND entity_type='INSPECTION_ASSIGNMENT' AND entity_id=CONVERT(nvarchar(50),{first.Id});
                DELETE dbo.audit_logs WHERE actor_user_id={actor} AND entity_type='room_inspections' AND entity_id=CONVERT(nvarchar(50),{first.Id});
                DELETE dbo.notification_operation_reservations WHERE actor_user_id={actor};
                DELETE dbo.room_inspections WHERE inspection_id={first.Id} AND cleaning_task_id={task} AND workflow_status='INSPECTING';
                UPDATE dbo.inspection_checklist_versions SET status='PUBLISHED' WHERE version_id={version};
                """,ct);
            await reset.CommitAsync(ct);
        }
        return new{serviceSqlChecks=checks,checksPassed=checks.Count,reassignment};
    }
    private sealed class Identity(CurrentUserInfo value):ICurrentUserAccessor
    {public bool TryGetCurrentUser(out CurrentUserInfo user){user=value;return true;}}
}
