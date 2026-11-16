namespace HotelOps.Api.DTOs.Notifications;

public sealed record CleaningCancellationReceiptDto(long TaskId, long BranchId, long AssignmentId,
    DateTime CancelledAtUtc, string State);
