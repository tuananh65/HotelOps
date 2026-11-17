using System.Data;
using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.WorkTime;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Services.Users;
public sealed record EmployeeMembershipInput(long DepartmentId,string Reason,long? ReplacementDepartmentId=null);
public sealed record EmployeeMembershipOption(long Id,string Name);
public sealed record EmployeeMembershipBranch(long Id,string Name,bool IsPrimary);
public sealed record EmployeeMembershipRow(long DepartmentId,string Name,DateTime GrantedAt,DateTime? RevokedAt,string Provenance);
public sealed record EmployeeMembershipPage(long UserId,long BranchId,string Version,IReadOnlyList<EmployeeMembershipRow> Memberships,IReadOnlyList<EmployeeMembershipOption> Departments)
{public IReadOnlyList<EmployeeMembershipPeriod> History{get;init;}=[];public long? PrimaryDepartmentId{get;init;}public IReadOnlyList<EmployeeMembershipOption> ReplacementDepartments{get;init;}=[];}
public sealed partial class EmployeeMembershipService(HotelOpsDbContext db,ICurrentUserAccessor current,IPermissionService permissions,ISqlSessionService sessions,UserCredentialResetAccess authority)
{
    private async Task Gate(SessionReference ticket,long sid,long target,long? branch,CancellationToken ct,bool requireTargetAccess=true)
    {
        if(!current.TryGetCurrentUser(out var actor)||actor.UserId!=ticket.UserId||actor.SessionId!=sid||actor.IsRestricted||ticket.IsRestricted)
            throw new InternalRequestReadException(401,"SESSION_INVALID");
        if(actor.UserId==target)throw new InternalRequestReadException(403,"SELF_SCOPE_CHANGE_FORBIDDEN");
        var valid=await sessions.ValidateSessionAsync(ticket,ct);
        if(!valid.IsValid||valid.Session?.SessionId!=sid||valid.Session.IsRestricted)throw new InternalRequestReadException(401,"SESSION_INVALID");
        var permission=await permissions.EvaluateGlobalAsync("USER_MANAGE",ct);
        if(!permission.IsGranted)throw new InternalRequestReadException(permission.Decision==PermissionDecision.Unavailable?503:403,"GLOBAL_USER_MANAGE_REQUIRED");
        var now=DateTime.SpecifyKind(await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct),DateTimeKind.Utc);
        if(!await db.UserRoles.AsNoTracking().AnyAsync(r=>r.UserId==actor.UserId&&r.Role.RoleCode=="SYS_ADMIN"&&r.IsActive&&r.Role.IsActive&&r.AssignedAt<=now
            &&(r.ExpiresAt==null||r.ExpiresAt>now)&&(r.BranchId==null||(branch==null||r.BranchId==branch)&&r.Branch!=null&&r.Branch.IsActive&&db.UserBranchAccesses.Any(b=>b.UserId==actor.UserId&&b.BranchId==r.BranchId&&b.RevokedAt==null&&b.GrantedAt<=now)),ct))
            throw new InternalRequestReadException(403,"ADMINISTRATOR_REQUIRED");
        if(requireTargetAccess&&branch!=null&&!await db.UserBranchAccesses.AsNoTracking().AnyAsync(a=>a.UserId==target&&a.BranchId==branch&&a.RevokedAt==null&&a.GrantedAt<=now&&a.Branch.IsActive
            &&a.User.DeletedAt==null&&a.User.AccountStatus=="ACTIVE"&&a.User.EmploymentStatus=="ACTIVE",ct))throw new InternalRequestReadException(409,"ACTIVE_TARGET_BRANCH_ACCESS_REQUIRED");
        if(!await WorkdayMembershipStore.InstalledAsync(db,ct))throw new InternalRequestReadException(503,"EMPLOYEE_MEMBERSHIP_STORAGE_REQUIRED");
        if(!await EmployeeMembershipHistory.InstalledAsync(db,ct))throw new InternalRequestReadException(503,"EMPLOYEE_MEMBERSHIP_HISTORY_REQUIRED");
    }
    private async Task Begin(SessionReference ticket,long sid,long target,long branch,CancellationToken ct)
    {await new UserEligibilityLock(db).LockAsync([ticket.UserId,target],ct);await authority.LockAuthorityAsync(ticket.UserId,target,ct);await Gate(ticket,sid,target,branch,ct);}
    public async Task<IReadOnlyList<EmployeeMembershipBranch>> BranchesAsync(SessionReference ticket,long sid,long target,CancellationToken ct)
    {
        if(target<=0)throw new InternalRequestReadException(400,"INVALID_SCOPE");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId,target],ct);await authority.LockAuthorityAsync(ticket.UserId,target,ct);
        await Gate(ticket,sid,target,null,ct);
        var now=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var rows=await db.UserBranchAccesses.AsNoTracking().Where(a=>a.UserId==target&&a.RevokedAt==null&&a.GrantedAt<=now
            &&a.Branch.IsActive&&a.User.DeletedAt==null&&a.User.AccountStatus=="ACTIVE"&&a.User.EmploymentStatus=="ACTIVE")
            .Where(a=>db.UserRoles.Any(r=>r.UserId==ticket.UserId&&r.Role.RoleCode=="SYS_ADMIN"&&r.IsActive&&r.Role.IsActive
                &&r.AssignedAt<=now&&(r.ExpiresAt==null||r.ExpiresAt>now)
                &&(r.BranchId==null||r.BranchId==a.BranchId&&db.UserBranchAccesses.Any(b=>b.UserId==ticket.UserId&&b.BranchId==a.BranchId&&b.RevokedAt==null&&b.GrantedAt<=now))))
            .OrderByDescending(a=>a.IsPrimary).ThenBy(a=>a.Branch.BranchName)
            .Select(a=>new EmployeeMembershipBranch(a.BranchId,a.Branch.BranchName,a.IsPrimary)).ToArrayAsync(ct);
        await Gate(ticket,sid,target,null,ct);await tx.CommitAsync(ct);return rows;
    }
    public async Task<EmployeeMembershipPage> ReadAsync(SessionReference ticket,long sid,long target,long branch,CancellationToken ct)
    {
        if(target<=0||branch<=0)throw new InternalRequestReadException(400,"INVALID_SCOPE");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);await Begin(ticket,sid,target,branch,ct);
        var rows=await db.Database.SqlQuery<EmployeeMembershipRow>($"SELECT m.department_id AS DepartmentId,d.department_name AS Name,m.granted_at AS GrantedAt,m.revoked_at AS RevokedAt,m.provenance AS Provenance FROM dbo.employee_department_memberships m JOIN dbo.departments d ON d.department_id=m.department_id AND d.branch_id=m.branch_id WHERE m.user_id={target} AND m.branch_id={branch}").OrderBy(m=>m.DepartmentId).ToArrayAsync(ct);
        var departments=await db.Departments.AsNoTracking().Where(d=>d.BranchId==branch&&d.IsActive).OrderBy(d=>d.DepartmentName).Select(d=>new EmployeeMembershipOption(d.DepartmentId,d.DepartmentName)).ToArrayAsync(ct);
        var version=await db.Users.AsNoTracking().Where(u=>u.UserId==target).Select(u=>u.RowVersion).SingleAsync(ct);
        var history=await EmployeeMembershipHistory.ReadAsync(db,target,branch,ct);
        var primary=await db.Users.AsNoTracking().Where(u=>u.UserId==target).Select(u=>u.DepartmentId).SingleAsync(ct);
        var now=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var replacements=(await WorkdayMembershipStore.ReadAsync(db,target,branch,now,ct)).Select(d=>new EmployeeMembershipOption(d.DepartmentId,d.Name)).ToArray();
        await Gate(ticket,sid,target,branch,ct);await tx.CommitAsync(ct);return new(target,branch,'"'+Convert.ToBase64String(version)+'"',rows,departments){History=history,PrimaryDepartmentId=primary,ReplacementDepartments=replacements};
    }
    public async Task AddAsync(SessionReference ticket,long sid,long target,long branch,byte[] version,EmployeeMembershipInput input,CancellationToken ct,bool restore=false)
    {
        var reason=input.Reason?.Trim();
        if(target<=0||branch<=0||input.DepartmentId<=0||input.ReplacementDepartmentId!=null||version.Length!=8||string.IsNullOrWhiteSpace(reason)||reason.Length<10||reason.Length>250||reason.Any(char.IsControl))
            throw new InternalRequestReadException(400,"INVALID_MEMBERSHIP_INPUT");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);await Begin(ticket,sid,target,branch,ct);
        var actual=await db.Users.AsNoTracking().Where(u=>u.UserId==target).Select(u=>u.RowVersion).SingleAsync(ct);
        if(!actual.AsSpan().SequenceEqual(version))throw new InternalRequestReadException(412,"USER_CHANGED");
        if(!await db.Departments.AsNoTracking().AnyAsync(d=>d.DepartmentId==input.DepartmentId&&d.BranchId==branch&&d.IsActive,ct))throw new InternalRequestReadException(400,"ACTIVE_DEPARTMENT_REQUIRED");
        var prior=await db.Database.SqlQuery<EmployeeMembershipRow>($"""
            SELECT m.department_id AS DepartmentId,d.department_name AS Name,m.granted_at AS GrantedAt,
              m.revoked_at AS RevokedAt,m.provenance AS Provenance
            FROM dbo.employee_department_memberships m WITH(UPDLOCK,HOLDLOCK)
            JOIN dbo.departments d ON d.department_id=m.department_id AND d.branch_id=m.branch_id
            WHERE m.user_id={target} AND m.branch_id={branch} AND m.department_id={input.DepartmentId}
            """).SingleOrDefaultAsync(ct);
        if(restore?prior?.RevokedAt==null:prior!=null)throw new InternalRequestReadException(409,restore?"REVOKED_MEMBERSHIP_REQUIRED":"MEMBERSHIP_ALREADY_RECORDED");
        var now=DateTime.SpecifyKind(await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct),DateTimeKind.Utc);
        if(restore)
        {
            if(prior!.RevokedAt<prior.GrantedAt||prior.RevokedAt>now)throw new InternalRequestReadException(409,"MEMBERSHIP_TIME_CONFLICT");
            // Preserve the complete old period before replacing the current projection. Never revive historical grants.
            if(await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT dbo.employee_department_membership_history(user_id,branch_id,department_id,granted_at,revoked_at,recorded_by,provenance,source_version,restored_by,restored_at)
                SELECT user_id,branch_id,department_id,granted_at,revoked_at,recorded_by,provenance,row_version,{ticket.UserId},{now}
                FROM dbo.employee_department_memberships WHERE user_id={target} AND branch_id={branch} AND department_id={input.DepartmentId} AND revoked_at IS NOT NULL
                """,ct)!=1)throw new InternalRequestReadException(409,"MEMBERSHIP_CHANGED");
            if(await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.employee_department_memberships SET granted_at={now},revoked_at=NULL,recorded_by={ticket.UserId},provenance={reason} WHERE user_id={target} AND branch_id={branch} AND department_id={input.DepartmentId} AND revoked_at IS NOT NULL",ct)!=1)
                throw new InternalRequestReadException(409,"MEMBERSHIP_CHANGED");
        }
        else await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.employee_department_memberships(user_id,branch_id,department_id,granted_at,recorded_by,provenance) VALUES({target},{branch},{input.DepartmentId},{now},{ticket.UserId},{reason})",ct);
        if(await db.Users.Where(u=>u.UserId==target&&u.RowVersion==version).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.UpdatedAt,now),ct)!=1)throw new InternalRequestReadException(412,"USER_CHANGED");
        var revoked=await new SessionRevocationService(db).RevokeAllAsync(target,now,ct);
        db.AuditLogs.Add(new AuditLog{ActorUserId=ticket.UserId,BranchId=branch,EntityType="users",EntityId=target.ToString(CultureInfo.InvariantCulture),Action="UPDATE",OccurredAt=now,
            OldValues=prior==null?null:JsonSerializer.Serialize(prior),NewValues=JsonSerializer.Serialize(new{operation=restore?"RESTORE_EMPLOYEE_DEPARTMENT":"ADD_EMPLOYEE_DEPARTMENT",branchId=branch,departmentId=input.DepartmentId,grantedAt=now,reason,revokedSessions=revoked}),ChangedFields="[\"employee_department_memberships\"]"});
        await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();await Gate(ticket,sid,target,branch,ct);await tx.CommitAsync(ct);
    }
    public async Task<IReadOnlyList<EmployeeMembershipDependency>> RemoveAsync(SessionReference ticket,long sid,long target,long branch,
        byte[] version,EmployeeMembershipInput input,CancellationToken ct)
    {
        var reason=input.Reason?.Trim();
        if(target<=0||branch<=0||input.DepartmentId<=0||version.Length!=8||string.IsNullOrWhiteSpace(reason)
            ||reason.Length<10||reason.Length>250||reason.Any(char.IsControl))throw new InternalRequestReadException(400,"INVALID_MEMBERSHIP_INPUT");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);await Begin(ticket,sid,target,branch,ct);
        var user=await db.Users.AsNoTracking().SingleAsync(u=>u.UserId==target,ct);
        if(!user.RowVersion.AsSpan().SequenceEqual(version))throw new InternalRequestReadException(412,"USER_CHANGED");
        var replacingPrimary=user.DepartmentId==input.DepartmentId;
        var membership=await db.Database.SqlQuery<EmployeeMembershipRow>($"""
            SELECT m.department_id AS DepartmentId,d.department_name AS Name,m.granted_at AS GrantedAt,
                m.revoked_at AS RevokedAt,m.provenance AS Provenance
            FROM dbo.employee_department_memberships m WITH(UPDLOCK,HOLDLOCK)
            JOIN dbo.departments d ON d.department_id=m.department_id AND d.branch_id=m.branch_id
            WHERE m.user_id={target} AND m.branch_id={branch} AND m.department_id={input.DepartmentId}
            """).SingleOrDefaultAsync(ct);
        if(membership==null||membership.RevokedAt!=null)throw new InternalRequestReadException(409,"ACTIVE_MEMBERSHIP_REQUIRED");
        var now=DateTime.SpecifyKind(await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct),DateTimeKind.Utc);
        if(membership.GrantedAt>now)throw new InternalRequestReadException(409,"MEMBERSHIP_TIME_CONFLICT");
        if(replacingPrimary)
        {
            if(user.PrimaryBranchId!=branch||input.ReplacementDepartmentId is not {} replacement||replacement==input.DepartmentId
                ||!(await WorkdayMembershipStore.ReadAsync(db,target,branch,now,ct)).Any(d=>d.DepartmentId==replacement))
                throw new InternalRequestReadException(409,"ACTIVE_PRIMARY_REPLACEMENT_MEMBERSHIP_REQUIRED");
            if(await db.UserRoles.AsNoTracking().AnyAsync(r=>r.UserId==target&&r.BranchId==branch&&r.IsActive&&(r.ExpiresAt==null||r.ExpiresAt>now),ct))
                return [new("PRIMARY_SCOPE_ROLES_REQUIRE_UC010",1)];
        }
        else if(input.ReplacementDepartmentId!=null)throw new InternalRequestReadException(400,"PRIMARY_REPLACEMENT_NOT_APPLICABLE");
        if(!(await WorkdayMembershipStore.ReadAsync(db,target,branch,now,ct)).Any(d=>d.DepartmentId!=input.DepartmentId))
            return [new("BRANCH_REQUIRES_DEPARTMENT_MEMBERSHIP",1)];
        var dependencies=await EmployeeMembershipDependencies.ReadAsync(db,target,branch,input.DepartmentId,now,ct);
        if(dependencies.Count!=0)return dependencies;
        if(await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.employee_department_memberships SET revoked_at={now} WHERE user_id={target} AND branch_id={branch} AND department_id={input.DepartmentId} AND revoked_at IS NULL AND granted_at<={now}",ct)!=1)
            throw new InternalRequestReadException(409,"MEMBERSHIP_CHANGED");
        var nextPrimary=replacingPrimary?input.ReplacementDepartmentId:user.DepartmentId;
        if(await db.Users.Where(u=>u.UserId==target&&u.RowVersion==version).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.DepartmentId,nextPrimary).SetProperty(u=>u.UpdatedAt,now),ct)!=1)
            throw new InternalRequestReadException(412,"USER_CHANGED");
        var revoked=await new SessionRevocationService(db).RevokeAllAsync(target,now,ct);
        db.AuditLogs.Add(new AuditLog{ActorUserId=ticket.UserId,BranchId=branch,EntityType="users",EntityId=target.ToString(CultureInfo.InvariantCulture),Action="UPDATE",OccurredAt=now,
            OldValues=JsonSerializer.Serialize(new{membership,primaryDepartmentId=user.DepartmentId}),NewValues=JsonSerializer.Serialize(new{operation="REMOVE_EMPLOYEE_DEPARTMENT",branchId=branch,
                departmentId=input.DepartmentId,primaryDepartmentId=nextPrimary,revokedAt=now,reason,revokedSessions=revoked}),ChangedFields="[\"employee_department_memberships.revoked_at\",\"department_id\"]"});
        await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();await Gate(ticket,sid,target,branch,ct);
        dependencies=await EmployeeMembershipDependencies.ReadAsync(db,target,branch,input.DepartmentId,now,ct);
        if(dependencies.Count!=0)return dependencies; // Disposing the uncommitted transaction restores membership, sessions and audit.
        await tx.CommitAsync(ct);return [];
    }
}
