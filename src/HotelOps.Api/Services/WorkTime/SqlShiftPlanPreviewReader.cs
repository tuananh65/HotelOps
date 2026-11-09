using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.WorkTime;

/// <summary>Non-atomic read facts; no HTTP/DI activation and no authorization bypass for callers.</summary>
public sealed class SqlShiftPlanPreviewReader(HotelOpsDbContext db) : IShiftPlanPreviewReader
{
    public Task<ShiftPlanScope?> ReadScopeAsync(long branchId, long departmentId, CancellationToken ct) =>
        db.Departments.AsNoTracking().Where(d => d.DepartmentId == departmentId && d.BranchId == branchId
            && d.IsActive && d.Branch.IsActive).Select(d => new ShiftPlanScope(d.Branch.Timezone)).SingleOrDefaultAsync(ct);

    public Task<ShiftPlanTemplate?> ReadTemplateAsync(long templateId, CancellationToken ct) =>
        db.ShiftTemplates.AsNoTracking().Where(t => t.ShiftTemplateId == templateId)
            .Select(t => new ShiftPlanTemplate(t.ShiftTemplateId, t.BranchId, t.IsActive,
                t.StartTime, t.EndTime, t.CrossesMidnight)).SingleOrDefaultAsync(ct);

    public async Task<ShiftPlanEmployee?> ReadEmployeeAsync(long employeeId, long branchId, CancellationToken ct)
    {
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS [Value]").SingleAsync(ct);
        return await db.Users.AsNoTracking().Where(u => u.UserId == employeeId)
            .Select(u => new ShiftPlanEmployee(u.UserId,
                u.DeletedAt == null && u.AccountStatus == "ACTIVE" && u.EmploymentStatus == "ACTIVE",
                db.UserBranchAccesses.Any(a => a.UserId == u.UserId && a.BranchId == branchId && a.GrantedAt <= now && a.RevokedAt == null),
                u.DepartmentId)).SingleOrDefaultAsync(ct);
    }

    public Task<bool> HasOverlapAsync(long employeeId, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken ct) =>
        new ShiftAssignmentConflictReader(db).HasOverlapAsync(employeeId, startUtc, endUtc, null, ct);
}