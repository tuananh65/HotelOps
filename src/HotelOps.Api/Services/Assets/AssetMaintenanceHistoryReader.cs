using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Insights;
using HotelOps.Api.Services.Insights;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Assets;

// Dedicated UC072 read model; it grants no asset or maintenance mutation authority.
public sealed class AssetMaintenanceHistoryReader(HotelOpsDbContext db, InsightAccess access, DashboardSessionGuard session)
{
    private async Task<int> PageSizeAsync(CancellationToken ct)
    {
        var values = await db.SystemSettings.AsNoTracking().Where(s => s.BranchId == null && s.SettingKey == "SYS-PAR-11")
            .Select(s => s.SettingValue).ToArrayAsync(ct);
        if (values.Length == 0) return 20;
        if (values.Length != 1 || !int.TryParse(values[0], out var size) || size < 1 || size > 100)
            throw new InsightException(503, "PAGE_SIZE_CONFIGURATION", "Invalid SYS-PAR-11 configuration.");
        return size;
    }
    public async Task<bool> AllowedAsync(long branch, CancellationToken ct)
    {
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var user = access.UserId;
        if (!await db.UserRoles.AsNoTracking().AnyAsync(r => r.UserId == user && r.IsActive && r.Role.IsActive
            && (r.Role.RoleCode == "OPERATIONS_MANAGER" || r.Role.RoleCode == "MAINTENANCE_LEAD")
            && (r.BranchId == null || r.BranchId == branch) && r.AssignedAt <= now && (r.ExpiresAt == null || r.ExpiresAt > now)
            && r.User.AccountStatus == "ACTIVE" && r.User.EmploymentStatus == "ACTIVE" && r.User.DeletedAt == null, ct)
            || !await db.UserBranchAccesses.AsNoTracking().AnyAsync(b => b.UserId == user && b.BranchId == branch
                && b.Branch.IsActive && b.RevokedAt == null && b.GrantedAt <= now, ct)) return false;
        try
        {
            foreach (var permission in new[] { "ASSET_VIEW", "INCIDENT_VIEW", "MAINTENANCE_VIEW" })
                await access.BranchesAsync(permission, branch, ct);
            return true;
        }
        catch (InsightException e) when (e.Status == 403) { return false; }
    }

    private async Task RequireAsync(long branch, CancellationToken ct)
    {
        await session.RequireAsync(ct);
        if (!await AllowedAsync(branch, ct)) throw new InsightException(403, "ACCESS_DENIED", "Asset and maintenance source read permissions in this branch are required.");
    }

    public async Task<object> OptionsAsync(CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([access.UserId], ct);
        await session.RequireAsync(ct);
        var branches = await db.Branches.AsNoTracking().Where(b => b.IsActive).OrderBy(b => b.BranchId)
            .Select(b => new { b.BranchId, b.BranchName }).ToArrayAsync(ct);
        var allowed = new HashSet<long>();
        foreach (var b in branches) if (await AllowedAsync(b.BranchId, ct)) allowed.Add(b.BranchId);
        if (allowed.Count == 0) throw new InsightException(403, "ACCESS_DENIED", "No authorized asset history branch.");
        await session.RequireAsync(ct);
        await tx.CommitAsync(ct);
        return branches.Where(b => allowed.Contains(b.BranchId)).ToArray();
    }

