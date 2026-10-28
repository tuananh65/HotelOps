using System.Text.Json;

namespace HotelOps.Api.DTOs.Users;

/// <summary>UC-006 input only; never bind an EF User.</summary>
public sealed record UserAccountStatusChange(string Status, string Reason)
{
    public static bool TryParse(JsonElement body, out UserAccountStatusChange? change)
    {
        change = null;
        if (body.ValueKind != JsonValueKind.Object) return false;
        var fields = body.EnumerateObject().ToArray();
        if (fields.Length != 2 || fields.Count(p => p.Name == "status") != 1
            || fields.Count(p => p.Name == "reason") != 1) return false;
        var status = body.GetProperty("status");
        var reason = body.GetProperty("reason");
        if (status.ValueKind != JsonValueKind.String || reason.ValueKind != JsonValueKind.String) return false;
        var text = reason.GetString()!.Trim();
        if (status.GetString() is not ("ACTIVE" or "INACTIVE") || text.Length < 10) return false;
        change = new(status.GetString()!, text);
        return true;
    }

    public bool IsValid => Status is "ACTIVE" or "INACTIVE" && Reason is not null
        && Reason == Reason.Trim() && Reason.Length >= 10;

    public bool IsTransitionFrom(string current) => IsValid && current is "ACTIVE" or "INACTIVE"
        && !string.Equals(current, Status, StringComparison.Ordinal);
}
