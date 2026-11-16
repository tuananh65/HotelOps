using HotelOps.Api.Data;
using HotelOps.Api.DTOs.MaterialRequests;
using HotelOps.Api.Services.InternalRequests;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.MaterialRequests;

internal static class MaterialLinkedWorkRead
{
    private sealed class Link
    {
        public long RequestId { get; set; }
        public bool Mandatory { get; set; }
    }

    internal static async Task<MaterialRequestDto> ReadAsync(HotelOpsDbContext db, MaterialRequestDto material,
        long actor, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null)
            throw new InvalidOperationException("Linked work disclosure requires the authorized read transaction.");
        if (!await InternalRequestCompletionService.InstalledAsync(db, ct)) return material;
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var scope = await new InternalRequestReadAccess(db).ReadAsync(actor, now, ct);
        var links = db.Database.SqlQuery<Link>($"SELECT internal_request_id AS RequestId,is_mandatory AS Mandatory FROM dbo.internal_request_material_links WHERE material_request_id={material.RequestId}");
        var rows = await (from link in links
            join request in scope.Apply(db.InternalRequests.AsNoTracking(), now)
                on link.RequestId equals request.InternalRequestId
            where request.BranchId == material.BranchId
            orderby request.CreatedAt descending, request.InternalRequestId descending
            select new MaterialLinkedRequestDto(request.InternalRequestId, request.RequestNo, request.Status, link.Mandatory))
            .ToArrayAsync(ct);
        return material with { LinkedWorkAvailable = true, LinkedInternalRequests = rows };
    }

    internal static async Task<bool> CanDiscloseAsync(HotelOpsDbContext db, long actor, long branch,
        long[] ids, DateTime now, CancellationToken ct)
    {
        if (ids.Length == 0) return true;
        var scope = await new InternalRequestReadAccess(db).ReadAsync(actor, now, ct);
        return await scope.Apply(db.InternalRequests.AsNoTracking(), now)
            .CountAsync(r => r.BranchId == branch && ids.Contains(r.InternalRequestId), ct) == ids.Distinct().Count();
    }
}
