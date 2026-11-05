using System.ComponentModel.DataAnnotations;

namespace HotelOps.Api.DTOs.Authentication;

public sealed class LoginRequest
{
    [Required, StringLength(150)]
    public string Username { get; init; } = "";
    [Required, StringLength(256)]
    public string Password { get; init; } = "";
    public override string ToString() => "[Login request: redacted]";
}

public sealed record AuthUserDto(string Id, string Username, string Name);

public sealed class ChangePasswordRequest
{
    [Required, StringLength(256)]
    public string CurrentPassword { get; init; } = "";
    [Required, StringLength(256)]
    public string NewPassword { get; init; } = "";
    [Required, StringLength(256)]
    public string ConfirmNewPassword { get; init; } = "";
    public override string ToString() => "[Change password request: redacted]";
}

public sealed class SessionDto(AuthUserDto user, DateTime expiresAt, DateTime absoluteExpiresAt,
    bool mustChangePassword, string csrfToken)
{
    public AuthUserDto User { get; } = user;
    public DateTime ExpiresAt { get; } = expiresAt;
    public DateTime AbsoluteExpiresAt { get; } = absoluteExpiresAt;
    public bool MustChangePassword { get; } = mustChangePassword;
    public string CsrfToken { get; } = csrfToken;
    public override string ToString() => "[Authentication response: token redacted]";
}
