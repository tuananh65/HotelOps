using System.Text.Json;

namespace HotelOps.Api.DTOs.Users;

public sealed record AddUserBranchRequest(long BranchId, long DepartmentId = 0)
{
    public static bool TryParse(JsonElement body, out AddUserBranchRequest? request)
    {
        request = null;
        if (body.ValueKind != JsonValueKind.Object) return false;
        var fields = body.EnumerateObject().ToArray();
        if (fields.Length != 2 || fields.Count(f => f.Name == "branchId") != 1 || fields.Count(f => f.Name == "departmentId") != 1) return false;
        var branch = body.GetProperty("branchId"); var department = body.GetProperty("departmentId");
        if (branch.ValueKind != JsonValueKind.Number || !branch.TryGetInt64(out var branchId) || branchId <= 0
            || department.ValueKind != JsonValueKind.Number || !department.TryGetInt64(out var departmentId) || departmentId <= 0) return false;
        request = new(branchId, departmentId);
        return true;
    }
}

public sealed record UserBranchMembershipDto(long AccessId, long UserId, long BranchId, string AccessLevel, bool IsPrimary)
{
    public long DepartmentId { get; init; }
}
