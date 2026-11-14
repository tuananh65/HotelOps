using HotelOps.Api.Data;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Services.WorkTime;

public sealed class SqlShiftPreviewScopeData(HotelOpsDbContext db, TimeProvider clock) : IShiftPreviewScopeData
{
    private IQueryable<HotelOps.Api.Entities.User> Eligible(long user, long branch, long department)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return db.Users.AsNoTracking().Where(u => u.UserId == user && u.DeletedAt == null && u.AccountStatus == "ACTIVE" && u.EmploymentStatus == "ACTIVE"
         && u.PrimaryBranchId == branch && u.DepartmentId == department && u.Department != null && u.Department.IsActive && u.Department.BranchId == branch && u.Department.Branch.IsActive
         && db.UserBranchAccesses.Any(a => a.UserId == user && a.BranchId == branch && a.GrantedAt <= now && a.RevokedAt == null));
    }
    public Task<bool> ActorScopeAsync(long actor, long branch, long department, CancellationToken ct) => Eligible(actor, branch, department).AnyAsync(ct);
    public Task<bool> EmployeeScopeAsync(long employee, long branch, long department, CancellationToken ct) => Eligible(employee, branch, department).AnyAsync(ct);
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
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        await new HotelOps.Api.Services.Users.UserEligibilityLock(db).LockAsync([actor.UserId], ct);
        var admission = new Session(sessions, ticket, sid);
        var result = await new SecureShiftPlanPreviewService(current, paths, new SqlShiftPreviewScopeData(db, clock), new SqlShiftPlanPreviewReader(db), admission, clock).PreviewAsync(branch, department, input, ct);
        // Non-atomic roster reads; no business changes and no version claim.
        return result;
    }
    private sealed class Session(ISqlSessionService sessions, SessionReference ticket, long sid) : IShiftPreviewSessionAdmission
    {
        public async Task<bool> ValidateAsync(CancellationToken ct) { var v = await sessions.ValidateSessionAsync(ticket, ct); return v.IsValid && v.Session?.SessionId == sid && v.Session.UserId == ticket.UserId && !v.Session.IsRestricted; }
    }
}