using System.ComponentModel.DataAnnotations;

namespace HotelOps.Api.Compact.Contracts;

public sealed record UpdateAccountRequest([Required] string RowVersion,
    [Required,StringLength(200,MinimumLength=1)] string FullName,
    [EmailAddress,StringLength(254)] string? Email, [Phone,StringLength(30)] string? Phone);
public sealed record ResetAccountPasswordRequest([Required] string RowVersion,
    [Required,StringLength(1024)] string AdminPassword,
    [Required,StringLength(2000,MinimumLength=10)] string Reason,
    [EmailAddress,StringLength(254)] string? GmailAddress=null,
    [StringLength(64)] string? GmailAppPassword=null);
