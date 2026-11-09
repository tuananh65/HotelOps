using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.WorkTime;

/// <summary>Non-atomic read facts; no HTTP/DI activation and no authorization bypass for callers.</summary>
public sealed class SqlShiftPlanPreviewReader(HotelOpsDbContext db) : IShiftPlanPreviewReader
{
    public Task<ShiftPlanScope?> ReadScopeAsync(long branchId, long departmentId, CancellationToken ct) =>
        ScopeQuery(branchId, departmentId).SingleOrDefaultAsync(ct);

    private IQueryable<ShiftPlanScope> ScopeQuery(long branchId, long departmentId) =>
        db.Departments.AsNoTracking().Where(d => d.DepartmentId == departmentId && d.BranchId == branchId
            && d.IsActive && d.Branch.IsActive).Select(d => new ShiftPlanScope(d.Branch.Timezone));

    public Task<ShiftPlanTemplate?> ReadTemplateAsync(long templateId, CancellationToken ct) =>
        TemplateQuery(templateId).SingleOrDefaultAsync(ct);

    private IQueryable<ShiftPlanTemplate> TemplateQuery(long templateId) =>
        db.ShiftTemplates.AsNoTracking().Where(t => t.ShiftTemplateId == templateId)
            .Select(t => new ShiftPlanTemplate(t.ShiftTemplateId, t.BranchId, t.IsActive,
                t.StartTime, t.EndTime, t.CrossesMidnight));

    private IQueryable<DateTime> ClockQuery() => db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS [Value]");

    public async Task<ShiftPlanEmployee?> ReadEmployeeAsync(long employeeId, long branchId, CancellationToken ct)
    {
        var now = await ClockQuery().SingleAsync(ct);
        return await EmployeeQuery(employeeId, branchId, now).SingleOrDefaultAsync(ct);
    }

    private IQueryable<ShiftPlanEmployee> EmployeeQuery(long employeeId, long branchId, DateTime now) =>
        db.Users.AsNoTracking().Where(u => u.UserId == employeeId)
            .Select(u => new ShiftPlanEmployee(u.UserId,
                u.DeletedAt == null && u.AccountStatus == "ACTIVE" && u.EmploymentStatus == "ACTIVE",
                db.UserBranchAccesses.Any(a => a.UserId == u.UserId && a.BranchId == branchId && a.GrantedAt <= now && a.RevokedAt == null),
                u.DepartmentId));

    public Task<bool> HasOverlapAsync(long employeeId, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken ct) =>
        new ShiftAssignmentConflictReader(db).HasOverlapAsync(employeeId, startUtc, endUtc, null, ct);
}