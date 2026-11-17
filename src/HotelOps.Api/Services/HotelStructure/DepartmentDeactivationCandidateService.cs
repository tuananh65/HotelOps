using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.HotelStructure;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Dependencies;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    // Compatibility for historical diagnostic tooling; runtime admission uses current facts.
    public static IReadOnlyList<string> DepartmentDeactivationReleaseGates { get; } = Array.AsReadOnly(Array.Empty<string>());

    public async Task<RoomTypeBatchResult> DeactivateDepartmentCandidateAsync(SessionReference ticket, long sessionId,
        long branchId, long id, string expected, DeactivateStructureRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ticket); ArgumentNullException.ThrowIfNull(request);
        if (!request.Confirmation) throw new ValidationException("Confirmation required.");
        var reason = Batch2Reason(request.Reason); Batch2Tag(expected);
        if (branchId <= 0 || id <= 0) throw new ValidationException("Positive branch and department required.");
        return await BatchAsync(ticket, sessionId, branchId, async (actor, token) =>
        {
            if (!await DepartmentDeactivationAdmin(actor, branchId, token)) return new(RoomTypeBatchStatus.Forbidden);
            var row = await db.Departments.FromSqlInterpolated(
                $"SELECT * FROM dbo.departments WITH(UPDLOCK,HOLDLOCK) WHERE department_id={id} AND branch_id={branchId}")
                .AsNoTracking().SingleOrDefaultAsync(token);
            if (row == null) return new(RoomTypeBatchStatus.NotFound);
            if (!row.IsActive) return new(RoomTypeBatchStatus.Inactive);
            var before = DepartmentDto(row);
            if (await DepartmentTag(before, token) != expected) return new(RoomTypeBatchStatus.Stale);
            if (!await DepartmentDeactivationSchema(token)) return new(RoomTypeBatchStatus.SchemaRequired);
            // Serializable retains dependency ranges; admission writers retain active-department
            // locks. A concurrent conflict rolls back rather than admitting an orphan binding.
            var dependencies = await DepartmentDeactivationFacts(id, token);
            if (dependencies.Length != 0) return new(RoomTypeBatchStatus.DependenciesExist,
                new DepartmentDeactivationReview(id, branchId, true, dependencies));
            var affected = await DepartmentDeactivationRows(db.Departments, before)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.IsActive, false), token);
            if (affected != 1) return new(RoomTypeBatchStatus.Stale);
            var after = before with { IsActive = false };
            await BatchAuditAsync(actor, branchId, "departments", id, before,
                new { record = after, reason }, ["isActive"], token);
            return new(RoomTypeBatchStatus.Success, after, await DepartmentTag(after, token));
        }, ct, requireRoomTypeSchema: false,
            eligibility: token => DepartmentDeactivationAdmin(ticket.UserId, branchId, token));
    }

    internal static RoomTypeBatchStatus DepartmentDeactivationDecision(DependencySnapshot snapshot,
        bool activeUsers, bool warehouses, bool ownerCoverageComplete)
    {
        if (activeUsers || warehouses || snapshot.Facts.Any(f => f.Classification is DependencyClassification.BLOCKING or DependencyClassification.TRANSFER_REQUIRED))
            return RoomTypeBatchStatus.DependenciesExist;
        if (!ownerCoverageComplete || !snapshot.CanDeactivate || snapshot.Unresolved.Count != 0
            || snapshot.Facts.Any(f => f.Classification == DependencyClassification.UNKNOWN_POLICY))
            return RoomTypeBatchStatus.Unavailable;
        return RoomTypeBatchStatus.Success;
    }

    internal static IQueryable<HotelOps.Api.Entities.Department> DepartmentDeactivationRows(
        IQueryable<HotelOps.Api.Entities.Department> rows, DepartmentInformationDto before)
        => rows.Where(d => d.DepartmentId == before.Id && d.BranchId == before.BranchId && d.IsActive
            && d.DepartmentCode == before.Code && d.DepartmentName == before.Name
            && d.ScopeDescription == before.Description && d.ManagerUserId == before.ManagerUserId);
}
