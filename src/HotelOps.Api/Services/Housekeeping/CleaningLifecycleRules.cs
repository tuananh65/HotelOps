using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.DTOs.Housekeeping;
namespace HotelOps.Api.Services.Housekeeping;

public static class CleaningLifecycleRules
{
    public static bool StrongTag(string? value) => value is { Length: 18 } && value[0] == '"' && value[^1] == '"'
        && value[1..^1].All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');
    public static string Reason(string? value, int minimum = 10)
    {
        var text = value?.Trim();
        if (text == null || text.Length < minimum || text.Length > 500 || text.Any(char.IsControl)) throw new ValidationException("A reason of 10–500 characters without control characters is required.");
        return text;
    }
    public static Guid Operation(string key) => Guid.TryParseExact(key, "D", out var id) && id != Guid.Empty ? id : throw new ValidationException("A nonempty GUID operation key is required.");
    public static string? StartConflict(string? state, long actor, long owner, bool resolveBlocker)
        => actor != owner ? "CURRENT_ASSIGNEE_REQUIRED" : state == "ON_HOLD" ? "BLOCKER_RESOLUTION_CONTRACT_REQUIRED"
        : state != "ASSIGNED" ? "TASK_NOT_ASSIGNED" : resolveBlocker ? "NO_OPEN_BLOCKER_TO_RESOLVE" : null;
    public static string? ReassignConflict(string? state, long previous, long next)
        => previous == next ? "SAME_ASSIGNEE" : state is not ("ASSIGNED" or "IN_PROGRESS" or "ON_HOLD") ? "REASSIGN_STATE_INVALID" : null;
    public static byte[] Fingerprint(long actor, long branch, long task, string originalTag, ReassignCleaningRequest request)
        => SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { version = 1, action = "REASSIGN", actor, branch, task, originalTag,
            request.AssigneeId, due = request.DueAt.ToUniversalTime().ToString("O"), reason = request.Reason.Trim() }));
}
