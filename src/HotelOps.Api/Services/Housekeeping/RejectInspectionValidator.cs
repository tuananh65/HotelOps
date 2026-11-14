using HotelOps.Api.Entities;

namespace HotelOps.Api.Services.Housekeeping;

// Backend-only inputs: a future authorized reader must load these from the same source snapshot.
// Never bind this object from HTTP. Validation cannot establish provenance or authorization.
public sealed record RejectInspectionSource(
    RoomInspection Inspection,
    CleaningTask CleaningTask,
    InspectionChecklistVersion Version,
    IReadOnlyList<InspectionChecklistItem> Criteria,
    IReadOnlyList<InspectionResult> Results,
    IReadOnlyList<InspectionDefect> Defects,
    string? WorkflowState,
    byte[] Revision);

public sealed record RejectInspectionInput(string? Reason, bool? Confirmed, string? ExpectedETag);

// A valid result is only a domain validation result, never a persisted conclusion/authority receipt.
public sealed record RejectInspectionValidation(IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

public static class RejectInspectionValidator
{
    public static RejectInspectionValidation Validate(RejectInspectionSource source, RejectInspectionInput input)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(input);
        var errors = new List<string>();
        var inspection = source.Inspection;
        var reason = input.Reason?.Trim();
        // Same reason constraints as the existing Inspection WorkflowReason/ChecklistText helper.
        if (string.IsNullOrEmpty(reason) || reason.Length > 500 || reason.Any(char.IsControl))
            errors.Add("REJECT_REASON_INVALID");
        if (input.Confirmed != true) errors.Add("REJECT_CONFIRMATION_REQUIRED");
        errors.AddRange(InspectionConclusionValidation.Validate(source, input.ExpectedETag));
        var criteria = source.Criteria.Where(c => c.IsActive).ToArray();

        bool ValidDefect(InspectionDefect defect) => defect.DefectId > 0
            && defect.InspectionId == inspection.InspectionId && !string.IsNullOrWhiteSpace(defect.Description)
            && criteria.Any(c => c.ItemId == defect.ChecklistItemId)
            && source.Results.Any(r => r.InspectionId == inspection.InspectionId
                && r.ChecklistItemId == defect.ChecklistItemId && r.Outcome == "FAIL");

        if (source.Defects.Any(d => !ValidDefect(d))
            || source.Defects.Select(d => d.DefectId).Distinct().Count() != source.Defects.Count)
            errors.Add("DEFECT_SOURCE_INVALID");
        if (!source.Defects.Any(ValidDefect)) errors.Add("REJECT_DEFECT_REQUIRED");
        if (source.Results.Any(r => r.Outcome == "FAIL"
            && !source.Defects.Any(d => d.ChecklistItemId == r.ChecklistItemId && ValidDefect(d))))
            errors.Add("FAILED_CRITERION_DEFECT_REQUIRED");

        return new(errors.AsReadOnly());
    }

}
