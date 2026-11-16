using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Services.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Dev2Authorization;

internal static class Dev2SessionValidation
{
    internal static async Task<SessionValidationResult> ValidateAsync(HotelOpsDbContext owner,
        SessionReference ticket, TimeProvider clock, CancellationToken ct)
    {
        var transaction = owner.Database.CurrentTransaction;
        if (!owner.Database.IsSqlServer() || transaction == null
            || transaction.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            throw new Dev2AccessDenied(503);
        // Session operations require no tracked entities. Reuse the action's SQL
        // transaction without flushing/detaching its pending aggregate and audit.
        await using var sessionDb = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer(owner.Database.GetDbConnection()).Options);
        await sessionDb.Database.UseTransactionAsync(transaction.GetDbTransaction(), ct);
        var sessions = new SqlSessionService(sessionDb, new SessionRevocationService(sessionDb), clock);
        return await sessions.ValidateSessionAsync(ticket, ct);
    }
}
