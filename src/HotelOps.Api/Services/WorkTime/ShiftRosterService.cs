using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Services.WorkTime;

public sealed record RosterAssignment(long Id,long EmployeeId,string EmployeeName,string Status,string Version);
public sealed record RosterShift(long Id,long TemplateId,string TemplateName,DateOnly Date,DateTimeOffset StartUtc,DateTimeOffset EndUtc,string Status,int RequiredStaff,IReadOnlyList<RosterAssignment> Assignments);
public sealed record RosterPage(long BranchId,long DepartmentId,DateOnly From,DateOnly To,string Timezone,IReadOnlyList<RosterShift> Shifts,long? NextCursor);
public sealed record ShiftEmployeeOption(long Id,string Name);
public sealed record ShiftEmployeePage(long BranchId,long DepartmentId,IReadOnlyList<ShiftEmployeeOption> Employees,long? NextCursor);
public sealed record ShiftDepartmentOption(long Id,string Name);
public sealed record ShiftBranchOption(long Id,string Name,string Timezone,IReadOnlyList<ShiftDepartmentOption> Departments);
public sealed class ShiftRosterService(HotelOpsDbContext db,ICurrentUserAccessor current,ISqlSessionService sessions,TimeProvider clock)
{
    public async Task<IReadOnlyList<ShiftBranchOption>> OptionsAsync(SessionReference ticket,long sid,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId],ct);
        await ValidateIdentity();
        var departments=await db.Departments.AsNoTracking().Where(d=>d.IsActive&&d.Branch.IsActive)
            .OrderBy(d=>d.BranchId).ThenBy(d=>d.DepartmentName)
            .Select(d=>new{d.DepartmentId,d.DepartmentName,d.BranchId,d.Branch.BranchName,d.Branch.Timezone}).ToArrayAsync(ct);
        var scope=new SqlShiftPreviewScopeData(db,clock);
        var result=new List<ShiftBranchOption>();
        foreach(var branch in departments.GroupBy(d=>d.BranchId))
        {
            var allowed=new List<ShiftDepartmentOption>();
            foreach(var d in branch)
                if(await scope.ActorScopeAsync(ticket.UserId,d.BranchId,d.DepartmentId,ct))
                    allowed.Add(new(d.DepartmentId,d.DepartmentName));
            if(allowed.Count>0)result.Add(new(branch.Key,branch.First().BranchName,branch.First().Timezone ?? "",allowed));
        }
        await ValidateIdentity();
        await tx.CommitAsync(ct);
        return result;

        async Task ValidateIdentity()
        {
            if(!current.TryGetCurrentUser(out var actor)||actor.UserId!=ticket.UserId||actor.SessionId!=sid||actor.IsRestricted||ticket.IsRestricted)
                throw new InternalRequestReadException(401,"SESSION_INVALID");
            var admission=await sessions.ValidateSessionAsync(ticket,ct);
            if(!admission.IsValid||admission.Session?.SessionId!=sid||admission.Session.IsRestricted)
                throw new InternalRequestReadException(401,"SESSION_INVALID");
        }
    }
    internal async Task Gate(SessionReference ticket,long sid,long branch,long department,CancellationToken ct)
    {
        if(!current.TryGetCurrentUser(out var actor)||actor.UserId!=ticket.UserId||actor.SessionId!=sid||actor.IsRestricted||ticket.IsRestricted)
            throw new InternalRequestReadException(401,"SESSION_INVALID");
        var session=await sessions.ValidateSessionAsync(ticket,ct);
        if(!session.IsValid||session.Session?.SessionId!=sid||session.Session.IsRestricted)throw new InternalRequestReadException(401,"SESSION_INVALID");
        if(!await new SqlShiftPreviewScopeData(db,clock).ActorScopeAsync(ticket.UserId,branch,department,ct))throw new InternalRequestReadException(403,"SHIFT_MANAGE_SCOPE_REQUIRED");
    }
    public async Task<ShiftEmployeePage> EmployeesAsync(SessionReference ticket,long sid,long branch,long department,long after,CancellationToken ct)
    {
        if(branch<=0||department<=0||after<0)throw new InternalRequestReadException(400,"INVALID_EMPLOYEE_FILTER");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId],ct);await Gate(ticket,sid,branch,department,ct);
        if(!await WorkdayMembershipStore.InstalledAsync(db,ct))throw new InternalRequestReadException(503,"EMPLOYEE_MEMBERSHIP_STORAGE_REQUIRED");
        var now=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        // Same explicit employment membership as save admission, not User.DepartmentId or management authority.
        // EXISTS avoids exposing duplicate candidates if legacy membership data is inconsistent.
        var rows=await db.Database.SqlQuery<ShiftEmployeeOption>($"""
            SELECT TOP(51) u.user_id AS Id,u.full_name AS Name
            FROM dbo.users u
            WHERE u.user_id>{after} AND u.deleted_at IS NULL
              AND u.account_status='ACTIVE' AND u.employment_status='ACTIVE'
              AND EXISTS(SELECT 1 FROM dbo.user_branch_access b WHERE b.user_id=u.user_id
                AND b.branch_id={branch} AND b.granted_at<={now} AND b.revoked_at IS NULL)
              AND EXISTS(SELECT 1 FROM dbo.employee_department_memberships m
                JOIN dbo.departments d ON d.department_id=m.department_id AND d.branch_id=m.branch_id
                WHERE m.user_id=u.user_id AND m.branch_id={branch} AND m.department_id={department}
                  AND m.granted_at<={now} AND m.revoked_at IS NULL AND d.is_active=1)
            ORDER BY u.user_id
            """).ToArrayAsync(ct);
        await Gate(ticket,sid,branch,department,ct);await tx.CommitAsync(ct);
        var page=rows.Take(50).ToArray();
        return new(branch,department,page,rows.Length>50?page[^1].Id:null);
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
            .Select(a=>new{Row=a,Shift=a.Shift,a.User.FullName}).ToArrayAsync(ct);
        if(assignments.Length>10000)throw new InternalRequestReadException(503,"ROSTER_ASSIGNMENT_LIMIT");
        var assignmentKeys=assignments.Select(a=>a.Row.ShiftAssignmentId.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        var revisions=await db.AuditLogs.AsNoTracking().Where(a=>a.EntityType=="shift_assignments"&&assignmentKeys.Contains(a.EntityId))
            .GroupBy(a=>a.EntityId).Select(g=>new{Id=g.Key,Revision=g.Max(a=>a.AuditId)}).ToDictionaryAsync(a=>a.Id,a=>a.Revision,ct);
        var grouped=assignments.ToLookup(a=>a.Row.ShiftId,a=>new RosterAssignment(a.Row.ShiftAssignmentId,a.Row.UserId,a.FullName,a.Row.AssignmentStatus,
            ShiftAssignmentRevision.Version(a.Row,a.Shift,revisions.GetValueOrDefault(a.Row.ShiftAssignmentId.ToString(System.Globalization.CultureInfo.InvariantCulture)))));
        await Gate(ticket,sid,branch,department,ct);await tx.CommitAsync(ct);
        return new(branch,department,from,to,timezone,page.Select(s=>new RosterShift(s.ShiftId,s.ShiftTemplateId,s.ShiftName,s.ShiftDate,
            new DateTimeOffset(DateTime.SpecifyKind(s.ScheduledStartAt,DateTimeKind.Utc)),new DateTimeOffset(DateTime.SpecifyKind(s.ScheduledEndAt,DateTimeKind.Utc)),s.Status,s.RequiredStaffCount,grouped[s.ShiftId].ToArray())).ToArray(),rows.Length>50?page[^1].ShiftId:null);
    }
}
