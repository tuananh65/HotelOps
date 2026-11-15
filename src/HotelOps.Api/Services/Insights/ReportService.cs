using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Insights;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Insights;

public sealed class ReportService(HotelOpsDbContext db, ReportAccess access, ReportSnapshotStore store,
    ISqlSessionService sessions, ICurrentUserAccessor current, TimeProvider clock)
{
    private async Task SessionAsync(SessionReference ticket, long sid, CancellationToken ct)
    {
        if (!current.TryGetCurrentUser(out var actor) || actor.UserId != ticket.UserId || actor.SessionId != sid
            || actor.IsRestricted || ticket.IsRestricted)
            throw new InsightException(401, "SESSION_INVALID", "Session unavailable.");
        var valid = await sessions.ValidateSessionAsync(ticket, ct);
        if (!valid.IsValid || valid.Session?.SessionId != sid || valid.Session.IsRestricted)
            throw new InsightException(401, "SESSION_INVALID", "Session unavailable.");
    }

    public async Task<InsightOptions> OptionsAsync(SessionReference ticket, long sid, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId], ct);
        await SessionAsync(ticket, sid, ct);
        var filters = await access.FiltersAsync(new(), clock.GetUtcNow(), ct);
        var branches = await db.Branches.AsNoTracking().Where(b => filters.BranchIds.Contains(b.BranchId))
            .OrderBy(b => b.BranchId).Select(b => new InsightBranch(b.BranchId, b.BranchCode, b.BranchName)).ToArrayAsync(ct);
        var departments = await db.Departments.AsNoTracking().Where(d => filters.BranchIds.Contains(d.BranchId))
            .OrderBy(d => d.DepartmentId).Select(d => new InsightDepartment(d.DepartmentId, d.BranchId, d.DepartmentName)).ToArrayAsync(ct);
        await SessionAsync(ticket, sid, ct);
        var finalScope = await access.FiltersAsync(new(), clock.GetUtcNow(), ct);
        if (!filters.BranchIds.SequenceEqual(finalScope.BranchIds))
            throw new InsightException(403, "REPORT_SCOPE_CHANGED", "Report scope changed.");
        await tx.CommitAsync(ct);
        return new(branches, departments, "UTC", 30, 50000);
    }

    public async Task<ReportResult> PreviewAsync(SessionReference ticket, long sid, string key, ReportQuery query, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId], ct);
        await SessionAsync(ticket, sid, ct);
        var now = clock.GetUtcNow();
        var filters = await access.FiltersAsync(query, now, ct);
        var report = await new ReportSnapshotQueries(db).MaterializeAsync(key, query, filters, now, ct);
        report = report with { ExportFormats = ["csv", "xlsx"] };
        await SessionAsync(ticket, sid, ct);
        var finalScope = await access.FiltersAsync(query, clock.GetUtcNow(), ct);
        if (!filters.BranchIds.SequenceEqual(finalScope.BranchIds))
            throw new InsightException(403, "REPORT_SCOPE_CHANGED", "Report scope changed.");
        await tx.CommitAsync(ct);
        store.Put(access.UserId, report);
        return report;
    }

    public async Task<ExportFile> ExportAsync(SessionReference ticket, long sid, string snapshotId, ExportRequest request, CancellationToken ct)
    {
        if (request.Format is not ("csv" or "xlsx")) throw new InsightException(400, "EXPORT_FORMAT_UNSUPPORTED", "Select CSV or XLSX.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId], ct);
        await SessionAsync(ticket, sid, ct);
        await access.FiltersAsync(new(), clock.GetUtcNow(), ct);
        var report = store.Get(access.UserId, snapshotId);
        // Reauthorize every original branch; never silently export a narrowed snapshot.
        foreach (var branch in report.Filters.BranchIds)
            await access.FiltersAsync(new() { BranchId = branch,
                From = report.Filters.From, To = report.Filters.To }, clock.GetUtcNow(), ct);
        if (report.Filters.BranchIds.Length == 0)
            await access.FiltersAsync(new(), clock.GetUtcNow(), ct);
        if (report.Filters.DepartmentId is {} department && !await db.Departments.AsNoTracking()
            .AnyAsync(d => d.DepartmentId == department && report.Filters.BranchIds.Contains(d.BranchId), ct))
            throw new InsightException(403, "DEPARTMENT_SCOPE", "Snapshot department scope changed.");
        var file = request.Format == "xlsx" ? ReportXlsxRenderer.Render(report) : ReportCsvRenderer.Render(report);
        var audit = new AuditLog { ActorUserId = ticket.UserId, EntityType = "REPORT_EXPORT", EntityId = report.SnapshotId,
            Action = "CREATE", OccurredAt = clock.GetUtcNow().UtcDateTime,
            BranchId = report.Filters.BranchIds.Length == 1 ? report.Filters.BranchIds[0] : null,
            NewValues = JsonSerializer.Serialize(new { report.Key, report.SnapshotId, report.Snapshot, report.Filters,
                report.AppliedFilters, report.TotalCount, operation = "EXPORT", format = request.Format }) };
        db.AuditLogs.Add(audit);
        try { await db.SaveChangesAsync(ct); }
        finally { db.Entry(audit).State = EntityState.Detached; }
        await SessionAsync(ticket, sid, ct);
        foreach (var branch in report.Filters.BranchIds)
            await access.FiltersAsync(new() { BranchId = branch }, clock.GetUtcNow(), ct);
        if (report.Filters.BranchIds.Length == 0) await access.FiltersAsync(new(), clock.GetUtcNow(), ct);
        await tx.CommitAsync(ct);
        // No file bytes leave the service until export admission and its audit commit.
        return file;
    }
}
