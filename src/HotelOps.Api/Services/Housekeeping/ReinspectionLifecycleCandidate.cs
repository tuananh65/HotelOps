namespace HotelOps.Api.Services.Housekeeping;

// SRS lifecycle plan only. It is deliberately not a SQL status transition authority.
internal sealed record ReinspectionLifecycleCandidate(bool AllFixed,string InspectionState,string ReworkState,string CleaningState)
{
    internal bool SchemaAndLifecycleAccepted=>false;
    internal static ReinspectionLifecycleCandidate From(ReinspectionDecisionResult result)
    {
        if(!result.IsComplete||result.AllFixed is not {} fixedAll)throw new InvalidOperationException("REINSPECTION_DECISIONS_INCOMPLETE");
        return fixedAll?new(true,"PASSED","COMPLETED","COMPLETED"):new(false,"FAILED","FAILED","REWORK_REQUIRED");
    }
}
