using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class WorkService
{
    // A planned shift or an unclosed shift from yesterday is not current attendance.
    private async Task<IQueryable<WorkdayAssignment>> CurrentCleaningShift(long branchId, CancellationToken ct)
    {
        var timezone = await db.Branches.AsNoTracking().Where(b => b.BranchId == branchId)
            .Select(b => b.Timezone).SingleAsync(ct);
        var instant = clock.GetUtcNow();
        var local = TimeZoneInfo.ConvertTime(instant, TimeZoneInfo.FindSystemTimeZoneById(timezone));
        var date = DateOnly.FromDateTime(local.DateTime);
        var minute = local.Hour * 60 + local.Minute;
        var utc = instant.UtcDateTime;
        return from a in db.WorkdayAssignments.AsNoTracking()
            join s in db.WorkdayShifts on a.ShiftId equals s.ShiftId
            where s.BranchId == branchId && s.IsActive && a.WorkDate == date
                && a.Status == "WORKING" && a.StartedAt != null && a.StartedAt <= utc && a.EndedAt == null
                && s.StartMinute <= minute && minute < s.EndMinute
            select a;
    }

    private IQueryable<long> HousekeepingStaffIds(long branchId)
    {
        var now = Now;
        return from ur in db.UserRoles.AsNoTracking() join role in db.Roles on ur.RoleId equals role.RoleId
            where ur.BranchId == branchId && role.Code == "HK_STAFF" && role.IsActive
                && ur.RevokedAt == null && ur.AssignedAt <= now && (ur.ExpiresAt == null || ur.ExpiresAt > now)
            select ur.UserId;
    }

    private IQueryable<long> UnacceptedHousekeepers(long? exceptWorkId = null) =>
        from a in db.WorkAssignments
        join w in db.WorkItems on a.WorkId equals w.WorkId
        where a.AssignmentStatus == "ACTIVE" && a.AcceptedAt == null && w.WorkId != exceptWorkId
            && (w.Kind == "CLEANING" || w.Kind == "REWORK") && !WorkRules.Terminal.Contains(w.Status)
        select a.AssigneeUserId;

    private async Task RequireCleaningAssignee(long userId, long branchId, CancellationToken ct, long? exceptWorkId = null)
    {
        // Same transaction lock/order as supply reservation and request writers.
        await StaffSupplyService.Lock(db, ct);
        await access.EligibleFor(userId, branchId, "CLEANING_EXECUTE", ct);
        Output.Require(await HousekeepingStaffIds(branchId).ContainsAsync(userId, ct), "CLEANING_STAFF_REQUIRED");
        Output.Require(await (await CurrentCleaningShift(branchId, ct)).AnyAsync(a => a.UserId == userId, ct),
            "CLEANING_ASSIGNEE_NOT_ON_SHIFT");
        Output.Require(!await UnacceptedHousekeepers(exceptWorkId).ContainsAsync(userId, ct),
            "CLEANING_ASSIGNEE_HAS_UNACCEPTED_WORK");
    }

    private async Task RequireExistingCleaningExecution(long workId,long branchId,CancellationToken ct)
    {
        await access.EligibleFor(actor.Id,branchId,"CLEANING_EXECUTE",ct);
        Output.Require(await HousekeepingStaffIds(branchId).ContainsAsync(actor.Id,ct),"CLEANING_STAFF_REQUIRED");
        // An active assignment can carry into a later eligible shift. Its assignment
        // timestamp is not an attendance requirement for that new shift.
        if (await db.WorkAssignments.AnyAsync(a => a.WorkId == workId && a.BranchId == branchId
                && a.AssigneeUserId == actor.Id && a.AssignmentStatus == "ACTIVE", ct)
            && await HotelOps.Api.Compact.Security.ShiftCommandAccess.CanOperate(
                db, actor.Id, branchId, Now, ct, "CLEANING_EXECUTE", owns: true)
            && await (from day in await CurrentCleaningShift(branchId, ct)
                join shift in db.WorkdayShifts on day.ShiftId equals shift.ShiftId
                join role in db.Roles on shift.RoleId equals role.RoleId
                where day.UserId == actor.Id && role.Code == "HK_STAFF" && role.IsActive
                select day.AssignmentId).AnyAsync(ct)) return;
        Output.Require(await HotelOps.Api.Compact.Security.CleaningContinuationAccess.CanFinishExisting(
            db,actor,clock,workId,branchId,ct),"CLEANING_ASSIGNEE_NOT_ON_SHIFT");
    }

    public async Task<object> CleaningCreationOptions(long branchId, CancellationToken ct)
    {
        await access.Require("CLEANING_ASSIGN", branchId, ct: ct);
        var canCreate = await (await CurrentCleaningShift(branchId, ct)).AnyAsync(a => a.UserId == actor.Id, ct);
        return new { canCreate, candidates = await CandidateRows(branchId, "CLEANING", ct, onShiftOnly: true) };
    }

    private IQueryable<Room> RoomsNeedingCleaning(long branchId) =>
        from room in db.Rooms.AsNoTracking()
        join readiness in db.VRoomReadinesses on room.RoomId equals readiness.RoomId
        where room.BranchId == branchId && room.IsActive
            && room.OperationalStatus != "OCCUPIED" && readiness.Readiness == "PENDING_CLEANING"
            && !db.WorkItems.Any(w => w.RoomId == room.RoomId
                && (w.Kind == "CLEANING" || w.Kind == "INSPECTION" || w.Kind == "REWORK")
                && !WorkRules.Terminal.Contains(w.Status))
        select room;

    public async Task<object> CleaningRooms(long branchId, int page, string? search, long? roomId, CancellationToken ct)
    {
        await access.Require("CLEANING_ASSIGN", branchId, ct: ct);
        await access.Require("CLEANING_VIEW", branchId, ct: ct);
        var query = RoomsNeedingCleaning(branchId);
        if (roomId != null) query = query.Where(r => r.RoomId == roomId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(r => r.Code.Contains(term));
        }
        page = Math.Clamp(page, 1, 1000000);
        const int pageSize = 25;
        var total = await query.CountAsync(ct);
        var items = await (from room in query
            join floor in db.Locations on room.LocationId equals floor.LocationId
            join type in db.RoomTypes on room.RoomTypeId equals type.RoomTypeId
            orderby floor.FloorNumber, room.Code, room.RoomId
            select new { room.RoomId, room.Code, room.RowVersion, room.TurnoverNo,
                floor = floor.Name, roomType = type.Name, type.Capacity, readiness = "PENDING_CLEANING" })
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
        return new { items, total, page, pageSize };
    }
}
