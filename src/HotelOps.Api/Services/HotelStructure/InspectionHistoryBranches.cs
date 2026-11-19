using HotelOps.Api.Services.Authentication;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed record InspectionHistoryBranchDto(long Id, string Name, bool IsPrimary);

public sealed partial class RoomTypeCreationService
{
    public Task<RoomTypeBatchResult> InspectionHistoryBranchesAsync(SessionReference ticket, long sid, CancellationToken ct, bool assignment = false)
        => BatchAsync(ticket, sid, null, async (actor, token) =>
        {
            var ids = await HistoryBranches("INSPECTION_VIEW", null, token);
            var primary = await db.Users.AsNoTracking().Where(u => u.UserId == actor)
                .Select(u => u.PrimaryBranchId).SingleAsync(token);
            var items = await db.Branches.AsNoTracking().Where(b => b.IsActive && ids.Contains(b.BranchId))
                .OrderBy(b => b.BranchName)
                .Select(b => new InspectionHistoryBranchDto(b.BranchId, b.BranchName, b.BranchId == primary)).ToArrayAsync(token);
            if (!await HistoryFinal("INSPECTION_VIEW", ids, token)) return new(RoomTypeBatchStatus.Forbidden);
            if (assignment)
            {
                var allowed = new List<InspectionHistoryBranchDto>();
                foreach (var item in items)
                    if (await EligibleInspector(actor, item.Id, token)
                        && (await permissions.EvaluateBranchAsync("INSPECTION_PERFORM", item.Id, token)).IsGranted)
                        allowed.Add(item);
                items = allowed.ToArray();
            }
            return new(RoomTypeBatchStatus.Success, items);
        }, ct, allowScopedList: true, requireRoomTypeSchema: false, permissionCode: "INSPECTION_VIEW");
}