    public async Task<object> AssetsAsync(long branch, string? search, long after, CancellationToken ct)
    {
        if (branch <= 0 || after < 0 || search?.Length > 200) throw new InsightException(400, "INVALID_FILTER", "Invalid asset filter.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([access.UserId], ct);
        await RequireAsync(branch, ct);
        var pageSize = await PageSizeAsync(ct);
        var term = search?.Trim();
        var rows = await db.Assets.AsNoTracking().Where(a => a.BranchId == branch && a.AssetId > after
            && (term == null || term == "" || a.AssetCode.Contains(term) || a.AssetName.Contains(term)))
            .OrderBy(a => a.AssetId).Take(pageSize + 1).Select(a => new { a.AssetId, a.AssetCode, a.AssetName }).ToArrayAsync(ct);
        await RequireAsync(branch, ct); await tx.CommitAsync(ct);
        return new { Items = rows.Take(pageSize), HasMore = rows.Length > pageSize };
    }

    public async Task<object> ReadAsync(long assetId, DateOnly? from, DateOnly? to, string? type, int page, CancellationToken ct)
    {
        if (assetId <= 0 || page < 1 || page > 2000 || type is not (null or "INCIDENT" or "PREVENTIVE"))
            throw new InsightException(400, "INVALID_FILTER", "Invalid history filter.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([access.UserId], ct);
        await session.RequireAsync(ct);
        var asset = await db.Assets.AsNoTracking().Where(a => a.AssetId == assetId)
            .Select(a => new { a.AssetId, a.AssetCode, a.AssetName, a.BranchId }).SingleOrDefaultAsync(ct);
        if (asset == null) throw new InsightException(404, "ASSET_UNAVAILABLE", "Asset unavailable.");
        await RequireAsync(asset.BranchId, ct);
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var filters = await access.FiltersAsync("ASSET_VIEW", new InsightQuery { BranchId = asset.BranchId, From = from, To = to }, new DateTimeOffset(now, TimeSpan.Zero), ct);
        var pageSize = await PageSizeAsync(ct);
        var start = InsightAccess.Start(filters); var end = InsightAccess.End(filters);
        var zones = await db.SystemSettings.AsNoTracking().Where(s => s.BranchId == null && s.SettingKey == "SYS-PAR-10")
            .Select(s => s.SettingValue).ToArrayAsync(ct);
        var timeZone = zones.Length == 0 ? "Asia/Ho_Chi_Minh" : zones.Length == 1 ? zones[0] ?? "" : "";
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(timeZone); }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        { throw new InsightException(503, "TIMEZONE_CONFIGURATION", "Invalid SYS-PAR-10 configuration."); }
        var incidentQuery = db.Incidents.AsNoTracking().Where(i => i.AssetId == assetId && i.BranchId == asset.BranchId
            && (i.Room == null || i.Room.BranchId == i.BranchId) && (i.Area == null || i.Area.BranchId == i.BranchId)
            && i.DiscoveredAt >= start && i.DiscoveredAt < end && i.DiscoveredAt <= now);
        var incidents = type == "PREVENTIVE" ? [] : await incidentQuery.OrderBy(i => i.IncidentId).Take(50001)
            .Select(i => new { i.IncidentId, i.IncidentNo, i.IncidentTitle, i.Status, i.DiscoveredAt }).ToArrayAsync(ct);
        if (incidents.Length > 50000) throw new InsightException(422, "HISTORY_LIMIT", "Narrow the history period.");
        var incidentIds = incidents.Select(i => i.IncidentId).ToArray();
        var cycles = type == "PREVENTIVE" ? [] : await ReportIncidentCycles.ReadAsync(db, incidentQuery, now, ct);
        var occurrences = type == "INCIDENT" ? [] : await ReportPreventiveOccurrences.ReadAsync(db, filters, now, ct, assetId);
        var workQuery = db.MaintenanceWorks.AsNoTracking().Where(w => w.IncidentId != null && incidentIds.Contains(w.IncidentId.Value)
            && (w.Department == null || w.Department.BranchId == asset.BranchId) && !w.PreventiveMaintenanceRuns.Any() && w.CreatedAt <= now);
        var works = await workQuery.OrderBy(w => w.MaintenanceWorkId).Take(50001).Select(w => new
        { w.MaintenanceWorkId, w.IncidentId, w.WorkNo, w.Status, w.ActualStartAt, w.ResolvedAt, w.ResolutionNote,
            CurrentAssignee = w.Assignee == null ? null : w.Assignee.FullName,
            Technicians = w.MaintenanceWorkLogs.Where(l => l.LoggedAt <= now).Select(l => l.LoggedByNavigation.FullName).Distinct().ToArray() }).ToArrayAsync(ct);
        if (works.Length > 50000) throw new InsightException(422, "HISTORY_LIMIT", "Narrow the history period.");
        var missing = new HashSet<string>();
        var rows = new List<HistoryRow>();
        foreach (var incident in incidents)
            rows.Add(new("INCIDENT", incident.IncidentId, incident.DiscoveredAt, incident.IncidentNo, incident.IncidentTitle,
                incident.Status, null, null, works.Where(w => w.IncidentId == incident.IncidentId).Cast<object>().ToArray()));
        foreach (var occurrence in occurrences)
        {
            if (occurrence.MissingReason != null) missing.Add(occurrence.MissingReason);
            // An invalid branch/snapshot cannot establish this asset's historical ownership.
            if (occurrence.MissingReason is "OCCURRENCE_SNAPSHOT_INVALID" or "OCCURRENCE_SNAPSHOT_UNAVAILABLE") continue;
            var run = await db.PreventiveMaintenanceRuns.AsNoTracking().Where(r => r.RunId == occurrence.RunId)
                .Select(r => new { r.Note, Technician = r.PerformedByNavigation == null ? null : r.PerformedByNavigation.FullName }).SingleAsync(ct);
            rows.Add(new("PREVENTIVE", occurrence.RunId, occurrence.DueAt, "PM-" + occurrence.RunId, run.Note,
                occurrence.WorkflowStatus, occurrence.MissingReason == null ? run.Technician : null, occurrence.MissingReason == null ? occurrence.Result : null, []));
        }
        var ordered = rows.OrderByDescending(r => r.At).ThenBy(r => r.Type).ThenByDescending(r => r.Id).ToArray();
        var selected = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToArray();
        var requestedWorkIds = works.Where(w => selected.Any(r => r.Type == "INCIDENT" && r.Id == w.IncidentId)).Select(w => w.MaintenanceWorkId)
            .Concat(occurrences.Where(o => selected.Any(r => r.Type == "PREVENTIVE" && r.Id == o.RunId)
                && o.MissingReason == null && o.WorkId != null).Select(o => o.WorkId!.Value)).Distinct().ToArray();
        var workIds = await db.MaintenanceWorks.AsNoTracking().Where(w => requestedWorkIds.Contains(w.MaintenanceWorkId)
            && (w.Department == null || w.Department.BranchId == asset.BranchId)
            && (w.IncidentId != null && incidentIds.Contains(w.IncidentId.Value) && !w.PreventiveMaintenanceRuns.Any()
                || w.IncidentId == null && w.PreventiveMaintenanceRuns.Count() == 1
                    && w.PreventiveMaintenanceRuns.Any(r => r.Schedule.AssetId == assetId && r.Schedule.RoomId == null && r.Schedule.AreaId == null)))
            .Select(w => w.MaintenanceWorkId).ToArrayAsync(ct);
        if (workIds.Length != requestedWorkIds.Length) missing.Add("MAINTENANCE_WORK_SCOPE_INCOMPLETE");
        // Materials are recorded consumption, never inferred from issued quantity.
        var materials = await db.MaintenanceMaterialUsages.AsNoTracking().Where(u => workIds.Contains(u.MaintenanceWorkId)
            && u.MaintenanceWorkLog.MaintenanceWorkId == u.MaintenanceWorkId && u.MaintenanceWorkLog.LoggedAt <= now
            && u.MaintenanceWorkLog.MaterialIssueId == u.StockDocumentLine.DocumentId
            && u.StockDocumentLine.Document.DocumentType == "ISSUE" && u.StockDocumentLine.Document.Status == "POSTED"
            && u.StockDocumentLine.Document.SourceWarehouse != null && u.StockDocumentLine.Document.SourceWarehouse.BranchId == asset.BranchId
            && u.StockDocumentLine.ItemId == u.ItemId && u.UsedBaseQty > 0)
            .Select(u => new { u.UsageId, u.MaintenanceWorkId, u.ItemId, u.Item.ItemCode, u.Item.ItemName,
                Quantity = u.UsedBaseQty, Unit = u.BaseUom.UomCode, Technician = u.MaintenanceWorkLog.LoggedByNavigation.FullName,
                At = u.MaintenanceWorkLog.LoggedAt }).ToArrayAsync(ct);
        if (await db.MaintenanceMaterialUsages.AsNoTracking().CountAsync(u => workIds.Contains(u.MaintenanceWorkId), ct) != materials.Length)
            missing.Add("MATERIAL_USAGE_PROVENANCE_INCOMPLETE");
        foreach (var row in selected)
        {
            var rowWorkIds = row.Type == "INCIDENT" ? works.Where(w => w.IncidentId == row.Id).Select(w => w.MaintenanceWorkId).ToArray()
                : occurrences.Where(o => o.RunId == row.Id && o.WorkId != null).Select(o => o.WorkId!.Value).ToArray();
            row.Materials = materials.Where(m => rowWorkIds.Contains(m.MaintenanceWorkId)).Cast<object>().ToArray();
        }
        var latestCycles = cycles.GroupBy(c => c.IncidentId).SelectMany(g => g.Where(c => (c.ReopenEventId ?? 0) == g.Max(v => v.ReopenEventId ?? 0))).ToArray();
        var resolved = latestCycles.Where(c => c.ResolutionSeconds != null).ToArray();
        if (latestCycles.Any(c => c.ResolutionSeconds == null)) missing.Add("RESOLUTION_TIME_INCOMPLETE");
        var reopens = await db.IncidentEvents.AsNoTracking().Where(e => incidentIds.Contains(e.IncidentId)
            && e.EventType == "REOPENED" && e.OccurredAt <= now).Select(e => e.EventId).ToArrayAsync(ct);
        var provenReopens = cycles.Where(c => c.ReopenEventId != null).Select(c => c.ReopenEventId!.Value).Distinct().ToArray();
        var recurrenceComplete = reopens.All(id => provenReopens.Contains(id));
        if (!recurrenceComplete) missing.Add("REOPEN_PROVENANCE_INCOMPLETE");
        var pmComplete = occurrences.All(o => o.MissingReason == null);
        var lastPm = occurrences.Where(o => o.WorkflowStatus == "COMPLETED" && o.MissingReason == null).Max(o => o.PerformedAt);
        await RequireAsync(asset.BranchId, ct); await tx.CommitAsync(ct);
        return new { Asset = asset, Snapshot = DateTime.SpecifyKind(now, DateTimeKind.Utc), TimeZone = timeZone, Filters = filters, Type = type,
            Page = page, PageSize = pageSize, TotalCount = rows.Count, Items = selected,
            Totals = new { IncidentCount = type == "PREVENTIVE" ? (int?)null : incidents.Length,
                Recurrences = type == "PREVENTIVE" || !recurrenceComplete ? (int?)null : provenReopens.Length,
                AverageResolutionHours = resolved.Length == 0 || resolved.Length != latestCycles.Length ? (decimal?)null : resolved.Average(c => c.ResolutionSeconds!.Value) / 3600m,
                LastPreventiveAt = type == "INCIDENT" || !pmComplete ? (DateTime?)null : lastPm,
                ResolutionPopulation = "Latest proven processing cycle of each incident discovered in the selected UTC period; discovery to resolution.",
                PreventivePopulation = "Occurrences due in the selected UTC period." }, MissingSources = missing.Order().ToArray() };
    }

    private sealed record HistoryRow(string Type, long Id, DateTime At, string Number, string? Title, string? Status,
        string? Technician, string? Result, object[] Repairs)
    {
        public object[] Materials { get; set; } = [];
    }
}
