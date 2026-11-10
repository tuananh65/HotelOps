using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.HotelStructure;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;
using Microsoft.EntityFrameworkCore;

[assembly: InternalsVisibleTo("HotelOps.Checklist.CollectionTests")]

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    public Task<RoomTypeBatchResult> ListChecklistsAsync(SessionReference ticket, long sessionId, long branchId,
        ChecklistCollectionRequest query, CancellationToken ct)
    {
        var filter = ChecklistCollectionQuery.Validate(branchId, query);
        // Exactly the existing detail-read authority, session recheck and transaction/lock order.
        return BatchAsync(ticket, sessionId, branchId, async (_, token) =>
        {
            if (await ChecklistSchema.StateAsync(db, token) != 1) return new(RoomTypeBatchStatus.SchemaRequired);
            var rows = ChecklistCollectionQuery.Rows(db.ChecklistScopes.AsNoTracking(),
                db.InspectionChecklists.AsNoTracking(), db.ChecklistRoomTypes.AsNoTracking(), branchId, filter);
            var total = await rows.CountAsync(token);
            var items = await ChecklistCollectionQuery.Page(rows, db.ChecklistRoomTypes.AsNoTracking(), branchId, filter)
                .AsSplitQuery().TagWith("FE05:UI002:ChecklistCollection").ToArrayAsync(token);
            return new(RoomTypeBatchStatus.Success, new ChecklistCollectionDto(items, total, filter.Page,
                filter.PageSize, branchId, filter.IsActive, filter.Search, filter.Status, filter.RoomTypeId));
        }, ct, permissionCode: "CHECKLIST_MANAGE");
    }
}

// Query composition only. This helper grants no access; execution stays inside BatchAsync.
internal static class ChecklistCollectionQuery
{
    internal static ChecklistCollectionRequest Validate(long branchId, ChecklistCollectionRequest query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var search = query.Search?.Trim();
        if (branchId <= 0 || query.Page < 1 || query.PageSize is < 1 or > 100
            || query.Page > int.MaxValue / query.PageSize || search?.Length > 200
            || search?.Any(char.IsControl) == true || query.RoomTypeId <= 0
            || query.Status is not (null or "DRAFT" or "PUBLISHED" or "RETIRED"))
            throw new ValidationException("Invalid checklist collection filters or pagination.");
        return query with { Search = string.IsNullOrEmpty(search) ? null : search };
    }

    internal static IQueryable<InspectionChecklist> Rows(IQueryable<ChecklistScopeV1> scopes,
        IQueryable<InspectionChecklist> roots, IQueryable<ChecklistRoomTypeV1> types,
        long branchId, ChecklistCollectionRequest filter)
    {
        // Never infer branch from room types, legacy RoomTypeId, caller claims or audit JSON.
        var rows = from root in roots
                   join scope in scopes on root.ChecklistId equals scope.ChecklistId
                   where scope.BranchId == branchId
                   select root;
        if (filter.IsActive is { } active) rows = rows.Where(c => c.IsActive == active);
        if (filter.Search is { } search) rows = rows.Where(c => c.ChecklistCode.Contains(search) || c.ChecklistName.Contains(search));
        // Status means ANY stored version, including history. The response keeps ALL versions.
        if (filter.Status is { } status) rows = rows.Where(c => c.InspectionChecklistVersions.Any(v => v.Status == status));
        if (filter.RoomTypeId is { } type) rows = rows.Where(c => types.Any(t => t.ChecklistId == c.ChecklistId && t.RoomTypeId == type));
        return rows;
    }

    internal static IQueryable<ChecklistListItemDto> Page(IQueryable<InspectionChecklist> rows,
        IQueryable<ChecklistRoomTypeV1> types, long branchId, ChecklistCollectionRequest filter)
        => rows.OrderBy(c => c.ChecklistCode).ThenBy(c => c.ChecklistId)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize)
            .Select(c => new ChecklistListItemDto(c.ChecklistId, branchId, c.ChecklistCode, c.ChecklistName, c.IsActive,
                types.Where(t => t.ChecklistId == c.ChecklistId).OrderBy(t => t.RoomTypeId).Select(t => t.RoomTypeId).ToArray(),
                c.InspectionChecklistVersions.OrderBy(v => v.VersionNo).ThenBy(v => v.VersionId)
                    .Select(v => new ChecklistVersionSummaryDto(v.VersionId, v.VersionNo, v.Status, v.EffectiveAt,
                        v.PublishedBy, v.RoomInspections.Any())).ToArray()));
}