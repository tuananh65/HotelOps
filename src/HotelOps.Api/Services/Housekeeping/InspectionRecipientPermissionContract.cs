using HotelOps.Api.Security.Authorization;
namespace HotelOps.Api.Services.HotelStructure;

// Proposed narrow notification-source permission binding, NOT a catalog permission or grant.
internal sealed record InspectionRecipientPermissionContract(string Id,string ExistingPermission,string Resource,
    IReadOnlyList<string> SummaryFields)
{
    internal string? AcceptedConcreteBinding=>null;
    internal bool PrivateEvidenceAllowed=>false;
    internal void RequireBusinessAcceptance()
    {
        if(AcceptedConcreteBinding==null)throw new HotelOps.Api.Services.Notifications.Fe05NotificationUnavailableException("FE05_RECIPIENT_SOURCE_READ_BINDING_UNACCEPTED:"+Id);
    }
    internal static InspectionRecipientPermissionContract For(InspectionRecipientRelationship role)=>role switch {
        InspectionRecipientRelationship.ResponsibleSupervisor=>new("FE05.RESPONSIBLE_SUPERVISOR.SUMMARY.V1.CANDIDATE","INSPECTION_PERFORM","Inspection/current-cycle",
            Fields("inspectionId","branchId","roomId","cleaningTaskId","cycleRevision","inspectionRevision","workflowState")),
        InspectionRecipientRelationship.OriginalCleaner=>new("FE05.ORIGINAL_CLEANER.SUMMARY.V1.CANDIDATE","CLEANING_VIEW","Own cleaning-task/current-cycle FAILED summary",
            Fields("inspectionId","branchId","roomId","cleaningTaskId","assignmentId","completionProgressId","completionRevision","workflowState")),
        InspectionRecipientRelationship.ReworkAssignee=>new("FE05.REWORK_ASSIGNEE.SUMMARY.V1.CANDIDATE","CLEANING_VIEW","Own assigned Rework/current-cycle",
            Fields("reworkId","inspectionId","branchId","roomId","cleaningTaskId","reworkRevision","workflowState")),
        _=>throw new ArgumentOutOfRangeException(nameof(role))};
    private static IReadOnlyList<string> Fields(params string[] fields)=>Array.AsReadOnly(fields);
    // Caller independently proves exact resource relation and branch access first. An OWN
    // grant can never be evaluated by this function alone as an authorization capability.
    internal bool MatchesExistingGrant(PermissionGrantPath path,long branch,DateTime now)
        =>branch>0&&path.PermissionCode==ExistingPermission&&PermissionGrantRules.IsEligible(path,now)
          &&(PermissionGrantRules.EffectiveScope(path)=="GLOBAL"||path.BranchId==branch
            &&PermissionGrantRules.EffectiveScope(path) is "BRANCH" or "OWN");
}
