using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Entities;

namespace HotelOps.Api.Services.Authentication;

/// <summary>Matches the existing, trusted audit_logs action/ISJSON constraints; no credential values.</summary>
internal static class AuthenticationAudit
{
    internal static AuditLog ForUser(long userId, string action, DateTime utcNow, object? newValues = null,
        params string[] changedFields)
    {
        if (action is not ("LOGIN" or "LOGOUT" or "UPDATE"))
            throw new ArgumentException("Unsupported authentication audit action.", nameof(action));
        if (utcNow.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC time is required.", nameof(utcNow));
        return new AuditLog
        {
            EntityType = "users", EntityId = userId.ToString(CultureInfo.InvariantCulture),
            Action = action, OccurredAt = utcNow,
            NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues),
            ChangedFields = changedFields.Length == 0 ? null : JsonSerializer.Serialize(changedFields)
        };
    }
}
