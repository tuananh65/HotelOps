using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.OperationalTasks;

internal sealed record TaskRequesterProof(string SourceType, long? SourceId, long RequestedBy, long TaskCreatedBy);

internal static class TaskRequesterSource
{
    // UC102 explicitly supports an optional Internal Request source. Other legacy
    // polymorphic types have no verified requester contract and remain fail-closed.
    internal static async Task<TaskRequesterProof?> ResolveAsync(HotelOpsDbContext db, OperationalTask task, CancellationToken ct)
    {
        if (task.SourceId == null && task.SourceType is null or "MANUAL")
            return new("MANUAL", null, task.CreatedBy, task.CreatedBy);
        if (task.SourceType != "INTERNAL_REQUEST" || task.SourceId is not > 0) return null;
        var requester = await db.InternalRequests.AsNoTracking().Where(r => r.InternalRequestId == task.SourceId && r.BranchId == task.BranchId
            && (r.RequesterDepartmentId == null || r.RequesterDepartment!.BranchId == task.BranchId)
            && (r.ReceivingDepartmentId == null || r.ReceivingDepartment!.BranchId == task.BranchId)
            && (r.RoomId == null || r.Room!.BranchId == task.BranchId)
            && (r.AreaId == null || r.Area!.BranchId == task.BranchId)
            && (r.RelatedIncidentId == null || r.RelatedIncident!.BranchId == task.BranchId))
            .Select(r => (long?)r.RequesterId).SingleOrDefaultAsync(ct);
        return requester is > 0 ? new("INTERNAL_REQUEST", task.SourceId, requester.Value, task.CreatedBy) : null;
    }
}
