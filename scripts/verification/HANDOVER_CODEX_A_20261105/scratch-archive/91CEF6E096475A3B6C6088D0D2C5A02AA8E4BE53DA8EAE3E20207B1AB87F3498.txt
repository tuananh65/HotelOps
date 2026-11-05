using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace HotelOps.Api.DTOs.Users;

public sealed record UserProfileDto(long Id, string FullName, string Email, string? Phone);

public sealed record UserProfilePatch(bool HasFullName, string? FullName, bool HasEmail, string? Email,
    bool HasPhone, string? Phone)
{
    public UserProfilePatch Normalize()
    {
        if (!HasFullName && !HasEmail && !HasPhone) throw new ValidationException("At least one profile field is required.");
        var name = FullName?.Trim();
        var email = Email?.Trim();
        var phone = Phone?.Trim();
        if (HasFullName && (string.IsNullOrEmpty(name) || name.Length > 200)
            || HasEmail && (string.IsNullOrEmpty(email) || email.Length > 150 || !new EmailAddressAttribute().IsValid(email))
            || HasPhone && phone is { Length: > 20 })
            throw new ValidationException("Invalid profile field.");
        return this with { FullName = name, Email = email, Phone = string.IsNullOrEmpty(phone) ? null : phone };
    }

    public static UserProfilePatch Parse(JsonElement body)
    {
        if (body.ValueKind != JsonValueKind.Object) throw new ValidationException("An object is required.");
        var fields = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var field in body.EnumerateObject())
            if (field.Name is not ("fullName" or "email" or "phone") || !fields.TryAdd(field.Name, field.Value))
                throw new ValidationException("Unknown or duplicate profile field.");
        if (fields.Count == 0) throw new ValidationException("At least one profile field is required.");
        string? Read(string name, int max, bool nullable)
        {
            if (!fields.TryGetValue(name, out var value)) return null;
            if (nullable && value.ValueKind == JsonValueKind.Null) return null;
            if (value.ValueKind != JsonValueKind.String) throw new ValidationException("Invalid profile field type.");
            var text = value.GetString()!.Trim();
            if (text.Length > max || (!nullable && text.Length == 0)) throw new ValidationException("Invalid profile field length.");
            return nullable && text.Length == 0 ? null : text;
        }
        var name = Read("fullName", 200, false);
        var email = Read("email", 150, false);
        if (email is not null && !new EmailAddressAttribute().IsValid(email)) throw new ValidationException("Invalid email format.");
        return new(fields.ContainsKey("fullName"), name, fields.ContainsKey("email"), email,
            fields.ContainsKey("phone"), Read("phone", 20, true));
    }
}
