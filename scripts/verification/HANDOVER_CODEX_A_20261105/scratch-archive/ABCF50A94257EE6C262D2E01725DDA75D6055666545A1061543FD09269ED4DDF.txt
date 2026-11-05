using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Security.Authorization;

public sealed record CleaningTaskAssignee(long UserId, string Status, DateTime AssignedAt, DateTime? EndedAt);
public sealed record CleaningTaskAccess(long BranchId, bool RoomActive, string TaskStatus,
    IReadOnlyList<CleaningTaskAssignee> Assignments);

public interface ICleaningTaskAccessReader
{
    Task<CleaningTaskAccess?> ReadAsync(long cleaningTaskId, CancellationToken cancellationToken);
}

public sealed class EfCleaningTaskAccessReader(HotelOpsDbContext db) : ICleaningTaskAccessReader
{
    public async Task<CleaningTaskAccess?> ReadAsync(long cleaningTaskId, CancellationToken cancellationToken)
    {
        var task = await db.CleaningTasks.AsNoTracking().Where(t => t.CleaningTaskId == cleaningTaskId)
            .Select(t => new
            {
                t.Room.BranchId,
                RoomActive = t.Room.IsActive,
                t.Status,
                Assignments = t.CleaningAssignments.Select(a => new CleaningTaskAssignee(
                    a.AssigneeUserId, a.AssignmentStatus, a.AssignedAt, a.EndedAt)).ToList()
            }).SingleOrDefaultAsync(cancellationToken);
        return task is null ? null : new CleaningTaskAccess(task.BranchId, task.RoomActive, task.Status, task.Assignments);
    }
}
