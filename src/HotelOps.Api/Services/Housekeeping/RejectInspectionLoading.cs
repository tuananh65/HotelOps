using System.Runtime.CompilerServices;
using HotelOps.Api.Services.HotelStructure;

[assembly: InternalsVisibleTo("HotelOps.Inspection.LoaderTests")]

namespace HotelOps.Api.Services.Housekeeping;

// Internal service command, not an HTTP DTO or a supplied domain snapshot.
internal sealed record RejectInspectionLoadCommand(long BranchId, long InspectionId, RejectInspectionInput Input);
internal sealed record RejectInspectionEvidence(long AttachmentId, long EntityId, string EntityType, DateTime? DeletedAt);
internal sealed record RejectInspectionLoadedSource(RejectInspectionSource Domain, IReadOnlyList<RejectInspectionEvidence> Evidence, long? CurrentCleaningTaskId = null);
internal sealed record RejectInspectionLoadResult(RoomTypeBatchStatus Status, string? Blocker = null,
    IReadOnlyList<string>? Errors = null, RejectInspectionLoadedSource? Source = null);
internal sealed class RejectInspectionOwnershipException : Exception;
internal sealed class RejectInspectionSourceUnavailable(string code) : Exception(code);

// Narrow test seam for this read operation only. The production implementation uses the existing DbContext.
internal interface IRejectInspectionReadSession
{
    void RequireCallerTransaction();
    Task<RoomTypeBatchStatus?> AuthorizeAsync(long branch, CancellationToken ct);
    Task<RejectInspectionLoadedSource?> ReadLockedAsync(long branch, long inspection, CancellationToken ct);
}

internal static class RejectInspectionLoading
{
    internal static async Task<RejectInspectionLoadResult> LoadAsync(IRejectInspectionReadSession session,
        RejectInspectionLoadCommand command, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        session.RequireCallerTransaction();
        if (command.BranchId <= 0 || command.InspectionId <= 0 || command.Input == null)
            return new(RoomTypeBatchStatus.InvalidReference);
        if (await session.AuthorizeAsync(command.BranchId, ct) is { } denied) return new(denied);
        RejectInspectionLoadedSource? loaded;
        try { loaded = await session.ReadLockedAsync(command.BranchId, command.InspectionId, ct); }
        catch (RejectInspectionSourceUnavailable error) { return new(RoomTypeBatchStatus.Unavailable,error.Message); }
        catch (RejectInspectionOwnershipException) { return new(RoomTypeBatchStatus.Forbidden); }
        if (loaded == null) return new(RoomTypeBatchStatus.NotFound);
        if (loaded.Domain.Inspection.InspectionId != command.InspectionId)
            return new(RoomTypeBatchStatus.InvalidReference, Errors: ["REQUEST_SOURCE_MISMATCH"]);
        var errors = RejectInspectionValidator.Validate(loaded.Domain, command.Input).Errors;
        if (errors.Contains("INSPECTION_REVISION_STALE")) return new(RoomTypeBatchStatus.Stale, Errors: errors);
        if(errors.Contains("INSPECTION_NOT_PENDING_CONCLUSION"))return new(RoomTypeBatchStatus.Inactive,Errors:errors);
        if (errors.Count != 0) return new(RoomTypeBatchStatus.InvalidReference, Errors: errors);
        if (loaded.Evidence.Any(e => e.AttachmentId <= 0 || e.EntityType != "INSPECTION"
            || e.EntityId != loaded.Domain.Inspection.InspectionId))
            return new(RoomTypeBatchStatus.InvalidReference, Errors: ["EVIDENCE_SOURCE_MISMATCH"]);
        if (await session.AuthorizeAsync(command.BranchId, ct) is { } revoked) return new(revoked);

        if(loaded.CurrentCleaningTaskId==null)return new(RoomTypeBatchStatus.Unavailable,"BLOCKED_SPECIFIC_RULE:D3_CURRENT_CYCLE_BINDING");
        if(loaded.CurrentCleaningTaskId!=loaded.Domain.CleaningTask.CleaningTaskId)return new(RoomTypeBatchStatus.Inactive,"CURRENT_CYCLE_MISMATCH");
        return new(RoomTypeBatchStatus.Success,Source:loaded);
    }
}
