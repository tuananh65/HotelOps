using System.Data.Common;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.HotelStructure;
using HotelOps.Api.Entities;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.Users;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

internal static class Task70SqlCases
{
    internal static async Task RunAsync(HotelOpsDbContext setup, Func<DbCommandInterceptor?, HotelOpsDbContext> open,
        long actor, long branch, long type, long checklist, long floor, Action<bool, string> check, CancellationToken ct)
    {
        var raw = SessionCredential.Generate(); var now = DateTime.UtcNow;
        var row = new UserSession { UserId = actor, SessionKeyHash = SessionCredential.Hash(raw), CreatedAt = now, LastUsedAt = now, ExpiresAt = now.AddMinutes(30) };
        setup.Add(row); await setup.SaveChangesAsync(ct); setup.ChangeTracker.Clear();
        var ticket = SessionReference.FromProtectedTicket(actor, raw, false, now);
        var snapshot = new SessionSnapshot(row.SessionId, actor, now, row.ExpiresAt, now.AddHours(12), false);
        RoomTypeCreationService Service(HotelOpsDbContext db)
        {
            var context = new DefaultHttpContext { User = HotelOpsCookieTicket.Create(new(ticket, snapshot), TimeProvider.System).Principal };
            context.Items[typeof(SessionSnapshot)] = snapshot;
            var current = new CurrentUserAccessor(new Context { HttpContext = context });
            return new(db, current, new PermissionService(current, new EfPermissionDataSource(db), new UnresolvedPermissionResourceResolver(), TimeProvider.System),
                new SqlSessionService(db, new SessionRevocationService(db), TimeProvider.System), TimeProvider.System);
        }
        async Task<string> State()
        {
            return await setup.Database.SqlQueryRaw<string>("SELECT (SELECT * FROM dbo.room_types ORDER BY room_type_id FOR JSON PATH,INCLUDE_NULL_VALUES)+(SELECT * FROM dbo.rooms ORDER BY room_id FOR JSON PATH,INCLUDE_NULL_VALUES)+(SELECT * FROM dbo.room_status_history ORDER BY history_id FOR JSON PATH,INCLUDE_NULL_VALUES)+(SELECT * FROM dbo.audit_logs ORDER BY audit_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS [Value]").SingleAsync(ct);
        }
        foreach (var uc in new[] { "UC018", "UC020", "UC142" })
        foreach (var fault in new[] { "session", "grant", "cancel" })
        {
            string? tag = null;
            if (uc == "UC018")
            {
                await using var reader = open(null);
                tag = (await Service(reader).ReadRoomTypeAsync(ticket, row.SessionId, branch, type, ct)).ETag;
            }
            var before = await State();
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var hook = new Probe { Uc = uc, Fault = fault, Actor = actor, Session = row.SessionId, Type = type, Stop = stop };
            await using var writer = open(hook);
            RoomTypeBatchResult? result = null; var cancelled = false;
            try
            {
                var service = Service(writer);
                result = uc switch
                {
                    "UC018" => await service.UpdateRoomTypeAsync(ticket, row.SessionId, branch, type, tag!, new("T70 SQL final gate", 66, checklist), stop.Token),
                    "UC020" => await service.CreateRoomAsync(ticket, row.SessionId, new(branch, floor, type, "T70_SQL_" + fault), stop.Token),
                    _ => await service.ListRoomTypesAsync(ticket, row.SessionId, branch, null, "T70_TYPE", 1, 20, stop.Token)
                };
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { cancelled = true; }
            check(hook.Fired && (uc == "UC142" || hook.SawBusiness), uc + " actual SQL reaches " + fault + " final-boundary probe after business write/page");
            check(fault == "cancel" ? cancelled : result?.Status == (fault == "session" ? RoomTypeBatchStatus.SessionInvalid : RoomTypeBatchStatus.Forbidden),
                uc + " rejects final " + fault + " after initial SQL authorization");
            check(await State() == before && writer.Database.CurrentTransaction == null && !writer.ChangeTracker.Entries().Any(),
                uc + " " + fault + " preserves all room/type/history/audit rows and cleans context");
        }
    }
    private sealed class Context : IHttpContextAccessor { public HttpContext? HttpContext { get; set; } }
    private sealed class Probe : DbCommandInterceptor
    {
        internal string Uc = "", Fault = "";
        internal long Actor, Session, Type;
        internal CancellationTokenSource Stop = null!;
        internal bool Fired, SawBusiness;
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData data,
            InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        {
            if (Fired || !(Uc == "UC142" ? command.CommandText.Contains("UC142:Page") : command.CommandText.Contains("INSERT INTO [audit_logs]"))) return result;
            Fired = true;
            await using var proof = command.Connection!.CreateCommand(); proof.Transaction = command.Transaction;
            if (Uc != "UC142")
            {
                proof.CommandText = Uc == "UC018" ? "SELECT COUNT(*) FROM dbo.room_types WHERE room_type_id=@type AND type_name=N'T70 SQL final gate'"
                    : "SELECT COUNT(*) FROM dbo.rooms WHERE room_number=@number";
                proof.Parameters.Add(new SqlParameter("@type", Type)); proof.Parameters.Add(new SqlParameter("@number", "T70_SQL_" + Fault));
                SawBusiness = (int)(await proof.ExecuteScalarAsync(ct))! == 1;
            }
            if (Fault == "cancel") { Stop.Cancel(); Stop.Token.ThrowIfCancellationRequested(); }
            proof.Parameters.Clear();
            proof.CommandText = Fault == "session" ? "UPDATE dbo.user_sessions SET expires_at=DATEADD(second,-1,SYSUTCDATETIME()) WHERE session_id=@id"
                : "UPDATE dbo.user_roles SET expires_at=DATEADD(second,-1,SYSUTCDATETIME()) WHERE user_id=@id";
            proof.Parameters.Add(new SqlParameter("@id", Fault == "session" ? Session : Actor));
            await proof.ExecuteNonQueryAsync(ct);
            return result;
        }
    }
}
