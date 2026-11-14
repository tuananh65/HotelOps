namespace HotelOps.Api.Services.Housekeeping;

// Snapshot checks only; does not establish authorization or transaction authority.
internal static class InspectionConclusionValidation
{
    internal static List<string> Validate(RejectInspectionSource source, string? expectedETag)
    {
        var errors = new List<string>();
        var inspection = source.Inspection;
        var cycle = source.CleaningTask;
        if (source.WorkflowState != "PENDING_CONCLUSION") errors.Add("INSPECTION_NOT_PENDING_CONCLUSION");
        if (inspection.Status != "IN_PROGRESS" || inspection.Result != null)
            errors.Add("INSPECTION_CONCLUSION_STATE_INCONSISTENT");

        if (inspection.InspectionId <= 0 || inspection.RoomId <= 0 || cycle.CleaningTaskId <= 0
            || inspection.CleaningTaskId != cycle.CleaningTaskId || inspection.RoomId != cycle.RoomId
            || source.Version.VersionId <= 0 || inspection.VersionId != source.Version.VersionId)
            errors.Add("INSPECTION_SOURCE_MISMATCH");

        if (source.Revision is not { Length: 8 }) errors.Add("INSPECTION_REVISION_INVALID");
        if (!StrongETag(expectedETag)) errors.Add("EXPECTED_REVISION_INVALID");
        else if (source.Revision is { Length: 8 } && expectedETag != '"' + Convert.ToHexString(source.Revision) + '"')
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

        return errors;
    }
    private static bool StrongETag(string? value) => value is { Length: 18 } && value[0] == '"' && value[^1] == '"'
        && value.Skip(1).Take(16).All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');
}
