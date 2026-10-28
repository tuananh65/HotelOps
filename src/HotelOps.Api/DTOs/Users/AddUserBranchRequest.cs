using System.Text.Json;

namespace HotelOps.Api.DTOs.Users;

public sealed record AddUserBranchRequest(long BranchId)
{
    public static bool TryParse(JsonElement body, out AddUserBranchRequest? request)
    {
        request = null;
        if (body.ValueKind != JsonValueKind.Object) return false;
        var fields = body.EnumerateObject().ToArray();
        if (fields.Length != 1 || fields[0].Name != "branchId" || fields[0].Value.ValueKind != JsonValueKind.Number
            || !fields[0].Value.TryGetInt64(out var branchId) || branchId <= 0) return false;
        request = new(branchId);
        return true;
    }
}

public sealed record UserBranchMembershipDto(long AccessId, long UserId, long BranchId, string AccessLevel, bool IsPrimary);
