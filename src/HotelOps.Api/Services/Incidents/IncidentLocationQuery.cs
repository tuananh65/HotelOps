using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Incidents;
using HotelOps.Api.Entities;

namespace HotelOps.Api.Services.Incidents;

internal static class IncidentLocationQuery
{
    // Always compose onto the caller's authorized population, never start a new DbSet query.
    internal static IQueryable<Incident> Apply(IQueryable<Incident> query, IncidentQueryRequest input)
    {
        if (input.BranchId is <= 0 || input.RoomId is <= 0 || input.AreaId is <= 0 || input.AssetId is <= 0)
            throw new ValidationException("Location identifiers must be positive when supplied.");
        if (input.SimilarOpenOnly)
        {
            if (!string.IsNullOrWhiteSpace(input.Status) || !string.IsNullOrWhiteSpace(input.Keyword)
                || input.PriorityId != null || input.SeverityId != null || input.CategoryId != null
                || input.DetectedFrom != null || input.DetectedTo != null)
                throw new ValidationException("Similar incident review cannot be narrowed by unrelated filters.");
            if (input.BranchId is not > 0 || input.RoomId == null && input.AreaId == null && input.AssetId == null)
                throw new ValidationException("Select a branch and at least one room, area or asset before reviewing similar incidents.");
            return query.Where(i => i.BranchId == input.BranchId
                && (i.Status == "OPEN" || i.Status == "IN_PROGRESS" || i.Status == "RESOLVED")
                && (input.RoomId != null && i.RoomId == input.RoomId
                    || input.AreaId != null && i.AreaId == input.AreaId
                    || input.AssetId != null && i.AssetId == input.AssetId));
        }
        if (input.RoomId is long room) query = query.Where(i => i.RoomId == room);
        if (input.AreaId is long area) query = query.Where(i => i.AreaId == area);
        if (input.AssetId is long asset) query = query.Where(i => i.AssetId == asset);
        return query;
    }
}
