namespace HotelOps.Api.DTOs.Housekeeping;
public sealed record InspectionAssignmentExpectation(string TaskRevision,string CycleRevision,long ChecklistVersionId);
public sealed record InspectionAssignmentCandidateRequest(long InspectorId,long ResponsibleSupervisorId,DateTime DueAt,string Reason,
    string CycleRevision,long ChecklistVersionId,bool Confirmed);
public sealed record InspectionAssignmentPerson(long UserId,string FullName);
public sealed record InspectionAssignmentOptions(long TaskId,long RoomId,string TaskRevision,string CycleRevision,long ChecklistVersionId,
    IReadOnlyList<InspectionAssignmentPerson> Inspectors,IReadOnlyList<InspectionAssignmentPerson> ResponsibleSupervisors,bool ProductionMutationAvailable);
public sealed record InspectionAssignmentComponentResult(long InspectionId,long TaskId,long RoomId,long InspectorId,long ResponsibleSupervisorId,
    long ChecklistVersionId,string InspectionRevision,string TaskRevision,string CycleRevision,bool NotificationAccepted,bool AuthoritativeCommitAccepted);
public sealed record InspectionAssignedItem(long InspectionId,long RoomId,long ChecklistVersionId,string InspectionRevision);

public sealed record InspectionAssignmentTask(long TaskId,long RoomId);
public sealed record InspectionAssignmentQueue(IReadOnlyList<InspectionAssignmentTask> Pending,IReadOnlyList<InspectionAssignedItem> Assigned);
