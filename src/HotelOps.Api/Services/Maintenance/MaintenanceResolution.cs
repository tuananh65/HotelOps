using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Maintenance;

namespace HotelOps.Api.Services.Maintenance;

internal static class MaintenanceResolution
{
    // Existing resolution_note is nvarchar(max). Keep legacy notes readable; do not
    // infer either mandatory business field from an unstructured historical note.
    internal static string Note(SubmitMaintenanceResultRequest request)
    {
        EvidenceIds(request.ResultAttachmentIds);
        var diagnosis = request.Diagnosis?.Trim();
        var action = request.ActionTaken?.Trim();
        if (string.IsNullOrEmpty(diagnosis))
            throw new ValidationException("Diagnosis/root cause is required.");
        if (string.IsNullOrEmpty(action))
            throw new ValidationException("Action taken is required.");
        var note = request.ResolutionNote?.Trim();
        return $"Diagnosis/root cause:\n{diagnosis}\n\nAction taken:\n{action}"
            + (string.IsNullOrEmpty(note) ? "" : $"\n\nAdditional result notes:\n{note}");
    }

    internal static long[] EvidenceIds(IReadOnlyList<long>? ids)
    {
        if (ids == null) return [];
        if (ids.Any(id => id <= 0) || ids.Distinct().Count() != ids.Count)
            throw new ValidationException("Result attachments require distinct positive identities.");
        return ids.OrderBy(id => id).ToArray();
    }
}
