using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Users;
using HotelOps.Api.Services.InternalRequests;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

[Collection("HotelOps isolated SQL")]
public sealed class InternalRequestRoleSqlTests
{
    [TaskRoleTrialFact]
    public async Task CurrentInternalRequestHandlerCapabilityLossAndInconsistencyUseRealSql()
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var ct = budget.Token;
        await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer("Server=localhost;Database=HotelOps_DEV3_142A_20261114_cc78c13f;Integrated Security=true;Encrypt=true;TrustServerCertificate=true").Options);
        Assert.Equal(1, await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))='meita'
              AND DB_NAME()='HotelOps_DEV3_142A_20261114_cc78c13f'
              AND EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
                AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
              AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='DEV3.Sprint142A.Run'
                AND CONVERT(nvarchar(128),value)='cc78c13ffc124efcad67b3d804baa84c')
            THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct));
        Assert.False(await db.Users.AnyAsync(ct)); Assert.False(await db.Branches.AnyAsync(ct));
        Assert.True(await ProgressSchema.InstalledAsync(db, ct));
        var now = DateTime.UtcNow;
        var prefix = "D3ROLE" + Guid.NewGuid().ToString("N")[..10];
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try
        {
            var branch = new Branch { BranchCode=prefix,BranchName="Synthetic revoke dependency",IsActive=true };
            db.Branches.Add(branch); await db.SaveChangesAsync(ct);
            var department = new Department { BranchId=branch.BranchId,DepartmentCode=prefix,DepartmentName="Synthetic",IsActive=true };
            db.Departments.Add(department); await db.SaveChangesAsync(ct);
            var user = new User { EmployeeCode=prefix,Username=prefix,FullName="Synthetic dependency executor",Email=prefix+"@example.invalid",
                PasswordHash="NO_LOGIN_SYNTHETIC_HASH",EmploymentStatus="ACTIVE",AccountStatus="ACTIVE",CreatedAt=now,
                PrimaryBranchId=branch.BranchId,DepartmentId=department.DepartmentId };
            db.Users.Add(user); await db.SaveChangesAsync(ct);
            await new UserEligibilityLock(db).LockAsync([user.UserId],ct);
            db.UserBranchAccesses.Add(new(){UserId=user.UserId,BranchId=branch.BranchId,GrantedAt=now.AddMinutes(-1),AccessLevel="STANDARD"});
            var permission=await db.Permissions.SingleOrDefaultAsync(p=>p.PermissionCode=="IR_VIEW",ct);
            if(permission==null){var template=await db.Permissions.SingleAsync(p=>p.PermissionCode=="IR_CREATE",ct);permission=new(){PermissionCode="IR_VIEW",PermissionName="View internal requests",ModuleCode=template.ModuleCode,ResourceCode=template.ResourceCode,ActionCode="VIEW"};db.Permissions.Add(permission);await db.SaveChangesAsync(ct);}
            var role=await db.Roles.SingleAsync(r=>r.RoleCode=="DEPT_MANAGER",ct);
            if(!await db.RolePermissions.AnyAsync(g=>g.RoleId==role.RoleId&&g.PermissionId==permission.PermissionId,ct))db.RolePermissions.Add(new(){RoleId=role.RoleId,PermissionId=permission.PermissionId,DataScope="BRANCH",GrantedAt=now});
            var selected=new UserRole{UserId=user.UserId,RoleId=role.RoleId,BranchId=branch.BranchId,AssignedAt=now.AddMinutes(-1),IsActive=true};db.UserRoles.Add(selected);
            var type=new InternalRequestType{TypeCode=prefix,TypeName="Synthetic request type",IsActive=true};db.InternalRequestTypes.Add(type);await db.SaveChangesAsync(ct);
            var request=new InternalRequest{RequestNo=prefix,BranchId=branch.BranchId,RequesterId=user.UserId,RequesterDepartmentId=department.DepartmentId,ReceivingDepartmentId=department.DepartmentId,
                RequestTypeId=type.RequestTypeId,Subject="Synthetic assigned request",Description="Synthetic revoke evidence",Status="IN_PROGRESS",CreatedAt=now};
            db.InternalRequests.Add(request);await db.SaveChangesAsync(ct);
            var assignment=new InternalRequestAssignment{InternalRequestId=request.InternalRequestId,AssigneeId=user.UserId,AssignedBy=user.UserId,AssignedAt=now.AddMinutes(-1),Status="ACTIVE"};db.InternalRequestAssignments.Add(assignment);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
            async Task<Dev1ResponsibilityReview> Read()=>await new RevokeRoleResponsibilities(db,TimeProvider.System).ReadAsync(user.UserId,selected.UserRoleId,false,ct);
            var label=$"INTERNAL_REQUEST_HANDLER:{request.InternalRequestId}";
            Assert.False(await new InternalRequestEscalationAuthority(db).AllowsAsync(user.UserId,request.InternalRequestId,ct));
            Assert.Contains(label,(await Read()).Blockers);
            await db.Departments.Where(d => d.DepartmentId == department.DepartmentId).ExecuteUpdateAsync(s => s.SetProperty(d => d.IsActive, false), ct);
            Assert.Contains(label + "_INCONSISTENT", (await Read()).Unresolved);
            await db.Departments.Where(d => d.DepartmentId == department.DepartmentId).ExecuteUpdateAsync(s => s.SetProperty(d => d.IsActive, true), ct);
            await db.Branches.Where(b => b.BranchId == branch.BranchId).ExecuteUpdateAsync(s => s.SetProperty(b => b.IsActive, false), ct);
            Assert.Contains(label + "_INCONSISTENT", (await Read()).Unresolved);
            await db.Branches.Where(b => b.BranchId == branch.BranchId).ExecuteUpdateAsync(s => s.SetProperty(b => b.IsActive, true), ct);
            // Operations Manager retains permissions but is not an allowed current-handler actor.
            var alternate=new UserRole{UserId=user.UserId,RoleId=role.RoleId,BranchId=branch.BranchId,AssignedAt=now.AddMinutes(-1),IsActive=true};
            // Duplicate active user-role keys may be forbidden: use a separate existing role.
            alternate.RoleId=await db.Roles.Where(r=>r.RoleCode=="OPERATIONS_MANAGER").Select(r=>r.RoleId).SingleAsync(ct);
            foreach(var code in new[]{"IR_PROCESS","IR_VIEW"}){
                var p=await db.Permissions.SingleAsync(p=>p.PermissionCode==code,ct);
                if(!await db.RolePermissions.AnyAsync(g=>g.RoleId==alternate.RoleId&&g.PermissionId==p.PermissionId,ct))db.RolePermissions.Add(new(){RoleId=alternate.RoleId,PermissionId=p.PermissionId,DataScope="BRANCH",GrantedAt=now});
            }
            db.UserRoles.Add(alternate);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
            Assert.True(await new InternalRequestEscalationAuthority(db).AllowsAsync(user.UserId,request.InternalRequestId,ct));
            var preserved=await Read();Assert.Contains(label,preserved.Blockers);
            var counterfactual = new RevokeRoleResponsibilities(db, TimeProvider.System);
            Assert.Contains(label, (await counterfactual.ReadAsync(user.UserId, selected.UserRoleId, false, ct)).Blockers);
            await db.UserRoles.Where(r => r.UserRoleId == selected.UserRoleId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false), ct);
            var actualAfter = await counterfactual.ReadAsync(user.UserId, selected.UserRoleId, true, ct);
            Assert.Contains(label, actualAfter.Blockers);
            Assert.DoesNotContain("POST_REVOKE_GRANTS_CHANGED", actualAfter.Unresolved);
            await db.UserRoles.Where(r => r.UserRoleId == selected.UserRoleId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, true), ct);
            var executorRole = await db.Roles.SingleAsync(r => r.RoleCode == "HK_STAFF" && r.IsActive, ct);
            foreach (var code in new[] { "IR_PROCESS", "IR_VIEW" })
            {
                var p = await db.Permissions.SingleAsync(p => p.PermissionCode == code, ct);
                if (!await db.RolePermissions.AnyAsync(g => g.RoleId == executorRole.RoleId && g.PermissionId == p.PermissionId, ct))
                    db.RolePermissions.Add(new() { RoleId = executorRole.RoleId, PermissionId = p.PermissionId, DataScope = "OWN", GrantedAt = now });
            }
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            await db.UserRoles.Where(r => r.UserRoleId == alternate.UserRoleId).ExecuteUpdateAsync(s => s.SetProperty(r => r.RoleId, executorRole.RoleId), ct);
            var eligibleAlternative = await Read();
            Assert.DoesNotContain(label, eligibleAlternative.Blockers);
            Assert.DoesNotContain(label + "_UNKNOWN", eligibleAlternative.Unresolved);
            Assert.NotEmpty(eligibleAlternative.Unresolved); // Global release gates remain.
            var progressAuthority=new InternalRequestProgressAuthority(db,new InternalRequestProcessAuthority(db));
            Assert.True((await progressAuthority.ResolveAsync(user.UserId,request.InternalRequestId,ct)).CanUpdate);
            await db.Users.Where(u=>u.UserId==user.UserId).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.DepartmentId,(long?)null),ct);
            Assert.Contains(label + "_UNKNOWN", (await Read()).Unresolved);
            Assert.False((await progressAuthority.ResolveAsync(user.UserId,request.InternalRequestId,ct)).CanUpdate);
            // Explicit management authority does not require the handler's home department.
            Assert.True(await new InternalRequestProcessAuthority(db).InstalledAsync(ct));
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.department_management_access(user_id,branch_id,department_id,granted_at) VALUES({user.UserId},{branch.BranchId},{department.DepartmentId},{now})",ct);
            Assert.True((await progressAuthority.ResolveAsync(user.UserId,request.InternalRequestId,ct)).CanUpdate);
            Assert.Contains(label,(await Read()).Blockers); // Removing DEPT_MANAGER loses mapped authority.
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.department_management_access SET granted_at={now.AddDays(1)} WHERE user_id={user.UserId} AND branch_id={branch.BranchId} AND department_id={department.DepartmentId}",ct);
            Assert.Contains(label+"_UNKNOWN",(await Read()).Unresolved);
            Assert.False((await progressAuthority.ResolveAsync(user.UserId,request.InternalRequestId,ct)).CanUpdate);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.department_management_access SET granted_at={now} WHERE user_id={user.UserId} AND branch_id={branch.BranchId} AND department_id={department.DepartmentId}",ct);
            var routedDepartment = new Department { BranchId=branch.BranchId,DepartmentCode=prefix+"R",DepartmentName="Synthetic routed department",IsActive=true };
            db.Departments.Add(routedDepartment);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
            await db.InternalRequests.Where(r=>r.InternalRequestId==request.InternalRequestId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.ReceivingDepartmentId,routedDepartment.DepartmentId),ct);
            Assert.Contains(label+"_UNKNOWN",(await Read()).Unresolved);
            Assert.False((await progressAuthority.ResolveAsync(user.UserId,request.InternalRequestId,ct)).CanUpdate);
            await db.InternalRequests.Where(r=>r.InternalRequestId==request.InternalRequestId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.ReceivingDepartmentId,department.DepartmentId),ct);
            var mappedSurvivor = new RevokeRoleResponsibilities(db,TimeProvider.System);
            var mappedBefore = await mappedSurvivor.ReadAsync(user.UserId,alternate.UserRoleId,false,ct);
            Assert.DoesNotContain(label,mappedBefore.Blockers);
            Assert.DoesNotContain(label+"_UNKNOWN",mappedBefore.Unresolved);
            await db.UserRoles.Where(r=>r.UserRoleId==alternate.UserRoleId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,false),ct);
            var mappedAfter = await mappedSurvivor.ReadAsync(user.UserId,alternate.UserRoleId,true,ct);
            Assert.DoesNotContain(label,mappedAfter.Blockers);
            Assert.DoesNotContain(label+"_UNKNOWN",mappedAfter.Unresolved);
            Assert.DoesNotContain("POST_REVOKE_GRANTS_CHANGED",mappedAfter.Unresolved);
            Assert.True((await progressAuthority.ResolveAsync(user.UserId,request.InternalRequestId,ct)).CanUpdate);
            await db.UserRoles.Where(r=>r.UserRoleId==alternate.UserRoleId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,true),ct);
            await db.UserRoles.Where(r=>r.UserRoleId==selected.UserRoleId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,false),ct);
            Assert.False((await progressAuthority.ResolveAsync(user.UserId,request.InternalRequestId,ct)).CanUpdate);
            await db.UserRoles.Where(r=>r.UserRoleId==selected.UserRoleId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,true),ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.department_management_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={user.UserId} AND branch_id={branch.BranchId} AND department_id={department.DepartmentId}",ct);
            Assert.Contains(label+"_UNKNOWN",(await Read()).Unresolved);
            Assert.False((await progressAuthority.ResolveAsync(user.UserId,request.InternalRequestId,ct)).CanUpdate);
            await db.Users.Where(u=>u.UserId==user.UserId).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.DepartmentId,department.DepartmentId),ct);
            var surviving = new RevokeRoleResponsibilities(db, TimeProvider.System);
            Assert.DoesNotContain(label + "_UNKNOWN", (await surviving.ReadAsync(user.UserId,selected.UserRoleId,false,ct)).Unresolved);
            await db.UserRoles.Where(r=>r.UserRoleId==selected.UserRoleId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,false),ct);
            var survivingAfter=await surviving.ReadAsync(user.UserId,selected.UserRoleId,true,ct);
            Assert.DoesNotContain(label,survivingAfter.Blockers);
            Assert.DoesNotContain(label + "_UNKNOWN",survivingAfter.Unresolved);
            Assert.True((await progressAuthority.ResolveAsync(user.UserId,request.InternalRequestId,ct)).CanUpdate);
            await db.UserRoles.Where(r=>r.UserRoleId==selected.UserRoleId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,true),ct);
            // Permission/scope summaries stay identical when a surviving grant's
            // validity window changes; the final reread must still flag drift.
            var changedWindow = new RevokeRoleResponsibilities(db, TimeProvider.System);
            await changedWindow.ReadAsync(user.UserId,selected.UserRoleId,false,ct);
            await db.UserRoles.Where(r=>r.UserRoleId==selected.UserRoleId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,false),ct);
            await db.UserRoles.Where(r=>r.UserRoleId==alternate.UserRoleId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.ExpiresAt,now.AddDays(1)),ct);
            Assert.Contains("POST_REVOKE_GRANTS_CHANGED",(await changedWindow.ReadAsync(user.UserId,selected.UserRoleId,true,ct)).Unresolved);
            await db.UserRoles.Where(r=>r.UserRoleId==alternate.UserRoleId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.ExpiresAt,(DateTime?)null),ct);
            Assert.DoesNotContain("POST_REVOKE_GRANTS_CHANGED",(await changedWindow.ReadAsync(user.UserId,selected.UserRoleId,true,ct)).Unresolved);
            await db.UserRoles.Where(r=>r.UserRoleId==selected.UserRoleId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,true),ct);
            await db.UserRoles.Where(r => r.UserRoleId == alternate.UserRoleId).ExecuteUpdateAsync(s => s.SetProperty(r => r.RoleId, alternate.RoleId), ct);
            await db.UserRoles.Where(r=>r.UserRoleId==alternate.UserRoleId).ExecuteUpdateAsync(u=>u.SetProperty(r=>r.ExpiresAt,now),ct);
            Assert.Contains(label,(await Read()).Blockers);
            await db.InternalRequestAssignments.Where(a=>a.AssignmentId==assignment.AssignmentId).ExecuteUpdateAsync(u=>u.SetProperty(a=>a.AssignedAt,now.AddDays(1)),ct);
            Assert.Contains(label+"_INCONSISTENT",(await Read()).Unresolved);
            await db.InternalRequestAssignments.Where(a=>a.AssignmentId==assignment.AssignmentId).ExecuteUpdateAsync(u=>u.SetProperty(a=>a.AssignedAt,now.AddMinutes(-1)),ct);
            await db.InternalRequests.Where(r=>r.InternalRequestId==request.InternalRequestId).ExecuteUpdateAsync(u=>u.SetProperty(r=>r.Status,"CLOSED"),ct);
            Assert.Contains(label+"_UNKNOWN",(await Read()).Unresolved);
            await db.InternalRequestAssignments.Where(a=>a.AssignmentId==assignment.AssignmentId).ExecuteUpdateAsync(u=>u.SetProperty(a=>a.Status,"COMPLETED").SetProperty(a=>a.EndedAt,now),ct);
            var history=await Read();Assert.DoesNotContain(label,history.Blockers);Assert.DoesNotContain(label+"_UNKNOWN",history.Unresolved);
            await db.UserRoles.Where(r=>r.UserRoleId==alternate.UserRoleId).ExecuteUpdateAsync(u=>u.SetProperty(r=>r.ExpiresAt,(DateTime?)null),ct);
            Assert.True(await new InternalRequestEscalationAuthority(db).AllowsAsync(user.UserId,request.InternalRequestId,ct));
            await db.RolePermissions.Where(g=>g.PermissionId==permission.PermissionId&&(g.RoleId==role.RoleId||g.RoleId==alternate.RoleId)).ExecuteDeleteAsync(ct);
            Assert.False(await new InternalRequestEscalationAuthority(db).AllowsAsync(user.UserId,request.InternalRequestId,ct));
            Assert.NotEmpty(UserRoleRevocationService.PendingReleaseGates);
        }
        finally{await tx.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();}
        Assert.False(await db.Users.AnyAsync(ct));Assert.False(await db.Branches.AnyAsync(ct));Assert.False(await db.InternalRequests.AnyAsync(ct));
    }
}
