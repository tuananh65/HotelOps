using HotelOps.Api.DTOs.HotelStructure;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.OperationalTasks;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed record DepartmentDeactivationDependency(string Kind, long Count);
public sealed record DepartmentDeactivationReview(long Id, long BranchId, bool IsActive, DepartmentDeactivationDependency[] Dependencies);

public sealed partial class RoomTypeCreationService
{
    private async Task<bool> DepartmentDeactivationSchema(CancellationToken ct) =>
        await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN COL_LENGTH('dbo.employee_department_memberships','revoked_at') IS NOT NULL
              AND COL_LENGTH('dbo.task_progress_versions','workflow_status') IS NOT NULL
              THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) == 1;

    private async Task<bool> DepartmentDeactivationAdmin(long actor, long branch, CancellationToken ct)
    {
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        return await db.UserRoles.AsNoTracking().AnyAsync(r => r.UserId == actor && r.IsActive && r.Role.IsActive
            && r.Role.RoleCode == "SYS_ADMIN" && r.AssignedAt <= now && (r.ExpiresAt == null || r.ExpiresAt > now)
            && (r.BranchId == null || r.BranchId == branch), ct);
    }

    // The caller holds Serializable and the department row lock through commit.
    // Counts disclose dependency categories, not employee identities or business content.
    private async Task<DepartmentDeactivationDependency[]> DepartmentDeactivationFacts(long department, CancellationToken ct)
    {
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var members = db.Database.SqlQuery<long>($"SELECT user_id AS Value FROM dbo.employee_department_memberships WITH(HOLDLOCK) WHERE department_id={department} AND revoked_at IS NULL");
        var facts = new List<DepartmentDeactivationDependency>();
        void Add(string kind,long count) { if(count>0)facts.Add(new(kind,count)); }
        Add("ACTIVE_USERS",await db.Users.AsNoTracking().LongCountAsync(u=>u.AccountStatus=="ACTIVE" && (u.DepartmentId==department || members.Contains(u.UserId)),ct));
        Add("WAREHOUSES",await db.Warehouses.AsNoTracking().LongCountAsync(w=>w.DepartmentId==department,ct));
        var closed = TaskWorkflowRead.Query(db,now).Where(w=>w.State=="CLOSED"||w.State=="CANCELLED").Select(w=>w.TaskId);
        Add("OPEN_OR_UNPROVEN_TASKS",await db.OperationalTasks.AsNoTracking().LongCountAsync(t=>t.DepartmentId==department&&!closed.Contains(t.TaskId),ct));
        Add("OPEN_INTERNAL_REQUESTS",await db.InternalRequests.AsNoTracking().LongCountAsync(r=>(r.ReceivingDepartmentId==department||r.RequesterDepartmentId==department)
            && r.Status!="CLOSED"&&r.Status!="REJECTED"&&r.Status!="CANCELLED",ct));
        Add("ASSETS_IN_CUSTODY",await db.Assets.AsNoTracking().LongCountAsync(a=>a.CurrentDepartmentId==department&&a.LifecycleStatus!="RETIRED",ct));
        Add("PENDING_ASSET_HANDOVERS",await db.AssetHandovers.AsNoTracking().LongCountAsync(h=>(h.FromDepartmentId==department||h.ToDepartmentId==department)&&h.HandoverStatus=="PENDING",ct));
        Add("OPEN_MAINTENANCE",await db.MaintenanceWorks.AsNoTracking().LongCountAsync(w=>w.DepartmentId==department&&w.Status!="CLOSED"&&w.Status!="CANCELLED",ct));
        Add("WORKING_WORKDAYS",await db.Workdays.AsNoTracking().LongCountAsync(w=>w.DepartmentId==department&&w.Status=="WORKING",ct));
        return facts.ToArray();
    }

    public Task<RoomTypeBatchResult> ReviewDepartmentDeactivationAsync(SessionReference ticket,long sid,long branch,long id,CancellationToken ct)
        => BatchAsync(ticket,sid,branch,async(actor,token)=>
        {
            if(!await DepartmentDeactivationAdmin(actor,branch,token))return new(RoomTypeBatchStatus.Forbidden);
            var row=await db.Departments.FromSqlInterpolated($"SELECT * FROM dbo.departments WITH(UPDLOCK,HOLDLOCK) WHERE department_id={id} AND branch_id={branch}")
                .AsNoTracking().SingleOrDefaultAsync(token);
            if(row==null)return new(RoomTypeBatchStatus.NotFound);
            if(!await DepartmentDeactivationSchema(token))return new(RoomTypeBatchStatus.SchemaRequired);
            var facts=await DepartmentDeactivationFacts(id,token);
            return new(RoomTypeBatchStatus.Success,new DepartmentDeactivationReview(id,branch,row.IsActive,facts),await DepartmentTag(DepartmentDto(row),token));
        },ct,requireRoomTypeSchema:false,eligibility:token=>DepartmentDeactivationAdmin(ticket.UserId,branch,token));
}
