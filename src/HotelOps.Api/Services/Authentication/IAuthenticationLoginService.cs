using System.Text.Json.Serialization;
using HotelOps.Api.DTOs.Authentication;

namespace HotelOps.Api.Services.Authentication;

public interface IAuthenticationLoginService
{
    // Owns its transaction. Success is returned ONLY after commit; never retry automatically.
    Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<CurrentSession?> GetSessionAsync(SessionReference reference, long sessionId, CancellationToken cancellationToken = default);
}

public enum LoginStatus { InvalidCredentials, Locked, TemporaryExpired, Succeeded }

/// <summary>Service integration result, never an HTTP response DTO.</summary>
public sealed class LoginResult(LoginStatus status, AuthUserDto? user = null,
    CreatedSqlSession? createdSession = null, DateTime? lockedUntil = null)
{
    public LoginStatus Status { get; } = status;
    public AuthUserDto? User { get; } = user;
    [JsonIgnore] public CreatedSqlSession? CreatedSession { get; } = createdSession;
    public DateTime? LockedUntil { get; } = lockedUntil;
    public override string ToString() => "[Login result: authentication material redacted]";
}

public sealed record CurrentSession(AuthUserDto User, SessionSnapshot Session);
