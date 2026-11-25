using System.ComponentModel.DataAnnotations;
namespace HotelOps.Api.Compact.Contracts;
public sealed record SetupAccountPasswordRequest([Required,StringLength(64,MinimumLength=64)]string Token,
    [Required,StringLength(128,MinimumLength=8)]string NewPassword);
public sealed record ResendInvitationRequest([Required]string UserRowVersion);
public sealed record ForgotAccountPasswordRequest([Required,StringLength(100)]string Username);
