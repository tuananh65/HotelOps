using System.Text.Json.Serialization;
using HotelOps.Api.DTOs.Authentication;

namespace HotelOps.Api.Services.Authentication;

public interface IAuthenticationCredentialWorkflow
{
    /// <summary>Null only when the HTTP authentication layer has established anonymous/invalid identity.</summary>
    Task LogoutAsync(SessionReference? reference, CancellationToken cancellationToken = default);

    /// <summary>Returns success only after commit. No caller transaction or automatic retry.</summary>
    Task<ChangePasswordResult> ChangePasswordAsync(SessionReference reference, long sessionId,
        ChangePasswordRequest request, CancellationToken cancellationToken = default);
}

public enum ChangePasswordStatus { Succeeded, ValidationFailed, PolicyViolation, CurrentPasswordInvalid, SessionInvalid, StateChanged }

/// <summary>Internal integration result; never use as an HTTP DTO.</summary>
public sealed class ChangePasswordResult(ChangePasswordStatus status, AuthUserDto? user = null, CreatedSqlSession? createdSession = null)
{
    public ChangePasswordStatus Status { get; } = status;
    public AuthUserDto? User { get; } = user;
    [JsonIgnore] public CreatedSqlSession? CreatedSession { get; } = createdSession;
    public override string ToString() => "[Change password result: credential redacted]";
}
