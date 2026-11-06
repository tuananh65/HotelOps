using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Services.HotelStructure;

public sealed record AssignedCleaningHeader(long Id, long BranchId, long RoomId, string RoomNumber, string TaskNumber, string RecordedStatus, long AssignmentId);
public sealed partial class RoomTypeCreationService
{
    // Explicit-user evaluator: no HttpContext/producer authority used to authorize a recipient.
    public async Task<bool> CanReadAssignedCleaningAsync(long recipient, long branch, long task, long assignment, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        if (!await db.Users.AsNoTracking().AnyAsync(x => x.UserId == recipient && x.AccountStatus == "ACTIVE" && x.EmploymentStatus == "ACTIVE" && x.DeletedAt == null, ct)) return false;
        var source = await db.CleaningAssignments.AsNoTracking().AnyAsync(x => x.AssignmentId == assignment && x.CleaningTaskId == task
            && x.CleaningTask.Room.BranchId == branch && x.CleaningTask.Room.IsActive && x.AssignedAt <= now, ct);
        if (!source) return false;
        var own = await db.CleaningAssignments.AsNoTracking().AnyAsync(x => x.AssignmentId == assignment && x.AssigneeUserId == recipient
            && x.AssignmentStatus == "ACTIVE" && x.EndedAt == null, ct);
        var data = new EfPermissionDataSource(db);
        if (!await data.IsActiveBranchAsync(branch, ct)) return false;
        foreach (var grant in await data.ReadPathsAsync(recipient, "CLEANING_VIEW", now, ct))
        {
            if (grant.PermissionCode != "CLEANING_VIEW" || !PermissionGrantRules.IsEligible(grant, now)) continue;
            var scope = PermissionGrantRules.EffectiveScope(grant);
            if (scope == "GLOBAL" || grant.BranchId == branch && (scope == "BRANCH" || scope == "OWN" && own)) return true;
        }
        return false;
    }
    public Task<RoomTypeBatchResult> ReadAssignedCleaningAsync(SessionReference ticket, long sid, long branch, long task, long assignment, CancellationToken ct)
    {
        if (task <= 0 || assignment <= 0) throw new System.ComponentModel.DataAnnotations.ValidationException("Task and assignment required.");
        return BatchAsync(ticket, sid, branch, async (_, token) =>
        {
            var dto = await db.CleaningAssignments.AsNoTracking().Where(x => x.AssignmentId == assignment && x.CleaningTaskId == task && x.CleaningTask.Room.BranchId == branch)
                .Select(x => new AssignedCleaningHeader(task, branch, x.CleaningTask.RoomId, x.CleaningTask.Room.RoomNumber, x.CleaningTask.TaskNo, x.CleaningTask.Status, assignment)).SingleOrDefaultAsync(token);
            return dto == null ? new(RoomTypeBatchStatus.NotFound) : new(RoomTypeBatchStatus.Success, dto);
        }, ct, requireRoomTypeSchema: false, featurePermission: async token =>
            new PermissionEvaluation(await CanReadAssignedCleaningAsync(ticket.UserId, branch, task, assignment, token) ? PermissionDecision.Granted : PermissionDecision.Denied));
    }
}
