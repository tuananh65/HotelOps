using System.Text.Json.Serialization;

namespace HotelOps.Api.Services.Authentication;

/// <summary>
/// All operations require the caller's SQL transaction, a clean context and
/// user-before-session lock ordering. Caller commits, or rolls back on exceptions;
/// no cookie may be issued before commit. Never use these results as HTTP DTOs.
/// </summary>
public interface ISqlSessionService
{
    Task<CreatedSqlSession> CreateSessionAsync(CredentialLockoutResult verifiedCredential, CancellationToken cancellationToken = default);
    Task<SessionValidationResult> ValidateSessionAsync(SessionReference ticket, CancellationToken cancellationToken = default);
    Task<SessionValidationResult> TouchSessionAsync(SessionReference ticket, CancellationToken cancellationToken = default);
    Task<bool> RevokeSessionAsync(SessionReference ticket, CancellationToken cancellationToken = default);
    Task<int> RevokeAllSessionsAsync(long userId, CancellationToken cancellationToken = default);
}

public sealed record SessionSnapshot(long SessionId, long UserId, DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc, DateTime AbsoluteExpiresAtUtc, bool IsRestricted);

public sealed record SessionValidationResult(bool IsValid, SessionSnapshot? Session = null);

public sealed class CreatedSqlSession(SessionReference ticket, SessionSnapshot session)
{
    [JsonIgnore]
    public SessionReference Ticket { get; } = ticket;
    public SessionSnapshot Session { get; } = session;
    public override string ToString() => "[Created session: credential redacted; commit required]";
}

/// <summary>
/// Internal integration object, NOT a request/response DTO. Construct only from
/// an authenticated Data Protection ticket (scheme/purpose/version verified).
/// ExportCredentialForProtectedTicket must never go to JSON, logs, audit or URLs.
/// </summary>
public sealed class SessionReference
{
    private readonly string _credential;
    public long UserId { get; }
    public bool IsRestricted { get; }
    public DateTime CreatedAtUtc { get; }
    internal string KeyHash => SessionCredential.Hash(_credential);

    private SessionReference(long userId, string credential, bool restricted, DateTime createdAtUtc)
        => (UserId, _credential, IsRestricted, CreatedAtUtc) = (userId, credential, restricted, createdAtUtc);

    public static SessionReference FromProtectedTicket(long userId, string credential, bool restricted, DateTime createdAtUtc)
    {
        if (userId <= 0 || createdAtUtc.Kind != DateTimeKind.Utc || !SessionCredential.IsValid(credential))
            throw new ArgumentException("Invalid protected session ticket fields.");
        return new(userId, credential, restricted, createdAtUtc);
    }

    public string ExportCredentialForProtectedTicket() => _credential;
    public override string ToString() => "[Session reference: credential redacted]";
}
