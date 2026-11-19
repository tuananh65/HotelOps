using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed record AssignedCleaningRecordDto(long Id, long BranchId, string RoomNumber,
    string TaskNumber, string RecordedStatus, string CleaningType, string? Instructions, DateTime? DueAt);

public sealed partial class RoomTypeCreationService
{
    public Task<RoomTypeBatchResult> ReadOwnCleaningRecordAsync(SessionReference ticket, long sid, long branch, long id, CancellationToken ct)
        => BatchAsync(ticket, sid, branch, async (_, token) => {
            var row = await db.CleaningTasks.AsNoTracking().Where(t => t.CleaningTaskId == id && t.Room.BranchId == branch)
                .Select(t => new AssignedCleaningRecordDto(t.CleaningTaskId, branch, t.Room.RoomNumber, t.TaskNo,
                    t.Status, t.CleaningType.DisplayName, t.Instruction, t.DueAt)).SingleOrDefaultAsync(token);
            return row == null ? new(RoomTypeBatchStatus.NotFound) : new(RoomTypeBatchStatus.Success,
                row with { DueAt = row.DueAt is { } time ? DateTime.SpecifyKind(time, DateTimeKind.Utc) : null });
        }, ct, requireRoomTypeSchema: false, featurePermission: async token => {
            var assignment = await db.CleaningAssignments.AsNoTracking().Where(a => a.CleaningTaskId == id
                && a.CleaningTask.Room.BranchId == branch && a.AssigneeUserId == ticket.UserId
                && a.AssignmentStatus == "ACTIVE" && a.EndedAt == null)
                .OrderByDescending(a => a.AssignmentId).Select(a => (long?)a.AssignmentId).FirstOrDefaultAsync(token);
            return new PermissionEvaluation(assignment is { } source && await CanReadAssignedCleaningAsync(ticket.UserId, branch, id, source, token)
                ? PermissionDecision.Granted : PermissionDecision.Denied);
        });
}
