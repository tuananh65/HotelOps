using HotelOps.Api.DTOs.Authentication;

namespace HotelOps.Api.Services.Authentication;

public static class PasswordChangePolicy
{
    public const int MinimumLength = 8;
    public const int MaximumLength = 256;

    public static bool HasValidInput(ChangePasswordRequest request)
        => !string.IsNullOrWhiteSpace(request.CurrentPassword) && request.CurrentPassword.Length <= MaximumLength
            && !string.IsNullOrWhiteSpace(request.NewPassword) && request.NewPassword.Length <= MaximumLength
            && !string.IsNullOrWhiteSpace(request.ConfirmNewPassword) && request.ConfirmNewPassword.Length <= MaximumLength
            && string.Equals(request.NewPassword, request.ConfirmNewPassword, StringComparison.Ordinal);

    // UTF-16 length matches DTO StringLength and the existing Login input limit.
    // Spaces are preserved but do not count as the required special character.
    public static bool IsStrongEnough(string password)
        => password.Length is >= MinimumLength and <= MaximumLength
            && !password.Any(char.IsControl) && password.Any(char.IsUpper) && password.Any(char.IsLower)
            && password.Any(char.IsDigit)
            && password.Any(c => char.IsPunctuation(c) || char.IsSymbol(c));
}
