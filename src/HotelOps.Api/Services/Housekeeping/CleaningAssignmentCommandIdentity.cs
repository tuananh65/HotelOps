using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HotelOps.Api.DTOs.Housekeeping;
namespace HotelOps.Api.Services.Housekeeping;

// Feature command identity only; NOT an A01 publisher interface or implementation.
public static class CleaningAssignmentCommandIdentity
{
    public static string Fingerprint(long actorId, long branchId, long taskId, string originalIfMatch, AssignCleaningRequest request)
    {
        if (actorId <= 0 || branchId <= 0 || taskId <= 0 || request.AssigneeId <= 0 || request.DueAt.Kind != DateTimeKind.Utc || request.AdoptUnassignedLegacy)
            throw new ArgumentException("Invalid assignment command identity.");
        var canonical = JsonSerializer.Serialize(new
        {
            version = 1,
            action = "UC029_ASSIGN",
            actorId,
            branchId,
            taskId,
            assigneeId = request.AssigneeId,
            dueAt = request.DueAt.ToString("O", CultureInfo.InvariantCulture),
            reason = request.Reason.Trim(),
            adoptUnassignedLegacy = false,
            originalIfMatch
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
