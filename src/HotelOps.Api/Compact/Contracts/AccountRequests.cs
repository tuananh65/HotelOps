using System.ComponentModel.DataAnnotations;
using System.Text;

namespace HotelOps.Api.Compact.Contracts;

// Separate contracts keep the legacy shared DTOs unchanged during integration.
public sealed record CreateAccountRequest(
    [Required,StringLength(100)] string Username,
    [Required,StringLength(200)] string FullName,
    [Required,StringLength(128,MinimumLength=8)] string Password,
    [Range(1,long.MaxValue)] long BranchId,
    [EmailAddress,StringLength(254)] string? Email=null,
    [Phone,StringLength(30)] string? Phone=null,
    [Range(1,long.MaxValue)] long? DepartmentId=null);

public sealed record ChangeAccountPasswordRequest(
    [Required,StringLength(1024)] string CurrentPassword,
    [Required,StringLength(128,MinimumLength=8)] string NewPassword);

// Identity and privileges are deliberately absent: the authenticated actor owns this update.
public sealed record UpdatePersonalProfileRequest(
    [Required] string RowVersion,
    [Required,StringLength(200,MinimumLength=1)] string FullName,
    [EmailAddress,StringLength(254)] string? Email,
    [Phone,StringLength(30)] string? Phone);

public static class AccountPasswordPolicy
{
    public static bool IsValid(string? password)=>password is {Length:>=8 and <=128}
        && password.Any(c=>c is >= 'A' and <= 'Z')
        && password.Any(c=>c is >= 'a' and <= 'z')
        && password.Any(c=>c is >= '0' and <= '9')
        && password.EnumerateRunes().Any(c=>Rune.IsPunctuation(c)||Rune.IsSymbol(c));
}
