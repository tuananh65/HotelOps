using HotelOps.Api.Services.Housekeeping;

internal static class ReinspectionDecisionChecks
{
    internal static int Run()
    {
        var passed = 0;
        byte[] revision = [0, 0, 0, 0, 0, 0, 0, 1];
        const string tag = "\"0000000000000001\"";
        ReinspectionDecisionResult Validate(long[] source, ReinspectionDefectDecision[] decisions,
            string? reworkTag = tag, string? inspectionTag = tag) =>
            ReinspectionDecisionValidator.Validate(source, decisions, revision, reworkTag, revision, inspectionTag);
        void Check(string name, bool condition)
        {
            if (!condition) throw new Exception("FAIL Reinspection: " + name);
            passed++;
            Console.WriteLine("PASS Reinspection: " + name);
        }
        void Denied(string name, ReinspectionDecisionResult result, string error)
        {
            Check(name, !result.IsComplete && result.AllFixed == null && result.Errors.Contains(error));
            try
            {
                ReinspectionLifecycleCandidate.From(result);
                throw new Exception("Incomplete decisions produced a lifecycle plan");
            }
            catch (InvalidOperationException e) when (e.Message == "REINSPECTION_DECISIONS_INCOMPLETE") { }
        }

        var fixedAll = Validate([11, 12], [new(11, true), new(12, true)]);
        var success = ReinspectionLifecycleCandidate.From(fixedAll);
        Check("all defects fixed maps to completed cycle", fixedAll.IsComplete && success.AllFixed
            && success.InspectionState == "PASSED" && success.ReworkState == "COMPLETED" && success.CleaningState == "COMPLETED");
        var incomplete = Validate([11, 12], [new(11, true), new(12, false)]);
        var retry = ReinspectionLifecycleCandidate.From(incomplete);
        Check("not fixed retains rework required", incomplete.IsComplete && !retry.AllFixed
            && retry.InspectionState == "FAILED" && retry.ReworkState == "FAILED" && retry.CleaningState == "REWORK_REQUIRED");
        Check("domain result never grants write authority", !fixedAll.AuthoritativeCommitAccepted && !success.SchemaAndLifecycleAccepted);
        Check("decision order does not change meaning", Validate([11, 12], [new(12, true), new(11, true)]).AllFixed == true);
        Denied("missing defect", Validate([11, 12], [new(11, true)]), "EVERY_DEFECT_DECISION_REQUIRED");
        Denied("unanswered defect", Validate([11], [new(11, null)]), "EVERY_DEFECT_DECISION_REQUIRED");
        Denied("foreign defect", Validate([11], [new(11, true), new(99, true)]), "REINSPECTION_DEFECT_MISMATCH");
        Denied("duplicate decision", Validate([11], [new(11, true), new(11, false)]), "REINSPECTION_DEFECT_MISMATCH");
        Denied("duplicate persisted defect", Validate([11, 11], [new(11, true)]), "REWORK_DEFECT_SOURCE_INVALID");
        Denied("empty persisted source", Validate([], []), "REWORK_DEFECT_SOURCE_INVALID");
        Denied("invalid persisted identity", Validate([0], [new(0, true)]), "REWORK_DEFECT_SOURCE_INVALID");
        foreach (var invalid in new string?[] { null, "", "*", "W/" + tag, "\"0000000000000002\"", tag + "," + tag })
        {
            Denied("invalid/stale rework tag " + invalid, Validate([11], [new(11, true)], invalid), "REWORK_REVISION_STALE_OR_INVALID");
            Denied("invalid/stale inspection tag " + invalid, Validate([11], [new(11, true)], inspectionTag: invalid), "INSPECTION_REVISION_STALE_OR_INVALID");
        }
        Denied("invalid stored revision", ReinspectionDecisionValidator.Validate([11], [new(11, true)], [], tag, revision, tag), "REWORK_REVISION_STALE_OR_INVALID");
        var before = Convert.ToHexString(revision);
        Validate([11], [new(11, true)]);
        Check("validation does not mutate revisions", before == Convert.ToHexString(revision));
        return passed;
    }
}
