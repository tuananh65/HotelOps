using System.Data;
using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.WorkTime;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Users;

public sealed record EmployeeBranchRemovalInput(string Reason,long? ReplacementBranchId=null,long? ReplacementDepartmentId=null);

public sealed partial class EmployeeMembershipService
{
    public async Task<IReadOnlyList<EmployeeMembershipDependency>> RemoveBranchAsync(SessionReference ticket,long sid,long target,long branch,
        byte[] version,EmployeeBranchRemovalInput input,CancellationToken ct)
    {
        var reason=input.Reason?.Trim();
        if(target<=0||branch<=0||version.Length!=8||string.IsNullOrWhiteSpace(reason)||reason.Length<10||reason.Length>250||reason.Any(char.IsControl))
            throw new InternalRequestReadException(400,"INVALID_MEMBERSHIP_INPUT");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await Begin(ticket,sid,target,branch,ct);
        var now=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var user=await db.Users.AsNoTracking().SingleAsync(u=>u.UserId==target,ct);
        if(!user.RowVersion.AsSpan().SequenceEqual(version))throw new InternalRequestReadException(412,"USER_CHANGED");
        var access=await db.UserBranchAccesses.AsNoTracking().SingleAsync(a=>a.UserId==target&&a.BranchId==branch&&a.RevokedAt==null,ct);
        if(user.PrimaryBranchId==null||await db.UserBranchAccesses.CountAsync(a=>a.UserId==target&&a.RevokedAt==null&&a.IsPrimary,ct)!=1
            ||!await db.UserBranchAccesses.AnyAsync(a=>a.UserId==target&&a.BranchId==user.PrimaryBranchId&&a.IsPrimary&&a.RevokedAt==null,ct))
            throw new InternalRequestReadException(409,"PRIMARY_BRANCH_INCONSISTENT");
        if(access.IsPrimary!=(user.PrimaryBranchId==branch))throw new InternalRequestReadException(409,"PRIMARY_BRANCH_INCONSISTENT");
        var nextBranch=user.PrimaryBranchId;var nextDepartment=user.DepartmentId;
        if(access.IsPrimary)
        {
            if(input.ReplacementBranchId is not {} replacement||replacement==branch||input.ReplacementDepartmentId is not {} department)
                throw new InternalRequestReadException(409,"PRIMARY_BRANCH_AND_DEPARTMENT_REPLACEMENT_REQUIRED");
            await Gate(ticket,sid,target,replacement,ct);
            if(!(await WorkdayMembershipStore.ReadAsync(db,target,replacement,now,ct)).Any(d=>d.DepartmentId==department))
                throw new InternalRequestReadException(409,"ACTIVE_PRIMARY_REPLACEMENT_MEMBERSHIP_REQUIRED");
            nextBranch=replacement;nextDepartment=department;
        }
        else if(input.ReplacementBranchId!=null||input.ReplacementDepartmentId!=null)
            throw new InternalRequestReadException(400,"PRIMARY_REPLACEMENT_NOT_APPLICABLE");
        else if(await db.Departments.AnyAsync(d=>d.DepartmentId==user.DepartmentId&&d.BranchId==branch,ct))
            throw new InternalRequestReadException(409,"PRIMARY_DEPARTMENT_BRANCH_INCONSISTENT");
        var blockers=await BranchRemovalDependencies(target,branch,now,ct);
        if(blockers.Count!=0)return blockers;
        var memberships=await db.Database.SqlQuery<BranchMembershipSnapshot>($"SELECT department_id AS DepartmentId,granted_at AS GrantedAt,revoked_at AS RevokedAt,provenance AS Provenance FROM dbo.employee_department_memberships WITH(UPDLOCK,HOLDLOCK) WHERE user_id={target} AND branch_id={branch}").ToArrayAsync(ct);
        if(memberships.Any(m=>m.GrantedAt>now))throw new InternalRequestReadException(409,"MEMBERSHIP_TIME_CONFLICT");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.employee_department_memberships SET revoked_at={now} WHERE user_id={target} AND branch_id={branch} AND revoked_at IS NULL",ct);
        if(await db.UserBranchAccesses.Where(a=>a.AccessId==access.AccessId&&a.RevokedAt==null).ExecuteUpdateAsync(s=>s.SetProperty(a=>a.RevokedAt,now).SetProperty(a=>a.IsPrimary,false),ct)!=1)
            throw new InternalRequestReadException(409,"BRANCH_ACCESS_CHANGED");
        if(access.IsPrimary&&await db.UserBranchAccesses.Where(a=>a.UserId==target&&a.BranchId==nextBranch&&a.RevokedAt==null)
            .ExecuteUpdateAsync(s=>s.SetProperty(a=>a.IsPrimary,true),ct)!=1)throw new InternalRequestReadException(409,"REPLACEMENT_BRANCH_CHANGED");
        if(await db.Users.Where(u=>u.UserId==target&&u.RowVersion==version).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.PrimaryBranchId,nextBranch)
            .SetProperty(u=>u.DepartmentId,nextDepartment).SetProperty(u=>u.UpdatedAt,now),ct)!=1)throw new InternalRequestReadException(412,"USER_CHANGED");
        var revoked=await new SessionRevocationService(db).RevokeAllAsync(target,now,ct);
        db.AuditLogs.Add(new AuditLog{ActorUserId=ticket.UserId,BranchId=branch,EntityType="users",EntityId=target.ToString(CultureInfo.InvariantCulture),Action="UPDATE",OccurredAt=now,
            OldValues=JsonSerializer.Serialize(new{user.PrimaryBranchId,user.DepartmentId,access.AccessId,access.GrantedAt,access.IsPrimary,memberships}),
            NewValues=JsonSerializer.Serialize(new{operation="REMOVE_EMPLOYEE_BRANCH",branchId=branch,primaryBranchId=nextBranch,primaryDepartmentId=nextDepartment,revokedAt=now,reason,revokedSessions=revoked}),
            ChangedFields="[\"user_branch_access\",\"employee_department_memberships\",\"primary_branch_id\",\"department_id\"]"});
        await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        await Gate(ticket,sid,target,branch,ct,requireTargetAccess:false);
        if(access.IsPrimary)await Gate(ticket,sid,target,nextBranch,ct);
        blockers=await BranchRemovalDependencies(target,branch,await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct),ct);
        if(blockers.Count!=0)return blockers;
        await tx.CommitAsync(ct);return [];
    }

    private sealed record BranchMembershipSnapshot(long DepartmentId,DateTime GrantedAt,DateTime? RevokedAt,string Provenance);

    private async Task<IReadOnlyList<EmployeeMembershipDependency>> BranchRemovalDependencies(long user,long branch,DateTime now,CancellationToken ct)
    {
        var result=new List<EmployeeMembershipDependency>();
        // Include orphaned responsibilities in every department of the removed branch,
        // not merely departments still represented by an active membership.
        foreach(var department in await db.Departments.AsNoTracking().Where(d=>d.BranchId==branch).Select(d=>d.DepartmentId).ToArrayAsync(ct))
            result.AddRange(await EmployeeMembershipDependencies.ReadAsync(db,user,branch,department,now,ct));
        await Add("BRANCH_ROLES_REQUIRE_UC010",db.UserRoles.Where(r=>r.UserId==user&&r.BranchId==branch&&r.IsActive&&(r.ExpiresAt==null||r.ExpiresAt>now)).Select(r=>r.UserRoleId));
        await Add("WORKDAY",db.Workdays.Where(w=>w.UserId==user&&w.BranchId==branch&&(w.Status!="ENDED"||w.EndedAt==null)).Select(w=>w.WorkdayId));
        await Add("OPEN_INTERVAL",db.WorkIntervals.Where(w=>w.UserId==user&&w.BranchId==branch&&w.ClosedAt==null).Select(w=>w.IntervalId));
        await Add("WAREHOUSE_MANAGER",db.Warehouses.Where(w=>w.BranchId==branch&&w.ManagerUserId==user&&w.IsActive).Select(w=>w.WarehouseId));
        await Add("ASSET_CUSTODIAN",db.Assets.Where(a=>a.BranchId==branch&&a.CustodianUserId==user&&(a.LifecycleStatus=="IN_USE"||a.LifecycleStatus=="UNDER_REPAIR")).Select(a=>a.AssetId));
        await Add("ASSET_HANDOVER",db.AssetHandovers.Where(h=>h.Asset.BranchId==branch&&(h.ToUserId==user||h.FromUserId==user)&&h.HandoverStatus=="PENDING").Select(h=>h.HandoverId));
        await Add("MATERIAL_REQUEST",db.MaterialRequests.Where(r=>r.BranchId==branch&&r.RequesterId==user&&(r.Status=="DRAFT"||r.Status=="NEEDS_INFORMATION")).Select(r=>r.RequestId));
        await Add("MATERIAL_APPROVAL",db.MaterialRequestApprovalCycles.Where(c=>c.Request.BranchId==branch&&c.AssignedApproverId==user&&c.Status=="PENDING"&&c.Request.Status=="PENDING_APPROVAL"
            &&!db.MaterialRequestApprovalCycles.Any(n=>n.RequestId==c.RequestId&&n.RevisionNo>c.RevisionNo)).Select(c=>c.ApprovalCycleId));
        return result.Distinct().ToArray();
        async Task Add(string kind,IQueryable<long> query){var count=await query.LongCountAsync(ct);if(count!=0)result.Add(new(kind,count));}
    }
}
