namespace HotelOps.Api.DTOs.Users;

public enum RevokeRoleStatus { Success, SessionInvalid, Forbidden, NotFound, Conflict, Stale, LastAdministrator, SerializationUnavailable, ReleaseAcceptanceRequired, ResponsibilitiesBlocked, ResponsibilitiesUnresolved }
public sealed record RevokeRoleResult(RevokeRoleStatus Status, byte[]? Version = null);
public static class RevokeRoleVersion
{
    public static bool TryParse(string? value, out byte[] version)
    {
        version = [];
        if (value is not { Length: 18 } || value[0] != '"' || value[^1] != '"' || !value[1..^1].All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F')) return false;
        version = Convert.FromHexString(value[1..^1]); return true;
    }
}
