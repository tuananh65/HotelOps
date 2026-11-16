using System.Text.Json;

namespace HotelOps.Api.DTOs.Users;

public sealed record AssignUserRoleRequest(string RoleCode, long? BranchId, bool ReactivateExisting = false)
{
    public static bool TryParse(JsonElement body, out AssignUserRoleRequest? request)
    {
        request = null;
        if (body.ValueKind != JsonValueKind.Object) return false;
        string? role = null; long? branch = null; var reactivate = false;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in body.EnumerateObject())
        {
            if (!seen.Add(field.Name)) return false;
            switch (field.Name)
            {
                case "roleCode" when field.Value.ValueKind == JsonValueKind.String:
                    role = field.Value.GetString()?.Trim(); break;
                case "branchId" when field.Value.ValueKind == JsonValueKind.Number
                    && field.Value.TryGetInt64(out var value) && value > 0:
                    branch = value; break;
                case "branchId" when field.Value.ValueKind == JsonValueKind.Null:
                    branch = null; break;
                case "reactivateExisting" when field.Value.ValueKind is JsonValueKind.True or JsonValueKind.False:
                    reactivate = field.Value.GetBoolean(); break;
                default: return false;
            }
        }
        // Global scope must be explicit; omitted branchId never means global.
        if (role is not { Length: > 0 and <= 60 } || !seen.Contains("branchId")) return false;
        request = new(role, branch, reactivate); return true;
    }
}

public sealed record UserRoleAssignmentDto(long Id, long UserId, string RoleCode, long? BranchId,
    long AssignedBy, DateTime AssignedAtUtc, bool IsActive, DateTime? ExpiresAtUtc);
