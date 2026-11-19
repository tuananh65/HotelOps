using HotelOps.Api.Services.Authentication;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

// Historical facts only; this contract cannot authorize an execution command.
public sealed record AssignedReworkRecordDto(long Id, long BranchId, string RoomNumber,
    string TaskNumber, long InspectionId, string RecordedStatus, string Reason,
    string? Note, DateTime RequestedAt, DateTime? CompletedAt);

public sealed partial class RoomTypeCreationService
{
    public Task<RoomTypeBatchResult> ReadAssignedReworkRecordAsync(SessionReference ticket, long sid,
        long branch, long id, CancellationToken ct)
        => BatchAsync(ticket, sid, branch, async (actor, token) =>
        {
            var row = await db.CleaningReworks.AsNoTracking()
                .Where(w => w.ReworkId == id && w.AssigneeUserId == actor
                    && w.CleaningTask.Room.BranchId == branch)
                .Select(w => new AssignedReworkRecordDto(w.ReworkId, branch,
                    w.CleaningTask.Room.RoomNumber, w.CleaningTask.TaskNo, w.InspectionId,
                    w.Status, w.Reason, w.Note, w.RequestedAt, w.CompletedAt))
                .SingleOrDefaultAsync(token);
            return row == null ? new(RoomTypeBatchStatus.NotFound) : new(RoomTypeBatchStatus.Success, row with {
                RequestedAt = DateTime.SpecifyKind(row.RequestedAt, DateTimeKind.Utc),
                CompletedAt = row.CompletedAt is { } completed ? DateTime.SpecifyKind(completed, DateTimeKind.Utc) : null
            });
        }, ct, requireRoomTypeSchema: false,
            featurePermission: token => ReworkAccess(ticket.UserId, branch, id, "CLEANING_VIEW", token));
}
