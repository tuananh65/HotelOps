using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Services.WorkTime;

public sealed record RosterAssignment(long Id,long EmployeeId,string EmployeeName,string Status);
public sealed record RosterShift(long Id,long TemplateId,string TemplateName,DateOnly Date,DateTimeOffset StartUtc,DateTimeOffset EndUtc,string Status,int RequiredStaff,IReadOnlyList<RosterAssignment> Assignments);
public sealed record RosterPage(long BranchId,long DepartmentId,DateOnly From,DateOnly To,string Timezone,IReadOnlyList<RosterShift> Shifts,long? NextCursor);
public sealed class ShiftRosterService(HotelOpsDbContext db,ICurrentUserAccessor current,ISqlSessionService sessions,TimeProvider clock)
{
    internal async Task Gate(SessionReference ticket,long sid,long branch,long department,CancellationToken ct)
    {
        if(!current.TryGetCurrentUser(out var actor)||actor.UserId!=ticket.UserId||actor.SessionId!=sid||actor.IsRestricted||ticket.IsRestricted)
            throw new InternalRequestReadException(401,"SESSION_INVALID");
        var session=await sessions.ValidateSessionAsync(ticket,ct);
        if(!session.IsValid||session.Session?.SessionId!=sid||session.Session.IsRestricted)throw new InternalRequestReadException(401,"SESSION_INVALID");
        if(!await new SqlShiftPreviewScopeData(db,clock).ActorScopeAsync(ticket.UserId,branch,department,ct))throw new InternalRequestReadException(403,"SHIFT_MANAGE_SCOPE_REQUIRED");
    }
    public async Task<RosterPage> ReadAsync(SessionReference ticket,long sid,long branch,long department,DateOnly from,long after,CancellationToken ct)
    {
        if(branch<=0||department<=0||from==default||from> DateOnly.MaxValue.AddDays(-6)||after<0)throw new InternalRequestReadException(400,"INVALID_ROSTER_FILTER");
        var to=from.AddDays(6);
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId],ct);await Gate(ticket,sid,branch,department,ct);
        var timezone=await db.Branches.AsNoTracking().Where(b=>b.BranchId==branch).Select(b=>b.Timezone).SingleAsync(ct);
        if(string.IsNullOrWhiteSpace(timezone))throw new InternalRequestReadException(503,"BRANCH_TIMEZONE_REQUIRED");
        var rows=await db.WorkShifts.AsNoTracking().Where(s=>s.BranchId==branch&&s.DepartmentId==department&&s.ShiftDate>=from&&s.ShiftDate<=to&&s.ShiftId>after)
            .OrderBy(s=>s.ShiftId).Take(51).Select(s=>new{s.ShiftId,s.ShiftTemplateId,s.ShiftTemplate.ShiftName,s.ShiftDate,s.ScheduledStartAt,s.ScheduledEndAt,s.Status,s.RequiredStaffCount}).ToArrayAsync(ct);
        var page=rows.Take(50).ToArray();var ids=page.Select(s=>s.ShiftId).ToArray();
        var assignments=await db.ShiftAssignments.AsNoTracking().Where(a=>ids.Contains(a.ShiftId)).OrderBy(a=>a.ShiftAssignmentId).Take(10001)
            .Select(a=>new{a.ShiftId,Assignment=new RosterAssignment(a.ShiftAssignmentId,a.UserId,a.User.FullName,a.AssignmentStatus)}).ToArrayAsync(ct);
        if(assignments.Length>10000)throw new InternalRequestReadException(503,"ROSTER_ASSIGNMENT_LIMIT");
        var grouped=assignments.ToLookup(a=>a.ShiftId,a=>a.Assignment);
        await Gate(ticket,sid,branch,department,ct);await tx.CommitAsync(ct);
        return new(branch,department,from,to,timezone,page.Select(s=>new RosterShift(s.ShiftId,s.ShiftTemplateId,s.ShiftName,s.ShiftDate,
            new DateTimeOffset(DateTime.SpecifyKind(s.ScheduledStartAt,DateTimeKind.Utc)),new DateTimeOffset(DateTime.SpecifyKind(s.ScheduledEndAt,DateTimeKind.Utc)),s.Status,s.RequiredStaffCount,grouped[s.ShiftId].ToArray())).ToArray(),rows.Length>50?page[^1].ShiftId:null);
    }
}
