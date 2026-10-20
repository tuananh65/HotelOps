using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Services.Authentication;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Security;

public interface ICookieSessionValidator
{
    Task<SessionValidationResult> ValidateAsync(SessionReference ticket, CancellationToken cancellationToken);
}

public interface ICookieSessionToucher
{
    Task<SessionValidationResult> TouchAsync(SessionReference ticket, CancellationToken cancellationToken);
}

public sealed class CookieSessionValidator(HotelOpsDbContext db, ISqlSessionService sessions)
    : ICookieSessionValidator, ICookieSessionToucher
{
    public Task<SessionValidationResult> ValidateAsync(SessionReference ticket, CancellationToken cancellationToken)
        => ExecuteAsync(ticket, false, cancellationToken);

    public Task<SessionValidationResult> TouchAsync(SessionReference ticket, CancellationToken cancellationToken)
        => ExecuteAsync(ticket, true, cancellationToken);

    private async Task<SessionValidationResult> ExecuteAsync(SessionReference ticket, bool touch, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction != null || System.Transactions.Transaction.Current != null)
            throw new InvalidOperationException("Cookie validation must precede business transactions.");
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            var result = touch
                ? await sessions.TouchSessionAsync(ticket, cancellationToken)
                : await sessions.ValidateSessionAsync(ticket, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (Exception e) when (!cancellationToken.IsCancellationRequested
            && e is not SessionStoreUnavailableException && SessionStoreUnavailableException.Matches(e))
        {
            // Includes connection/BeginTransaction failures outside SqlSessionService.
            throw new SessionStoreUnavailableException(e);
        }
    }
}
