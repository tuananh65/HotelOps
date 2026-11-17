using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Insights;

public sealed class DashboardSessionGuard(IHttpContextAccessor http, ISqlSessionService sessions, ICurrentUserAccessor current,
    HotelOpsDbContext db, TimeProvider clock)
{
    internal async Task RequireAsync(CancellationToken ct)
    {
        var context = http.HttpContext;
        if (context == null || !context.Request.IsHttps
            || !HotelOpsCookieTicket.TryRead(context.User, out var ticket, out var sid)
            || !current.TryGetCurrentUser(out var actor) || actor.SessionId != sid || actor.UserId != ticket!.UserId
            || actor.IsRestricted || ticket.IsRestricted)
            throw new InsightException(401, "SESSION_INVALID", "Current unrestricted HTTPS session required.");
        SessionValidationResult result;
        if (!db.ChangeTracker.Entries().Any()) result = await sessions.ValidateSessionAsync(ticket, ct);
        else
        {
            // Keep the writer's pending aggregate/audit intact. Session validation
            // uses a clean context enlisted in the SAME connection/transaction,
            // retaining its locks and rollback boundary (never an independent read).
            var transaction = db.Database.CurrentTransaction
                ?? throw new InsightException(503, "SESSION_TRANSACTION_REQUIRED", "Session revalidation requires the owning transaction.");
            await using var sessionDb = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
                .UseSqlServer(db.Database.GetDbConnection()).Options);
            await sessionDb.Database.UseTransactionAsync(transaction.GetDbTransaction(), ct);
            var isolated = new SqlSessionService(sessionDb, new SessionRevocationService(sessionDb), clock);
            result = await isolated.ValidateSessionAsync(ticket, ct);
        }
        if (!result.IsValid || result.Session?.SessionId != sid || result.Session.IsRestricted)
            throw new InsightException(401, "SESSION_INVALID", "Session expired or revoked.");
    }
}
