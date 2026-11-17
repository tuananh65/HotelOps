using System.Text.Json;
using HotelOps.Api.DTOs.WorkTime;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.WorkTime;

public sealed partial class WorkdayService
{
    public Task<WorkdayResult> ListCorrectionsAsync(long branch, long? department, int page, CancellationToken ct)
    {
        if (page is < 1 or > 100000 || department is <= 0) return Task.FromResult(new WorkdayResult(400, Code: "MSG03"));
        return Execute(branch, "ATTENDANCE_RECORD", async actor =>
        {
            var query = from r in db.TimeCorrectionRequests.AsNoTracking()
                join i in db.WorkIntervals.AsNoTracking() on r.IntervalId equals i.IntervalId
                join d in db.Workdays.AsNoTracking() on i.WorkdayId equals d.WorkdayId
                where i.BranchId == branch && d.BranchId == branch && d.UserId == i.UserId && r.RequestedBy == i.UserId
                    && (department == null ? r.RequestedBy == actor.UserId : d.DepartmentId == department && r.RequestedBy != actor.UserId)
                select new { Request = r, Interval = i, Day = d };
            var total = await query.CountAsync(ct);
            var rows = await query.OrderByDescending(x => x.Request.RequestedAt).ThenByDescending(x => x.Request.CorrectionId)
                .Skip((page - 1) * 50).Take(50).ToArrayAsync(ct);
            return new(200, new { BranchId = branch, DepartmentId = department, Page = page, PageSize = 50, Total = total,
                Items = rows.Select(x => CorrectionView(x.Request, x.Interval, x.Day, department != null && x.Request.Status == "PENDING")).ToArray() });
        }, ct, async actor => department == null
            ? await Authorize("ATTENDANCE_RECORD", new("workday_self_branch", branch), ct)
            : await TimeCorrectionAccess.DecideAsync(db, actor.UserId, branch, department.Value, await CorrectionNow(ct), ct)
                ? null : new WorkdayResult(403, Code: "CORRECTION_FORBIDDEN"));
    }

    public Task<WorkdayResult> RequestCorrectionAsync(long branch, long intervalId, byte[] version, RequestTimeCorrection input, CancellationToken ct)
        => Execute(branch, "ATTENDANCE_RECORD", async actor =>
        {
            var reason = input.Reason?.Trim();
            if (intervalId <= 0 || version.Length != 8 || reason is null || reason.Length is < 10 or > 1000 || reason.Contains('\0'))
                return new(400, Code: "MSG03");
            var interval = await db.WorkIntervals.FromSqlInterpolated($"SELECT * FROM dbo.work_intervals WITH(UPDLOCK,HOLDLOCK) WHERE interval_id={intervalId}").SingleOrDefaultAsync(ct);
            if (interval is null || interval.UserId != actor.UserId || interval.BranchId != branch) return new(404, Code: "INTERVAL_UNAVAILABLE");
            if (!interval.RowVersion.SequenceEqual(version)) return new(412, Code: "TIME_STALE");
            if (interval.ClosedAt == null || interval.EffectiveEndedAt == null) return new(409, Code: "CLOSED_INTERVAL_REQUIRED");
            if (await db.TimeCorrectionRequests.AsNoTracking().AnyAsync(r => r.IntervalId == intervalId && r.Status == "PENDING", ct))
                return new(409, Code: "CORRECTION_ALREADY_PENDING");
            var day = await db.Workdays.AsNoTracking().SingleOrDefaultAsync(d => d.WorkdayId == interval.WorkdayId && d.UserId == actor.UserId && d.BranchId == branch, ct);
            var now = await CorrectionNow(ct);
            if (day == null || !await CorrectionRangeValid(interval, day, input.Start.UtcDateTime, input.End.UtcDateTime, now, ct)) return new(400, Code: "MSG03");
            var row = new TimeCorrectionRequest { IntervalId = intervalId, RequestedBy = actor.UserId, RequestedAt = now,
                OriginalStartedAt = interval.EffectiveStartedAt, OriginalEndedAt = interval.EffectiveEndedAt.Value,
                OriginalIntervalVersion = interval.RowVersion.ToArray(), ProposedStartedAt = input.Start.UtcDateTime,
                ProposedEndedAt = input.End.UtcDateTime, Reason = reason, Status = "PENDING" };
            db.TimeCorrectionRequests.Add(row);
            await db.SaveChangesAsync(ct);
            CorrectionAudit(row, branch, actor.UserId, "CREATE", null, now);
            await db.SaveChangesAsync(ct);
            return new(201, new { Correction = CorrectionView(row, interval, day, false), MessageCode = "MSG09" });
        }, ct);

