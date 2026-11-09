using HotelOps.Api.Services.Notifications;

namespace HotelOps.Api.Services.Housekeeping;

// Lossless owner-result adapter. No operation, recipient, publication or revision is generated here.
public static class CleaningAssignmentNotificationReceipt
{
    public static CleaningAssignmentReceipt Adapt(CandidateAssignmentResult value) => new(
        value.Id, value.AssignmentId, value.BranchId, value.AssigneeUserId, value.OperationId,
        value.PublicationId, value.WorkflowState, value.Notification, value.SourceETag, value.Contract);
}
