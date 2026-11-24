using Microsoft.EntityFrameworkCore;
using HotelOps.Api.Compact.Infrastructure;

namespace HotelOps.Api.Compact.Services;

public sealed partial class WorkService
{
    public async Task<object> HousekeepingHistory(long branchId, DateOnly? date, int page, string? search, CancellationToken ct, string period="day", bool mine=false, string? status=null, string sort="latest")
    {
        var all = await access.Allows("CLEANING_VIEW", branchId, ct: ct);
        if (!all) await access.Require("CLEANING_VIEW", branchId, true, ct);
        Output.Require(period is "day" or "week", "INVALID_HISTORY_PERIOD", 400);
        Output.Require(sort is "latest" or "oldest" or "title", "INVALID_WORK_SORT", 400);
        var timezone = await db.Branches.AsNoTracking().Where(b => b.BranchId == branchId).Select(b => b.Timezone).SingleAsync(ct);
        var day = date ?? WorkdayService.LocalDate(clock.GetUtcNow(), timezone);
        Output.Require(day.Year is >= 2000 and <= 2100, "INVALID_WORKDAY_DATE", 400);
        if (period == "week") day = day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
        var end = day.AddDays(period == "week" ? 7 : 1);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timezone);
        var from = TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue), zone);
        var to = TimeZoneInfo.ConvertTimeToUtc(end.ToDateTime(TimeOnly.MinValue), zone);
        // CompletedAt is the staff submission time, independently of creation and inspection dates.
        var query = db.WorkItems.AsNoTracking().Where(w => w.BranchId == branchId && (w.Kind == "CLEANING" || w.Kind == "REWORK") && w.CompletedAt >= from && w.CompletedAt < to);
        if (mine || !all || await access.HasRole("HK_STAFF", branchId, ct)) query = query.Where(w => db.WorkEvents.Any(e => e.WorkId == w.WorkId && e.EventType == "COMPLETE" && e.ActorUserId == actor.Id));
        if(status=="WITH_INCIDENT")query=query.Where(w=>db.WorkEvents.Any(e=>e.WorkId==w.WorkId&&e.EventType=="CLEANING_WITH_INCIDENT"));
        else if (!string.IsNullOrEmpty(status)) query = query.Where(w => w.Status == status);
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim(); query = query.Where(w => w.Title.Contains(term) || db.Rooms.Any(r => r.RoomId == w.RoomId && r.Code.Contains(term)) || db.WorkEvents.Any(e => e.WorkId == w.WorkId && e.EventType == "COMPLETE" && db.Users.Any(u => u.UserId == e.ActorUserId && u.FullName.Contains(term)))); }
        page = Math.Clamp(page, 1, 1000000);
        var total = await query.CountAsync(ct);
        var ordered = sort == "title" ? query.OrderBy(w => w.Title).ThenBy(w => w.WorkId) : sort == "oldest" ? query.OrderBy(w => w.CompletedAt).ThenBy(w => w.WorkId) : query.OrderByDescending(w => w.CompletedAt).ThenByDescending(w => w.WorkId);
        var items = await ordered.Skip((page - 1) * 25).Take(25)
            .Select(w => new { w.WorkId, w.Kind, w.Title, w.Status, w.CompletedAt,
                completedWithIncident=db.WorkEvents.Any(e=>e.WorkId==w.WorkId&&e.EventType=="CLEANING_WITH_INCIDENT"),
                room = db.Rooms.Where(r => r.RoomId == w.RoomId).Select(r => r.Code).FirstOrDefault(),
                performer = (from e in db.WorkEvents join u in db.Users on e.ActorUserId equals u.UserId
                    where e.WorkId == w.WorkId && e.EventType == "COMPLETE" orderby e.WorkEventId descending select u.FullName).FirstOrDefault() }).ToArrayAsync(ct);
        return new { date = day, endDate = end.AddDays(-1), period, timezone, items, total, page, pageSize = 25 };
    }

    public async Task<object> Housekeepers(long branchId, DateOnly? date, CancellationToken ct)
    {
        await access.Require("CLEANING_ASSIGN", branchId, ct: ct);
        var candidates = await CandidateRows(branchId, "CLEANING", ct);
        var zone = await db.Branches.AsNoTracking().Where(b => b.BranchId == branchId).Select(b => b.Timezone).SingleAsync(ct);
        var day = date ?? WorkdayService.LocalDate(clock.GetUtcNow(), zone);
        var ids = candidates.Select(c => c.UserId).ToArray();
        var shifts = await (from a in db.WorkdayAssignments.AsNoTracking() join s in db.WorkdayShifts on a.ShiftId equals s.ShiftId
            where s.BranchId == branchId && ids.Contains(a.UserId) && a.WorkDate == day && a.Status != "CANCELLED"
            select new { a.UserId, a.Status, s.Name, s.StartMinute, s.EndMinute, a.StartedAt, a.EndedAt }).ToArrayAsync(ct);
        var work = await (from a in db.WorkAssignments.AsNoTracking() join w in db.WorkItems on a.WorkId equals w.WorkId
            where w.BranchId == branchId && (w.Kind == "CLEANING" || w.Kind == "REWORK") && ids.Contains(a.AssigneeUserId)
            select new { a.AssigneeUserId, a.AssignmentStatus, w.WorkId, w.Status, w.CompletedAt }).ToArrayAsync(ct);
        var busy = await db.WorkIntervals.AsNoTracking().Where(t => t.BranchId == branchId && ids.Contains(t.UserId) && t.ClosedAt == null).Select(t => t.UserId).Distinct().ToArrayAsync(ct);
        return new { date = day, timezone = zone, items = candidates.Select(c => new { c.UserId, c.EmployeeCode, c.FullName, accountStatus = "ACTIVE",
            shifts = shifts.Where(s => s.UserId == c.UserId), busy = busy.Contains(c.UserId),
            open = work.Where(w => w.AssigneeUserId == c.UserId && w.AssignmentStatus == "ACTIVE" && !WorkRules.Terminal.Contains(w.Status)).Select(w => w.WorkId).Distinct().Count(),
            completed = work.Where(w => w.AssigneeUserId == c.UserId && w.CompletedAt != null && (w.AssignmentStatus == "ACTIVE" || w.AssignmentStatus == "COMPLETED")).Select(w => w.WorkId).Distinct().Count()
        }) };
    }

    public async Task<object> InspectionQueue(long branchId, int page, string? search, CancellationToken ct, string sort="oldest")
    {
        await access.Require("CLEANING_VIEW", branchId, ct: ct);
        await access.Require("INSPECTION_VIEW", branchId, ct: ct);
        var incidentWork=CleaningWithOpenIssues(branchId);
        var query = db.WorkItems.AsNoTracking().Where(w => w.BranchId == branchId && w.Kind == "CLEANING"
            && !incidentWork.Contains(w.WorkId)
            && (w.Status == "PENDING_INSPECTION" || (w.Status == "REWORK_REQUIRED"
                && db.WorkItems.Any(r => r.ParentWorkId == w.WorkId && r.Kind == "REWORK" && r.Status == "PENDING_REINSPECTION"
                    && !db.WorkItems.Any(i=>i.ParentWorkId==w.WorkId&&i.Kind=="INSPECTION"&&i.Status=="FAILED"&&i.WorkId>r.WorkId&&i.VerifiedAt>=r.CompletedAt))))
            && !db.WorkItems.Any(i => i.ParentWorkId == w.WorkId && i.Kind == "INSPECTION" && !WorkRules.Terminal.Contains(i.Status))
            && !db.WorkItems.Any(r => r.ParentWorkId == w.WorkId && r.Kind == "REWORK" && !WorkRules.Terminal.Contains(r.Status) && r.Status != "PENDING_REINSPECTION")
            && db.Rooms.Any(r => r.RoomId == w.RoomId && r.TurnoverNo == w.TurnoverNo && r.IsActive));
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim(); query = query.Where(w => w.Title.Contains(term) || db.Rooms.Any(r => r.RoomId == w.RoomId && r.Code.Contains(term))); }
        page = Math.Clamp(page, 1, 1000000);
        var total = await query.CountAsync(ct);
        var ordered=sort switch {"oldest"=>query.OrderBy(w=>w.CompletedAt).ThenBy(w=>w.WorkId),"latest"=>query.OrderByDescending(w=>w.WorkId),"title"=>query.OrderBy(w=>w.Title).ThenBy(w=>w.WorkId),_=>throw new ApiException(400,"INVALID_WORK_SORT")};
        var items = await ordered.Skip((page-1)*25).Take(25).Select(w=>new{w.WorkId,w.Title,w.RoomId,room=db.Rooms.Where(r=>r.RoomId==w.RoomId).Select(r=>r.Code).FirstOrDefault(),w.Status,w.CompletedAt}).ToArrayAsync(ct);
        return new { items, total, page, pageSize = 25 };
    }

    public async Task<object> Defects(long branchId, int page, string? search, string? status, CancellationToken ct, string sort="latest")
    {
        await access.Require("INSPECTION_VIEW", branchId, ct: ct);
        Output.Require(status == null || status is "OPEN" or "RESOLVED" or "CANCELLED", "INVALID_DEFECT_STATUS", 400);
        var query = from f in db.InspectionFindings.AsNoTracking()
            join i in db.WorkItems on f.InspectionWorkId equals i.WorkId
            join r in db.Rooms on i.RoomId equals r.RoomId
            where f.BranchId == branchId && i.BranchId == branchId
            select new { f.FindingId, f.InspectionWorkId, i.ParentWorkId, f.Description, f.Status, f.ReworkWorkId, f.ResolvedAt, f.ResolutionNote, room = r.Code };
        if (status != null) query = query.Where(f => f.Status == status);
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim(); query = query.Where(f => f.Description.Contains(term) || f.room.Contains(term)); }
        page = Math.Clamp(page, 1, 1000000);
        var ordered=sort switch {"latest"=>query.OrderByDescending(f=>f.FindingId),"oldest"=>query.OrderBy(f=>f.FindingId),"title"=>query.OrderBy(f=>f.Description).ThenBy(f=>f.FindingId),_=>throw new ApiException(400,"INVALID_WORK_SORT")};
        return new { items = await ordered.Skip((page - 1) * 25).Take(25).ToArrayAsync(ct), total = await query.CountAsync(ct), page, pageSize = 25 };
    }
}