    public async Task<WorkdayResult> ReadCorrectionAsync(long branch, long id, CancellationToken ct)
    {
        var hint = await CorrectionOwner(id, branch, ct);
        if (hint == null) return new(404, Code: "CORRECTION_UNAVAILABLE");
        return await Execute(branch, "ATTENDANCE_RECORD", async actor =>
        {
            var row = await db.TimeCorrectionRequests.AsNoTracking().SingleAsync(r => r.CorrectionId == id, ct);
            var interval = await db.WorkIntervals.AsNoTracking().SingleAsync(i => i.IntervalId == row.IntervalId, ct);
            var day = await db.Workdays.AsNoTracking().SingleAsync(d => d.WorkdayId == interval.WorkdayId, ct);
            var now = await CorrectionNow(ct);
            var decide = actor.UserId != row.RequestedBy && await TimeCorrectionAccess.DecideAsync(db, actor.UserId, branch, day.DepartmentId, now, ct);
            // Show the neighboring ranges without leaking unrelated work-item identities or branches.
            var others = await db.WorkIntervals.AsNoTracking().Where(i => i.UserId == interval.UserId && i.IntervalId != interval.IntervalId
                && i.EffectiveStartedAt <= (day.EndedAt ?? now) && (i.EffectiveEndedAt == null || i.EffectiveEndedAt >= day.StartedAt))
                .OrderBy(i => i.EffectiveStartedAt).Select(i => new { i.EffectiveStartedAt, i.EffectiveEndedAt }).ToArrayAsync(ct);
            return new(200, new TimeCorrectionDetail(CorrectionView(row, interval, day, decide && row.Status == "PENDING"),
                others.Select(i => new TimeCorrectionOverlap(Utc(i.EffectiveStartedAt), i.EffectiveEndedAt is { } end ? Utc(end) : null)).ToArray()));
        }, ct, actor => CorrectionReadGate(actor.UserId, branch, id, ct), hint);
    }

    public async Task<WorkdayResult> DecideCorrectionAsync(long branch, long id, byte[] version, DecideTimeCorrection input, CancellationToken ct)
    {
        var reason = input.Reason?.Trim();
        if (version.Length != 8 || input.Decision is not ("APPLY" or "REJECT") || reason?.Length > 1000 || reason?.Contains('\0') == true
            || input.Decision == "REJECT" && (reason is null || reason.Length < 10 || input.Start != null || input.End != null)
            || input.Decision == "APPLY" && (input.Start == null) != (input.End == null)) return new(400, Code: "MSG03");
        var hint = await CorrectionOwner(id, branch, ct);
        if (hint == null) return new(404, Code: "CORRECTION_UNAVAILABLE");
        return await Execute(branch, "TIME_CORRECTION_DECIDE", async actor =>
        {
            var row = await db.TimeCorrectionRequests.FromSqlInterpolated($"SELECT * FROM dbo.time_correction_requests WITH(UPDLOCK,HOLDLOCK) WHERE correction_id={id}").SingleAsync(ct);
            if (!row.RowVersion.SequenceEqual(version)) return new(412, Code: "TIME_STALE");
            if (row.Status != "PENDING") return new(409, Code: "CORRECTION_ALREADY_DECIDED");
            var interval = await db.WorkIntervals.FromSqlInterpolated($"SELECT * FROM dbo.work_intervals WITH(UPDLOCK,HOLDLOCK) WHERE interval_id={row.IntervalId}").SingleAsync(ct);
            var day = await db.Workdays.AsNoTracking().SingleAsync(d => d.WorkdayId == interval.WorkdayId, ct);
            var now = await CorrectionNow(ct);
            var before = new { row.Status, interval.EffectiveStartedAt, interval.EffectiveEndedAt, IntervalVersion = Convert.ToBase64String(interval.RowVersion) };
            if (input.Decision == "APPLY")
            {
                if (interval.ClosedAt == null || interval.EffectiveEndedAt == null || !interval.RowVersion.SequenceEqual(row.OriginalIntervalVersion)
                    || interval.EffectiveStartedAt != row.OriginalStartedAt || interval.EffectiveEndedAt != row.OriginalEndedAt)
                    return new(409, Code: "ORIGINAL_INTERVAL_CHANGED");
                var start = input.Start?.UtcDateTime ?? row.ProposedStartedAt;
                var end = input.End?.UtcDateTime ?? row.ProposedEndedAt;
                if (!await CorrectionRangeValid(interval, day, start, end, now, ct)) return new(400, Code: "MSG03");
                // Actual opening/closing events remain unchanged; reports consume effective time.
                interval.EffectiveStartedAt = start; interval.EffectiveEndedAt = end;
                row.AppliedStartedAt = start; row.AppliedEndedAt = end; row.Status = "APPLIED";
            }
            else row.Status = "REJECTED";
            row.DecidedBy = actor.UserId; row.DecidedAt = now; row.DecisionReason = reason;
            CorrectionAudit(row, branch, actor.UserId, "UPDATE", before, now);
            await db.SaveChangesAsync(ct);
            return new(200, new { Correction = CorrectionView(row, interval, day, false), MessageCode = "MSG08" });
        }, ct, actor => CorrectionDecisionGate(actor.UserId, branch, id, ct), hint);
    }

