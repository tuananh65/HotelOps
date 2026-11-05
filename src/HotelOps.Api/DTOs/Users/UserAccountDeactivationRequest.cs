using System.Text.Json;

namespace HotelOps.Api.DTOs.Users;

public sealed record UserAccountDeactivationRequest(string Reason)
{
    public bool IsValid => Reason is not null && Reason == Reason.Trim() && Reason.Length >= 10;

    public static bool TryParse(JsonElement body, out UserAccountDeactivationRequest? request)
    {
        request = null;
        if (body.ValueKind != JsonValueKind.Object) return false;
        var fields = body.EnumerateObject().ToArray();
        if (fields.Length != 1 || fields[0].Name != "reason" || fields[0].Value.ValueKind != JsonValueKind.String)
            return false;
        var candidate = new UserAccountDeactivationRequest(fields[0].Value.GetString()!.Trim());
        if (!candidate.IsValid) return false;
        request = candidate;
        return true;
    }
}
