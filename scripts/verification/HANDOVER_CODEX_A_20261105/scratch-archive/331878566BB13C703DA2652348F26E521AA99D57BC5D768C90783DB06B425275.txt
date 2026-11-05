using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace HotelOps.Api.DTOs.Users;

public sealed record CreateEmployeeAccountRequest(string EmployeeCode, string Username, string FullName,
    string Email, string? Phone, long BranchId, long DepartmentId, string RoleCode)
{
    public CreateEmployeeAccountRequest Normalize()
    {
        var value = this with { EmployeeCode = EmployeeCode?.Trim()!, Username = Username?.Trim()!,
            FullName = FullName?.Trim()!, Email = Email?.Trim()!, Phone = Phone?.Trim(), RoleCode = RoleCode?.Trim()! };
        if (string.IsNullOrEmpty(value.EmployeeCode) || value.EmployeeCode.Length > 40
            || string.IsNullOrEmpty(value.Username) || value.Username.Length > 100
            || string.IsNullOrEmpty(value.FullName) || value.FullName.Length > 200
            || string.IsNullOrEmpty(value.Email) || value.Email.Length > 150 || !new EmailAddressAttribute().IsValid(value.Email)
            || value.Phone is { Length: > 20 } || value.BranchId <= 0 || value.DepartmentId <= 0
            || string.IsNullOrEmpty(value.RoleCode) || value.RoleCode.Length > 60)
            throw new ValidationException("Invalid employee account field.");
        return value with { Phone = string.IsNullOrEmpty(value.Phone) ? null : value.Phone };
    }

    public static CreateEmployeeAccountRequest Parse(JsonElement body)
    {
        if (body.ValueKind != JsonValueKind.Object) throw new ValidationException("An object is required.");
        var fields = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var field in body.EnumerateObject())
            if (field.Name is not ("employeeCode" or "username" or "fullName" or "email" or "phone" or "branchId" or "departmentId" or "roleCode")
                || !fields.TryAdd(field.Name, field.Value)) throw new ValidationException("Unknown or duplicate account field.");
        string Text(string name)
            => fields.TryGetValue(name, out var field) && field.ValueKind == JsonValueKind.String
                ? field.GetString()! : throw new ValidationException("Required text field is missing or invalid.");
        long Id(string name)
            => fields.TryGetValue(name, out var field) && field.ValueKind == JsonValueKind.Number && field.TryGetInt64(out var id) && id > 0
                ? id : throw new ValidationException("A positive Int64 reference is required.");
        string? phone = null;
        if (fields.TryGetValue("phone", out var item) && item.ValueKind != JsonValueKind.Null)
            phone = item.ValueKind == JsonValueKind.String ? item.GetString() : throw new ValidationException("Invalid phone type.");
        return new CreateEmployeeAccountRequest(Text("employeeCode"), Text("username"), Text("fullName"), Text("email"),
            phone, Id("branchId"), Id("departmentId"), Text("roleCode")).Normalize();
    }
}

public sealed record CreatedEmployeeAccountDto(long Id, string EmployeeCode, string Username, string FullName,
    string Email, string? Phone, long BranchId, long DepartmentId, string RoleCode, string AccountStatus);

// Dedicated success-only handoff; never use in ordinary list/detail/profile responses or telemetry.
public sealed record InitialCredentialHandoffDto(string Password, DateTime ExpiresAtUtc)
{
    public override string ToString() => "[Initial credential handoff: redacted]";
}
public sealed record CreateEmployeeAccountResponse(CreatedEmployeeAccountDto User, InitialCredentialHandoffDto TemporaryCredential);