    private Task<DateTime> CorrectionNow(CancellationToken ct) => db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
    private async Task<long?> CorrectionOwner(long id, long branch, CancellationToken ct)
        => await (from r in db.TimeCorrectionRequests.AsNoTracking() join i in db.WorkIntervals.AsNoTracking() on r.IntervalId equals i.IntervalId
            where r.CorrectionId == id && i.BranchId == branch && r.RequestedBy == i.UserId select (long?)i.UserId).SingleOrDefaultAsync(ct);

    private async Task<WorkdayResult?> CorrectionReadGate(long actor, long branch, long id, CancellationToken ct)
    {
        var owner = await CorrectionOwner(id, branch, ct);
        if (owner == null) return new(404, Code: "CORRECTION_UNAVAILABLE");
        if (owner == actor) return await Authorize("ATTENDANCE_RECORD", new("workday_self_branch", branch), ct);
        return await CorrectionDecisionGate(actor, branch, id, ct);
    }

    private async Task<WorkdayResult?> CorrectionDecisionGate(long actor, long branch, long id, CancellationToken ct)
    {
        var info = await (from r in db.TimeCorrectionRequests.AsNoTracking() join i in db.WorkIntervals.AsNoTracking() on r.IntervalId equals i.IntervalId
            join d in db.Workdays.AsNoTracking() on i.WorkdayId equals d.WorkdayId
            where r.CorrectionId == id && i.BranchId == branch && d.BranchId == branch && i.UserId == r.RequestedBy && d.UserId == i.UserId
            select new { r.RequestedBy, d.DepartmentId }).SingleOrDefaultAsync(ct);
        return info == null || info.RequestedBy == actor || !await TimeCorrectionAccess.DecideAsync(db, actor, branch, info.DepartmentId, await CorrectionNow(ct), ct)
            ? new(403, Code: "CORRECTION_FORBIDDEN") : null;
    }

    private async Task<bool> CorrectionRangeValid(WorkInterval interval, Workday day, DateTime start, DateTime end, DateTime now, CancellationToken ct)
    {
        if (day.UserId != interval.UserId || day.BranchId != interval.BranchId || start < day.StartedAt || end < start
            || end > now || day.EndedAt != null && end > day.EndedAt || start.Year < 1900) return false;
        if (start == end) return true;
        return !await db.WorkIntervals.AsNoTracking().AnyAsync(i => i.UserId == interval.UserId && i.IntervalId != interval.IntervalId
            && i.EffectiveStartedAt < end && (i.EffectiveEndedAt == null || i.EffectiveEndedAt > start)
            && (i.EffectiveEndedAt == null || i.EffectiveEndedAt > i.EffectiveStartedAt), ct);
    }

    private void CorrectionAudit(TimeCorrectionRequest row, long branch, long actor, string action, object? before, DateTime now)
        => db.AuditLogs.Add(new AuditLog { BranchId = branch, ActorUserId = actor, EntityType = "time_correction_requests",
            EntityId = row.CorrectionId.ToString(System.Globalization.CultureInfo.InvariantCulture), Action = action, OccurredAt = now,
            OldValues = before == null ? null : JsonSerializer.Serialize(before), NewValues = JsonSerializer.Serialize(new {
                row.CorrectionId, row.IntervalId, row.RequestedBy, row.RequestedAt, row.OriginalStartedAt, row.OriginalEndedAt,
                row.ProposedStartedAt, row.ProposedEndedAt, row.Reason, row.Status, row.DecidedBy, row.DecidedAt,
                row.DecisionReason, row.AppliedStartedAt, row.AppliedEndedAt }) });

    private static TimeCorrectionView CorrectionView(TimeCorrectionRequest r, WorkInterval i, Workday d, bool canDecide)
        => new(r.CorrectionId, i.BranchId, d.DepartmentId, i.IntervalId, i.UserId, i.WorkItemKind, i.WorkItemId, r.Status,
            Utc(r.RequestedAt), Utc(r.OriginalStartedAt), Utc(r.OriginalEndedAt), Utc(r.ProposedStartedAt), Utc(r.ProposedEndedAt), r.Reason,
            r.AppliedStartedAt is { } start ? Utc(start) : null, r.AppliedEndedAt is { } end ? Utc(end) : null, r.DecidedBy,
            r.DecidedAt is { } at ? Utc(at) : null, r.DecisionReason, Convert.ToBase64String(r.RowVersion), canDecide);
}
