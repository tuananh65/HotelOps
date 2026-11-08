using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.DTOs.Housekeeping;

namespace HotelOps.Api.Services.Housekeeping;

public static class CleaningCancellationRules
{
    public static string Validate(long branch, long task, string tag, string key, CancelCleaningRequest input)
    {
        if (branch <= 0 || task <= 0 || !CleaningLifecycleRules.StrongTag(tag) || !input.Confirmed)
            throw new ValidationException("Positive branch/task, confirmation and a strong task ETag are required.");
        CleaningLifecycleRules.Operation(key);
        return CleaningLifecycleRules.Reason(input.Reason, 1);
    }

    public static bool CanCancel(string? state, string status, DateTime? started, DateTime? ended, int assignments)
        => status == "PENDING" && started == null && ended == null
            && (state == "PENDING_ASSIGNMENT" && assignments == 0 || state == "ASSIGNED" && assignments == 1);

    public static byte[] Fingerprint(long actor, long branch, long task, string tag, string reason)
        => SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { version = 1, action = "CANCEL_CLEANING",
            actor, branch, task, tag, confirmed = true, reason = reason.Trim() }));

    public static bool ValidReceipt(CleaningCancellationResult r, long branch, long task, Guid operation)
        => r.Id == task && r.BranchId == branch && r.RoomId > 0 && r.OperationId == operation
            && r.ReadinessEventId > 0 && r.OccurredAtUtc.Kind == DateTimeKind.Utc
            && CleaningLifecycleRules.StrongTag(r.SourceETag) && r.State == "CANCELLED"
            && r.Readiness == "PENDING_CLEANING" && r.Contract == "PROVISIONAL_NOT_ACCEPTED"
            && (r.AssignmentId == null && r.AssigneeId == null && r.PublicationId == null
                || r.AssignmentId > 0 && r.AssigneeId > 0 && r.PublicationId != null && r.PublicationId != Guid.Empty);
}
