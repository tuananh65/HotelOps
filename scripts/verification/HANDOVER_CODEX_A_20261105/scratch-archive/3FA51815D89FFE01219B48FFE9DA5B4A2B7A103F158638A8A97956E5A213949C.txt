using System.Text.Json;

namespace HotelOps.Api.DTOs.Users;

public sealed record UserAccountActivationRequest(string Reason)
{
    public static bool TryParse(JsonElement body, out UserAccountActivationRequest? request)
    {
        request = null;
        if (body.ValueKind != JsonValueKind.Object) return false;
        var fields = body.EnumerateObject().ToArray();
        if (fields.Length != 1 || fields[0].Name != "reason" || fields[0].Value.ValueKind != JsonValueKind.String)
            return false;
        var reason = fields[0].Value.GetString()!.Trim();
        if (!new UserAccountStatusChange("ACTIVE", reason).IsValid) return false;
        request = new(reason);
        return true;
    }
}

public sealed record UserAccountActivationDto(long UserId, string AccountStatus);
