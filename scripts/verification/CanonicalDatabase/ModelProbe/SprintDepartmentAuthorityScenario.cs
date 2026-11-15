using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.InternalRequests;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

internal static class SprintDepartmentAuthorityScenario
{
    internal static async Task RunAsync(HotelOpsDbContext db,long actor,List<object> checks,CancellationToken ct,string? username=null,string? password=null,bool wholeClone=false,HttpClient? http=null)
    {
        // SprintAuthProbe already verified exact disposable database GUID/run and empty personnel.
        var prefix="D3AUTH"+Guid.NewGuid().ToString("N")[..8];
        var branches=new[]{new Branch{BranchCode=prefix+"A",BranchName="Synthetic authority A",IsActive=true},
            new Branch{BranchCode=prefix+"B",BranchName="Synthetic authority B",IsActive=true}};
        var type=new InternalRequestType{TypeCode=prefix,TypeName="Synthetic authority",IsActive=true};
        long grantId=0;long[] requests=[];
        var authority=new InternalRequestProcessAuthority(db);
        void Check(bool value,string label){if(!value)throw new Exception(label);checks.Add(new{departmentAuthorityCheck=label});}
        async Task AssertAllowed(long request,bool expected,string label)
        {
            await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
            Check(await authority.AllowsCurrentRequestAsync(actor,request,ct)==expected,label);
            await tx.RollbackAsync(ct);
        }
        try
        {
            if(!wholeClone)
            {
                var script=await File.ReadAllTextAsync("scripts/verification/CanonicalDatabase/DepartmentManagementIsolated.sql",ct);
                await db.Database.ExecuteSqlRawAsync(script,ct);await db.Database.ExecuteSqlRawAsync(script,ct);
            }
            Check(await authority.InstalledAsync(ct),"idempotent guarded schema and trusted foreign keys");
            db.Branches.AddRange(branches);db.InternalRequestTypes.Add(type);await db.SaveChangesAsync(ct);
            var branch=branches[0].BranchId;var otherBranch=branches[1].BranchId;
            var departments=new[]{new Department{DepartmentCode=prefix+"A",DepartmentName="Synthetic A",BranchId=branch,IsActive=true},
                new Department{DepartmentCode=prefix+"B",DepartmentName="Synthetic B",BranchId=branch,IsActive=true},
                new Department{DepartmentCode=prefix+"C",DepartmentName="Synthetic C",BranchId=otherBranch,IsActive=true}};
            db.Departments.AddRange(departments);await db.SaveChangesAsync(ct);
            var department=departments[0].DepartmentId;var second=departments[1].DepartmentId;
            var role=await db.Roles.SingleAsync(r=>r.RoleCode=="DEPT_MANAGER"&&r.IsActive,ct);
            var permission=await db.Permissions.SingleAsync(p=>p.PermissionCode=="IR_PROCESS",ct);
            var grant=await db.RolePermissions.AsNoTracking().SingleOrDefaultAsync(g=>g.RoleId==role.RoleId&&g.PermissionId==permission.PermissionId,ct);
            if(grant==null)
            {
                grant=new(){RoleId=role.RoleId,PermissionId=permission.PermissionId,DataScope="BRANCH",GrantedAt=DateTime.UtcNow};
                db.RolePermissions.Add(grant);await db.SaveChangesAsync(ct);grantId=grant.RolePermissionId;
            }
            Check(grant.DataScope is "BRANCH" or "ALL","existing IR_PROCESS grant has supported scope");
            db.UserRoles.Add(new(){UserId=actor,RoleId=role.RoleId,BranchId=branch,AssignedAt=DateTime.UtcNow.AddMinutes(-1),IsActive=true});
            db.UserBranchAccesses.Add(new(){UserId=actor,BranchId=branch,GrantedAt=DateTime.UtcNow.AddMinutes(-1)});
            var rows=new[]{new InternalRequest{RequestNo=prefix+"A",BranchId=branch,RequestTypeId=type.RequestTypeId,RequesterId=actor,
                ReceivingDepartmentId=department,Subject="Synthetic authority",Description="Synthetic",Status="IN_PROGRESS",CreatedAt=DateTime.UtcNow},
                new InternalRequest{RequestNo=prefix+"B",BranchId=otherBranch,RequestTypeId=type.RequestTypeId,RequesterId=actor,
                ReceivingDepartmentId=departments[2].DepartmentId,Subject="Synthetic cross branch",Description="Synthetic",Status="IN_PROGRESS",CreatedAt=DateTime.UtcNow}};
            db.InternalRequests.AddRange(rows);await db.SaveChangesAsync(ct);requests=rows.Select(r=>r.InternalRequestId).ToArray();db.ChangeTracker.Clear();
            await AssertAllowed(requests[0],false,"IR_PROCESS and DEPT_MANAGER without mapping deny");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.department_management_access(user_id,branch_id,department_id,granted_at) VALUES({actor},{branch},{department},DATEADD(minute,-1,SYSUTCDATETIME()))",ct);
            await AssertAllowed(requests[0],true,"exact current department mapping allows");
            if(wholeClone)
            {
                if(Environment.GetEnvironmentVariable("DEV3_VERIFY_IR_HANDOVER")=="1")
                {
                    await SprintInternalRequestHandoverScenario.RunAsync(db,http??throw new Exception("Owned HTTP client required"),actor,requests[0],checks,ct,
                        username??throw new Exception("Username required"),password??throw new Exception("Password required"));
                    return;
                }
                if(Environment.GetEnvironmentVariable("DEV3_VERIFY_IR_INTAKE")=="1")
                {
                    await SprintInternalRequestIntakeScenario.RunAsync(db,http??throw new Exception("Owned HTTP client required"),actor,requests[0],checks,ct,
                        username??throw new Exception("Username required"),password??throw new Exception("Password required"));
                    return;
                }
                if(Environment.GetEnvironmentVariable("DEV3_VERIFY_INCIDENT_READ")=="1")
                {
                    await SprintIncidentReadScenario.RunAsync(db,http??throw new Exception("Owned HTTP client required"),actor,branch,department,requests[0],checks,ct,
                        username??throw new Exception("Username required"),password??throw new Exception("Password required"));
                    return;
                }
                if(Environment.GetEnvironmentVariable("DEV3_VERIFY_IR_WITHDRAWAL")=="1")
                {
                    await SprintInternalRequestWithdrawalScenario.RunAsync(db,http??throw new Exception("Owned HTTP client required"),actor,requests[0],checks,ct,
                        username??throw new Exception("Username required"),password??throw new Exception("Password required"));
                    return;
                }
                await SprintLinkedTaskCreationScenario.RunAsync(db,http??throw new Exception("Owned authenticated HTTP client required"),actor,requests[0],checks,ct,
                    username??throw new Exception("Username required"),password??throw new Exception("Password required"));
                return;
            }
            if(Environment.GetEnvironmentVariable("DEV3_VERIFY_IR_TASK_LINKS")=="1")
                await SprintInternalRequestTaskLinksScenario.RunAsync(db,actor,requests[0],checks,ct,username,password);
            else if(Environment.GetEnvironmentVariable("DEV3_VERIFY_IR_PROGRESS")=="1")
                await SprintInternalRequestProgressScenario.RunAsync(db,actor,requests[0],checks,ct,username,password);
            else if(Environment.GetEnvironmentVariable("DEV3_VERIFY_IR_WORKER_BROWSER")=="1")
                await SprintInternalRequestWorkerScenario.RunAsync(db,actor,requests[0],username??throw new Exception("Username required"),password??throw new Exception("Password required"),checks,ct);
            else if(Environment.GetEnvironmentVariable("DEV3_VERIFY_IR_DECISION")=="1")
                await SprintInternalRequestDecisionScenario.RunAsync(db,actor,requests[0],checks,ct,username,password);
            await AssertAllowed(requests[1],false,"cross branch denies even requester");
            var crossBranchRejected=false;
            try
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.department_management_access(user_id,branch_id,department_id,granted_at) VALUES({actor},{branch},{departments[2].DepartmentId},SYSUTCDATETIME())",ct);
            }
            catch(SqlException error)when(error.Number==547){crossBranchRejected=true;}
            Check(crossBranchRejected,"composite FK rejects cross-branch department mapping");
            await using(var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct))
            {
                await db.Database.ExecuteSqlRawAsync("EXEC sys.sp_dropextendedproperty @name=N'HotelOps.DepartmentAuthority.V1'",ct);
                Check(!await authority.AllowsCurrentRequestAsync(actor,requests[0],ct),"missing schema capability denies");
                await tx.RollbackAsync(ct);
            }
            await db.Departments.Where(d=>d.DepartmentId==department).ExecuteUpdateAsync(s=>s.SetProperty(d=>d.IsActive,false),ct);
            await AssertAllowed(requests[0],false,"inactive receiving department denies");
            await db.Departments.Where(d=>d.DepartmentId==department).ExecuteUpdateAsync(s=>s.SetProperty(d=>d.IsActive,true),ct);
            await db.InternalRequests.Where(r=>r.InternalRequestId==requests[0]).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.ReceivingDepartmentId,second),ct);
            await AssertAllowed(requests[0],false,"routing rechecks new receiving department");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.department_management_access(user_id,branch_id,department_id,granted_at) VALUES({actor},{branch},{second},DATEADD(minute,-1,SYSUTCDATETIME()))",ct);
            await AssertAllowed(requests[0],true,"one manager can explicitly manage multiple departments");
            await db.UserRoles.Where(r=>r.UserId==actor).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,false),ct);
            await AssertAllowed(requests[0],false,"revoked role denies despite mapping");
            await db.UserRoles.Where(r=>r.UserId==actor).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,true),ct);
            await using(var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct))
            {
                await db.RolePermissions.Where(g=>g.RolePermissionId==grant.RolePermissionId).ExecuteDeleteAsync(ct);
                Check(!await authority.AllowsCurrentRequestAsync(actor,requests[0],ct),"revoked permission denies despite active mapped role");
                await tx.RollbackAsync(ct);
            }
            await db.Users.Where(u=>u.UserId==actor).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.AccountStatus,"INACTIVE"),ct);
            await AssertAllowed(requests[0],false,"inactive actor denies");
            await db.Users.Where(u=>u.UserId==actor).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.AccountStatus,"ACTIVE"),ct);
            // Independent connection must not revoke a mapping or route while the authorized transaction is alive.
            await using(var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct))
            {
                Check(await authority.AllowsCurrentRequestAsync(actor,requests[0],ct),"authority established in transaction");
                await using var connection=new SqlConnection(db.Database.GetConnectionString());await connection.OpenAsync(ct);
                foreach(var query in new[]{"UPDATE dbo.department_management_access SET revoked_at=SYSUTCDATETIME() WHERE user_id=@actor AND branch_id=@branch AND department_id=@department",
                    "UPDATE dbo.internal_requests SET receiving_department_id=@target WHERE internal_request_id=@request"})
                {
                    await using var command=connection.CreateCommand();command.CommandText="SET LOCK_TIMEOUT 500; "+query;
                    command.Parameters.AddWithValue("@actor",actor);command.Parameters.AddWithValue("@branch",branch);
                    command.Parameters.AddWithValue("@department",second);command.Parameters.AddWithValue("@target",department);command.Parameters.AddWithValue("@request",requests[0]);
                    var blocked=false;try{await command.ExecuteNonQueryAsync(ct);}catch(SqlException error)when(error.Number==1222){blocked=true;}
                    Check(blocked,"concurrent "+(query.Contains("management_access")?"mapping revocation":"request routing")+" waits for authorized transaction");
                }
                await db.InternalRequests.Where(r=>r.InternalRequestId==requests[0]).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.Subject,"Synthetic rollback"),ct);
                db.AuditLogs.Add(new(){ActorUserId=actor,BranchId=branch,EntityType="internal_requests",EntityId=requests[0].ToString(),Action="UPDATE",OccurredAt=DateTime.UtcNow,NewValues="{\"syntheticRollback\":true}"});
                await db.SaveChangesAsync(ct);await tx.RollbackAsync(ct);db.ChangeTracker.Clear();
            }
            Check(await db.InternalRequests.Where(r=>r.InternalRequestId==requests[0]).Select(r=>r.Subject).SingleAsync(ct)=="Synthetic authority"
                &&!await db.AuditLogs.AnyAsync(a=>a.EntityType=="internal_requests"&&a.EntityId==requests[0].ToString(),ct),"authorized data and audit rollback together");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.department_management_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={actor} AND branch_id={branch} AND department_id={second}",ct);
            await AssertAllowed(requests[0],false,"committed mapping revocation denies next transaction");
        }
        finally
        {
            if(!wholeClone)
            {
            db.ChangeTracker.Clear();await using var tx=await db.Database.BeginTransactionAsync(CancellationToken.None);
            if(await authority.InstalledAsync(CancellationToken.None))
                await db.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.department_management_access WHERE user_id={actor}",CancellationToken.None);
            await db.AuditLogs.Where(a=>a.EntityType=="internal_requests"&&requests.Select(id=>id.ToString()).Contains(a.EntityId)).ExecuteDeleteAsync(CancellationToken.None);
            await db.InternalRequests.Where(r=>r.RequestNo.StartsWith(prefix)).ExecuteDeleteAsync(CancellationToken.None);
            await db.InternalRequestTypes.Where(t=>t.TypeCode==prefix).ExecuteDeleteAsync(CancellationToken.None);
            await db.UserRoles.Where(r=>r.UserId==actor).ExecuteDeleteAsync(CancellationToken.None);
            if(grantId!=0)await db.RolePermissions.Where(g=>g.RolePermissionId==grantId).ExecuteDeleteAsync(CancellationToken.None);
            await db.UserBranchAccesses.Where(a=>a.UserId==actor&&branches.Select(b=>b.BranchId).Contains(a.BranchId)).ExecuteDeleteAsync(CancellationToken.None);
            await db.Departments.Where(d=>d.DepartmentCode.StartsWith(prefix)).ExecuteDeleteAsync(CancellationToken.None);
            await db.Branches.Where(b=>b.BranchCode.StartsWith(prefix)).ExecuteDeleteAsync(CancellationToken.None);
            await db.Users.Where(u=>u.UserId==actor).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.AccountStatus,"ACTIVE"),CancellationToken.None);
            await tx.CommitAsync(CancellationToken.None);checks.Add(new{departmentAuthorityCleanup=true});
            }
            else checks.Add(new{departmentAuthorityCleanup="Verified whole-clone drop required; immutable task records retained"});
        }
    }
}
