using HotelOps.Api.Data;
using HotelOps.Api.DTOs.MaterialRequests;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.Incidents;
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
        // Incident linkage is a baseline FK and must not depend on the separate
        // Internal Request material-link schema capability.
        if (material.IncidentId is long incident)
        {
            var incidents = await IncidentReadAccess.ReadAsync(db, actor, material.BranchId, ct);
            var detail = await incidents.Apply(db.Incidents.AsNoTracking()).Where(i => i.IncidentId == incident)
                .Select(i => new MaterialLinkedIncidentDto(i.IncidentId, i.IncidentNo, i.IncidentTitle, i.Status))
                .SingleOrDefaultAsync(ct);
            material = material with { LinkedIncident = detail };
        }
        if (!await InternalRequestCompletionService.InstalledAsync(db, ct)) return material;
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var scope = await new InternalRequestReadAccess(db).ReadAsync(actor, now, ct);
        var rows = await Query(db, material.RequestId, material.BranchId,
            scope.Apply(db.InternalRequests.AsNoTracking(), now)).ToArrayAsync(ct);
        return material with { LinkedWorkAvailable = true, LinkedInternalRequests = rows };
    }

    internal static IQueryable<MaterialLinkedRequestDto> Query(HotelOpsDbContext db, long materialId,
        long branchId, IQueryable<HotelOps.Api.Entities.InternalRequest> readableRequests)
    {
        var links = db.Database.SqlQuery<Link>($"SELECT internal_request_id AS RequestId,is_mandatory AS Mandatory FROM dbo.internal_request_material_links WHERE material_request_id={materialId}");
        return from link in links
            join request in readableRequests
                on link.RequestId equals request.InternalRequestId
            where request.BranchId == branchId
            orderby request.CreatedAt descending, request.InternalRequestId descending
            select new MaterialLinkedRequestDto(request.InternalRequestId, request.RequestNo, request.Status, link.Mandatory);
    }

    internal static async Task<bool> CanDiscloseAsync(HotelOpsDbContext db, long actor, long branch,
        long[] ids, DateTime now, CancellationToken ct)
    {
        if (ids.Length == 0) return true;
        var scope = await new InternalRequestReadAccess(db).ReadAsync(actor, now, ct);
        return await scope.Apply(db.InternalRequests.AsNoTracking(), now)
            .CountAsync(r => r.BranchId == branch && ids.Contains(r.InternalRequestId), ct) == ids.Distinct().Count();
    }

    internal static async Task<bool> CanDiscloseIncidentAsync(HotelOpsDbContext db, long actor,
        long branch, long incident, CancellationToken ct)
    {
        var scope = await IncidentReadAccess.ReadAsync(db, actor, branch, ct);
        return await scope.Apply(db.Incidents.AsNoTracking()).AnyAsync(i => i.IncidentId == incident, ct);
    }
}
