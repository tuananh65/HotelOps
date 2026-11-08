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
        var cycle = source.CleaningTask;
        var reason = input.Reason?.Trim();
        // Same reason constraints as the existing Inspection WorkflowReason/ChecklistText helper.
        if (string.IsNullOrEmpty(reason) || reason.Length > 500 || reason.Any(char.IsControl))
            errors.Add("REJECT_REASON_INVALID");
        if (input.Confirmed != true) errors.Add("REJECT_CONFIRMATION_REQUIRED");
        if (source.WorkflowState != "PENDING_CONCLUSION") errors.Add("INSPECTION_NOT_PENDING_CONCLUSION");
        if (inspection.Status != "IN_PROGRESS" || inspection.Result != null)
            errors.Add("INSPECTION_CONCLUSION_STATE_INCONSISTENT");

        if (inspection.InspectionId <= 0 || inspection.RoomId <= 0 || cycle.CleaningTaskId <= 0
            || inspection.CleaningTaskId != cycle.CleaningTaskId || inspection.RoomId != cycle.RoomId
            || source.Version.VersionId <= 0 || inspection.VersionId != source.Version.VersionId)
            errors.Add("INSPECTION_SOURCE_MISMATCH");

        if (source.Revision is not { Length: 8 }) errors.Add("INSPECTION_REVISION_INVALID");
        if (!StrongETag(input.ExpectedETag)) errors.Add("EXPECTED_REVISION_INVALID");
        else if (source.Revision is { Length: 8 } && input.ExpectedETag != '"' + Convert.ToHexString(source.Revision) + '"')
            errors.Add("INSPECTION_REVISION_STALE");

        if (source.Criteria.Count == 0 || source.Criteria.Any(c => c.ItemId <= 0 || c.VersionId != inspection.VersionId)
            || source.Criteria.Select(c => c.ItemId).Distinct().Count() != source.Criteria.Count)
            errors.Add("CHECKLIST_SOURCE_INVALID");

        var criteria = source.Criteria.Where(c => c.IsActive).ToArray();
        if (criteria.Length == 0) errors.Add("ACTIVE_CRITERIA_REQUIRED");
        if (source.Results.Any(r => r.InspectionId != inspection.InspectionId
                || !criteria.Any(c => c.ItemId == r.ChecklistItemId) || r.Outcome is not ("PASS" or "FAIL"))
            || source.Results.Select(r => r.ChecklistItemId).Distinct().Count() != source.Results.Count)
            errors.Add("CRITERION_RESULTS_INVALID");

        if (criteria.Any(c => c.IsRequired && !source.Results.Any(r => r.InspectionId == inspection.InspectionId
            && r.ChecklistItemId == c.ItemId && r.Outcome is "PASS" or "FAIL")))
            errors.Add("MANDATORY_RESULT_REQUIRED");

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

    private static bool StrongETag(string? value) => value is { Length: 18 } && value[0] == '"' && value[^1] == '"'
        && value.Skip(1).Take(16).All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');
}
