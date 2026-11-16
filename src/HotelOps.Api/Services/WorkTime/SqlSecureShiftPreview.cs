using HotelOps.Api.Data;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Services.WorkTime;

public sealed class SqlShiftPreviewScopeData(HotelOpsDbContext db, TimeProvider clock) : IShiftPreviewScopeData
{
    private async Task<bool> EligibleAsync(long user,long branch,long department,CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(clock);
        var now=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        return await db.Users.AsNoTracking().AnyAsync(u=>u.UserId==user&&u.DeletedAt==null&&u.AccountStatus=="ACTIVE"&&u.EmploymentStatus=="ACTIVE",ct)
            &&await db.Departments.AsNoTracking().AnyAsync(d=>d.DepartmentId==department&&d.BranchId==branch&&d.IsActive&&d.Branch.IsActive,ct)
            &&await db.UserBranchAccesses.AsNoTracking().AnyAsync(a=>a.UserId==user&&a.BranchId==branch&&a.GrantedAt<=now&&a.RevokedAt==null,ct);
    }
    public async Task<bool> ActorScopeAsync(long actor,long branch,long department,CancellationToken ct)
    {
        if(!await new HotelOps.Api.Services.InternalRequests.InternalRequestProcessAuthority(db).InstalledAsync(ct)
            ||!await EligibleAsync(actor,branch,department,ct))return false;
        var now=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var paths=await (from a in db.UserRoles.AsNoTracking() join p in db.RolePermissions.AsNoTracking() on a.RoleId equals p.RoleId
            where a.UserId==actor&&a.Role.RoleCode=="DEPT_MANAGER"&&p.Permission.PermissionCode=="SHIFT_MANAGE"
            select new PermissionGrantPath(p.Permission.PermissionCode,p.DataScope,a.BranchId,a.IsActive,a.Role.IsActive,a.AssignedAt,a.ExpiresAt,
                a.Branch!=null&&a.Branch.IsActive,db.UserBranchAccesses.Any(b=>b.UserId==actor&&b.BranchId==a.BranchId&&b.RevokedAt==null&&b.GrantedAt<=now))).ToArrayAsync(ct);
        if(!paths.Any(p=>PermissionGrantRules.IsEligible(p,now)&&(PermissionGrantRules.EffectiveScope(p)=="GLOBAL"
            ||p.BranchId==branch&&PermissionGrantRules.EffectiveScope(p)=="BRANCH")))return false;
        return await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.department_management_access WITH(HOLDLOCK) WHERE user_id={actor} AND branch_id={branch} AND department_id={department} AND granted_at<={now} AND revoked_at IS NULL").SingleAsync(ct)==1;
    }
    public async Task<bool> EmployeeScopeAsync(long employee,long branch,long department,CancellationToken ct)
    {
        if(!await WorkdayMembershipStore.InstalledAsync(db,ct)||!await EligibleAsync(employee,branch,department,ct))return false;
        var now=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        return (await WorkdayMembershipStore.ReadAsync(db,employee,branch,now,ct)).Any(d=>d.DepartmentId==department);
    }
    public Task<bool> TemplateScopeAsync(long template, long branch, CancellationToken ct) => db.ShiftTemplates.AsNoTracking().AnyAsync(t => t.ShiftTemplateId == template && t.BranchId == branch && t.IsActive && t.Branch.IsActive, ct);
    public Task<bool> HiddenOverlapAsync(long employee, long branch, long department, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        var a = start.UtcDateTime; var b = end.UtcDateTime;
        return db.ShiftAssignments.AsNoTracking().AnyAsync(x => x.UserId == employee && x.AssignmentStatus != "CANCELLED"
         && (x.Shift.BranchId != branch || x.Shift.DepartmentId != department)
         && ((x.Shift.ScheduledStartAt < b && x.Shift.ScheduledEndAt > a) || x.Shift.ScheduledEndAt <= x.Shift.ScheduledStartAt), ct);
    }
}
/// <summary>Owns read transaction because SQL session admission requires retained User-before-session locks.</summary>
public sealed class SqlSecureShiftPreview(HotelOpsDbContext db, ICurrentUserAccessor current, IPermissionDataSource paths,
 ISqlSessionService sessions, TimeProvider clock)
{
    public async Task<SecurePreviewResult> PreviewAsync(SessionReference ticket, long sid, long branch, long department, ShiftPreviewRequest input, CancellationToken ct)
    {
        if (!current.TryGetCurrentUser(out var actor) || actor.UserId != ticket.UserId || actor.SessionId != sid || sid <= 0) return new(401);
        if (actor.IsRestricted || ticket.IsRestricted) return new(403);
        if (branch <= 0 || department <= 0 || input?.Drafts == null || input.Drafts.Count is < 1 or > 500 || input.Drafts.Any(x => x == null || x.EmployeeId <= 0 || x.TemplateId <= 0 || x.ShiftDate == default)) return new(400);
        if (!db.Database.IsSqlServer() || db.Database.CurrentTransaction != null || System.Transactions.Transaction.Current != null || db.ChangeTracker.Entries().Any()) throw new InvalidOperationException("Clean preview transaction required.");
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct); budget.CancelAfter(TimeSpan.FromSeconds(30)); ct = budget.Token;
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        await new HotelOps.Api.Services.Users.UserEligibilityLock(db).LockAsync(input.Drafts.Select(d=>d.EmployeeId).Append(actor.UserId).Distinct().Order().ToArray(), ct);
        var admission = new Session(sessions, ticket, sid);
        var result = await new SecureShiftPlanPreviewService(current, paths, new SqlShiftPreviewScopeData(db, clock), new SqlShiftPlanPreviewReader(db), admission, clock).PreviewAsync(branch, department, input, ct);
        // Retain scoped membership and authority reads until the final authorization check.
        return result;
    }
    private sealed class Session(ISqlSessionService sessions, SessionReference ticket, long sid) : IShiftPreviewSessionAdmission
    {
        public async Task<bool> ValidateAsync(CancellationToken ct) { var v = await sessions.ValidateSessionAsync(ticket, ct); return v.IsValid && v.Session?.SessionId == sid && v.Session.UserId == ticket.UserId && !v.Session.IsRestricted; }
    }
}