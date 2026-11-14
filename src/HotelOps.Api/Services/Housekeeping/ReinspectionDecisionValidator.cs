namespace HotelOps.Api.Services.Housekeeping;

// UC047 SRS section 5.9 uses per-defect Fixed/Not Fixed, not checklist PASS/FAIL.
// Internal domain input only. It supplies neither persisted provenance nor authorization.
internal sealed record ReinspectionDefectDecision(long DefectId, bool? Fixed);
internal sealed record ReinspectionDecisionResult(IReadOnlyList<string> Errors, bool? AllFixed)
{
    internal bool IsComplete => Errors.Count == 0;
    internal bool AuthoritativeCommitAccepted => false;
}
internal static class ReinspectionDecisionValidator
{
    internal static ReinspectionDecisionResult Validate(IReadOnlyList<long> persistedDefects,
        IReadOnlyList<ReinspectionDefectDecision> decisions, byte[] reworkRevision, string? expectedReworkTag,
        byte[] inspectionRevision, string? expectedInspectionTag)
    {
        var errors = new List<string>();
        static bool Matches(byte[] revision, string? tag) => revision.Length == 8 && tag == '"' + Convert.ToHexString(revision) + '"';
        if (!Matches(reworkRevision, expectedReworkTag)) errors.Add("REWORK_REVISION_STALE_OR_INVALID");
        if (!Matches(inspectionRevision, expectedInspectionTag)) errors.Add("INSPECTION_REVISION_STALE_OR_INVALID");
        if (persistedDefects.Count == 0 || persistedDefects.Any(id => id <= 0)
            || persistedDefects.Distinct().Count() != persistedDefects.Count) errors.Add("REWORK_DEFECT_SOURCE_INVALID");
        if (decisions.Any(d => d.DefectId <= 0 || !persistedDefects.Contains(d.DefectId))
            || decisions.Select(d => d.DefectId).Distinct().Count() != decisions.Count) errors.Add("REINSPECTION_DEFECT_MISMATCH");
        if (decisions.Any(d => d.Fixed == null) || persistedDefects.Any(id => !decisions.Any(d => d.DefectId == id)))
            errors.Add("EVERY_DEFECT_DECISION_REQUIRED");
        return new(errors.AsReadOnly(), errors.Count == 0 ? decisions.All(d => d.Fixed == true) : null);
    }
}
