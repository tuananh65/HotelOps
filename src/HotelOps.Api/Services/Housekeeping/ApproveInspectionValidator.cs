namespace HotelOps.Api.Services.Housekeeping;

// UC044 GBR-062: all mandatory criteria must PASS. This is not permission,
// evidence provenance, lifecycle completion or a Ready room decision.
public static class ApproveInspectionValidator
{
    public static RejectInspectionValidation Validate(RejectInspectionSource source, string? expectedETag)
    {
        ArgumentNullException.ThrowIfNull(source);
        var errors = InspectionConclusionValidation.Validate(source, expectedETag);
        if (source.Criteria.Any(c => c.IsActive && c.IsRequired && !source.Results.Any(r =>
            r.InspectionId == source.Inspection.InspectionId && r.ChecklistItemId == c.ItemId && r.Outcome == "PASS")))
            errors.Add("MANDATORY_PASS_REQUIRED");
        // GBR-060 still requires a described defect for any recorded FAIL,
        // including an optional criterion. Optional FAIL alone is not GBR-062 failure.
        var active = source.Criteria.Where(c => c.IsActive).ToArray();
        bool ValidDefect(Entities.InspectionDefect d) => d.DefectId > 0
            && d.InspectionId == source.Inspection.InspectionId && !string.IsNullOrWhiteSpace(d.Description)
            && active.Any(c => c.ItemId == d.ChecklistItemId)
            && source.Results.Any(r => r.ChecklistItemId == d.ChecklistItemId && r.Outcome == "FAIL");
        if (source.Defects.Any(d => !ValidDefect(d)) || source.Defects.Select(d => d.DefectId).Distinct().Count() != source.Defects.Count)
            errors.Add("DEFECT_SOURCE_INVALID");
        if (source.Results.Any(r => r.Outcome == "FAIL" && !source.Defects.Any(d => d.ChecklistItemId == r.ChecklistItemId && ValidDefect(d))))
            errors.Add("FAILED_CRITERION_DEFECT_REQUIRED");
        return new(errors.AsReadOnly());
    }
}
