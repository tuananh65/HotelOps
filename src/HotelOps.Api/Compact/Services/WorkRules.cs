using HotelOps.Api.Compact.Infrastructure;
namespace HotelOps.Api.Compact.Services;
public static class WorkRules
{
    public static readonly string[] Terminal=["CLOSED","COMPLETED","CANCELLED","PASSED","FAILED","REJECTED"];
    public static string Initial(string kind)=>kind switch {
        "CLEANING" or "OPERATIONAL"=>"PENDING_ASSIGNMENT","REWORK"=>"PENDING_ACCEPTANCE","INSPECTION"=>"ASSIGNED",
        "INCIDENT"=>"OPEN","MAINTENANCE"=>"PENDING","INTERNAL_REQUEST"=>"DRAFT",_=>throw new ApiException(400,"UNKNOWN_WORK_KIND")};
    public static string Permission(string kind,string action)=>kind switch {
        "CLEANING"=>action=="read"?"CLEANING_VIEW":action is "create" or "assign" or "cancel"?"CLEANING_ASSIGN":"CLEANING_EXECUTE",
        "REWORK"=>action=="read"?"CLEANING_VIEW":action is "create" or "assign" or "cancel"?"CLEANING_ASSIGN":"CLEANING_EXECUTE",
        "INSPECTION"=>action=="read"?"INSPECTION_VIEW":"INSPECTION_PERFORM",
        "INCIDENT"=>action=="read"?"INCIDENT_VIEW":action=="create"?"INCIDENT_CREATE":action=="triage"?"INCIDENT_TRIAGE":action=="escalate"?"INCIDENT_ESCALATION_HANDLE":"INCIDENT_MANAGE",
        "MAINTENANCE"=>action=="read"?"MAINTENANCE_VIEW":action is "verify" or "reopen"?"MAINTENANCE_REVIEW":action is "create" or "assign" or "cancel"?"MAINTENANCE_ASSIGN":"MAINTENANCE_EXECUTE",
        "OPERATIONAL"=>action=="read"?"TASK_VIEW":action is "create" or "assign" or "verify" or "cancel" or "reopen"?"TASK_MANAGE":"TASK_EXECUTE",
        "INTERNAL_REQUEST"=>action=="read"?"IR_VIEW":action is "create" or "submit" or "verify" or "reopen"?"IR_CREATE":action=="escalate"?"IR_OVERDUE_HANDLE":"IR_PROCESS",
        _=>throw new ApiException(400,"UNKNOWN_WORK_KIND")};
    public static string Transition(string kind,string status,string action)
    {
        var next=(kind,action,status) switch {
            ("INTERNAL_REQUEST","submit","DRAFT")=>"SUBMITTED",
            ("INTERNAL_REQUEST","route","SUBMITTED")=>"PENDING_ACCEPTANCE",
            ("INTERNAL_REQUEST","reject","PENDING_ACCEPTANCE")=>"REJECTED",
            ("INTERNAL_REQUEST","accept","PENDING_ACCEPTANCE")=>"ACCEPTED",
            ("INTERNAL_REQUEST","start","ACCEPTED")=>"IN_PROGRESS",
            ("INTERNAL_REQUEST","complete","IN_PROGRESS")=>"COMPLETED",
            ("INTERNAL_REQUEST","verify","COMPLETED")=>"CLOSED",
            ("INTERNAL_REQUEST","reopen","COMPLETED" or "CLOSED")=>"ACCEPTED",
            ("CLEANING","accept","ASSIGNED")=>"ASSIGNED",
            ("REWORK","accept","PENDING_ACCEPTANCE")=>"ACCEPTED",
            ("OPERATIONAL","accept","ASSIGNED")=>"ACCEPTED",
            ("INSPECTION","accept","ASSIGNED")=>"ASSIGNED",
            ("MAINTENANCE","accept","PENDING")=>"PENDING",
            ("INCIDENT","accept","OPEN")=>"OPEN",
            ("CLEANING","start","ASSIGNED")=>"IN_PROGRESS",
            ("REWORK" or "OPERATIONAL","start","ACCEPTED")=>"IN_PROGRESS",
            ("MAINTENANCE","start","PENDING")=>"IN_PROGRESS",
            ("INSPECTION","start","ASSIGNED")=>"INSPECTING",
            ("INCIDENT","start","OPEN")=>"IN_PROGRESS",
            ("CLEANING" or "REWORK" or "MAINTENANCE","pause","IN_PROGRESS")=>"ON_HOLD",
            ("OPERATIONAL","pause","IN_PROGRESS")=>"PAUSED",
            ("CLEANING" or "REWORK" or "MAINTENANCE","resume","ON_HOLD")=>"IN_PROGRESS",
            ("OPERATIONAL","resume","PAUSED")=>"IN_PROGRESS",
            ("CLEANING","complete","IN_PROGRESS")=>"PENDING_INSPECTION",
            ("REWORK","complete","IN_PROGRESS")=>"PENDING_REINSPECTION",
            ("INSPECTION","complete","INSPECTING")=>"PENDING_CONCLUSION",
            ("MAINTENANCE","complete","IN_PROGRESS")=>"COMPLETED",
            ("OPERATIONAL","complete","IN_PROGRESS")=>"PENDING_VERIFICATION",
            ("INCIDENT","complete","IN_PROGRESS")=>"RESOLVED",
            ("INSPECTION","pass","PENDING_CONCLUSION")=>"PASSED",
            ("INSPECTION","fail","PENDING_CONCLUSION")=>"FAILED",
            ("OPERATIONAL","verify","PENDING_VERIFICATION")=>"CLOSED",
            ("MAINTENANCE","verify","COMPLETED")=>"CLOSED",
            ("INCIDENT","verify","RESOLVED")=>"CLOSED",
            ("OPERATIONAL","reopen","PENDING_VERIFICATION" or "CLOSED")=>"ACCEPTED",
            ("MAINTENANCE","reopen","COMPLETED" or "CLOSED")=>"PENDING",
            ("INCIDENT","reopen","RESOLVED" or "CLOSED")=>"OPEN",
            (_,"cancel",_) when !Terminal.Contains(status)=>"CANCELLED",
            _=>throw new ApiException(409,"INVALID_WORK_TRANSITION")};
        return next;
    }
}
