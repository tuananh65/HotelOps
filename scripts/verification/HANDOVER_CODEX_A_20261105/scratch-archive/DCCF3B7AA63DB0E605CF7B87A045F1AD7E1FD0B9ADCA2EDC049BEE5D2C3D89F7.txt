using Microsoft.AspNetCore.Authorization;

namespace HotelOps.Api.Security.Authorization;

public enum PermissionCheck { Resource, Branch, Global }

public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public string PermissionCode { get; }
    public PermissionCheck Check { get; }

    public PermissionRequirement(string permissionCode, PermissionCheck check = PermissionCheck.Resource)
    {
        if (!PermissionPolicy.IsValidCode(permissionCode) || !Enum.IsDefined(check))
            throw new ArgumentException("Invalid permission requirement.");
        PermissionCode = permissionCode;
        Check = check;
    }
}

/// <summary>Branch-level check only; not evidence of a particular resource's branch or ownership.</summary>
public sealed record PermissionBranchContext(long BranchId);

public static class PermissionPolicy
{
    public const string Prefix = "HotelOps.Permission:";

    public static string Name(string permissionCode, PermissionCheck check = PermissionCheck.Resource)
    {
        _ = new PermissionRequirement(permissionCode, check);
        return $"{Prefix}{check}:{permissionCode}";
    }

    internal static bool IsValidCode(string? code) => code is { Length: > 0 and <= 100 }
        && code[0] is >= 'A' and <= 'Z'
        && code.All(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_');
}
