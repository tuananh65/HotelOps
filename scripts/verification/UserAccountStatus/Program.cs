using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.DTOs.Users;
using System.Text.Json;

// Independent session-policy regression only. No SQL connection or UC-006 mutation.
var checks = 0;
void Check(bool value, string label)
{
    if (!value) throw new InvalidOperationException("FAIL: " + label);
    Console.WriteLine("PASS: " + label);
    checks++;
}
var now = new DateTime(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);
var created = now.AddMinutes(-1);
var credential = SessionCredential.Generate();
var ticket = SessionReference.FromProtectedTicket(1, credential, false, created);
var user = new User { UserId = 1, AccountStatus = "ACTIVE", EmploymentStatus = "ACTIVE" };
var session = new UserSession
{
    SessionId = 1, UserId = 1, SessionKeyHash = SessionCredential.Hash(credential),
    CreatedAt = created, LastUsedAt = created, ExpiresAt = now.AddMinutes(20)
};
Check(SqlSessionPolicy.Validate(user, session, ticket, now) is not null, "active baseline session valid");
user.AccountStatus = "INACTIVE";
Check(SqlSessionPolicy.Validate(user, session, ticket, now) is null, "inactive account rejects existing ticket");
session.RevokedAt = now;
user.AccountStatus = "ACTIVE";
Check(SqlSessionPolicy.Validate(user, session, ticket, now) is null, "reactivation cannot revive revoked session");
session.RevokedAt = null; // Independent deleted/expired policy cases, not a recovery workflow.
user.DeletedAt = now;
Check(SqlSessionPolicy.Validate(user, session, ticket, now) is null, "soft-deleted active account rejects ticket");
user.DeletedAt = null;
session.ExpiresAt = now;
Check(SqlSessionPolicy.Validate(user, session, ticket, now) is null, "activation cannot revive expired session");
session.ExpiresAt = now.AddMinutes(20);
user.AccountStatus = "UNKNOWN";
Check(SqlSessionPolicy.Validate(user, session, ticket, now) is null, "unknown account status fails closed");
foreach (var json in new[] { "{}", "[]", "null", "{\"status\":\"ACTIVE\",\"reason\":\"short\"}",
    "{\"status\":\"active\",\"reason\":\"valid reason text\"}",
    "{\"status\":\"ACTIVE\",\"reason\":\"valid reason text\",\"actorId\":1}",
    "{\"status\":\"ACTIVE\",\"status\":\"INACTIVE\",\"reason\":\"valid reason text\"}",
    "{\"status\":\"ACTIVE\",\"reason\":\"valid reason text\",\"reason\":\"duplicate reason\"}",
    "{\"status\":true,\"reason\":\"valid reason text\"}", "{\"status\":\"ACTIVE\",\"reason\":null}",
    "{\"status\":\"ACTIVE\",\"reason\":\"        123456789       \"}" })
{
    using var document = JsonDocument.Parse(json);
    Check(!UserAccountStatusChange.TryParse(document.RootElement, out _), "strict parser rejects invalid contract");
}
foreach (var status in new[] { "ACTIVE", "INACTIVE" })
{
    using var document = JsonDocument.Parse(JsonSerializer.Serialize(new { status, reason = "  valid reason text  " }));
    Check(UserAccountStatusChange.TryParse(document.RootElement, out var change) && change!.Reason == "valid reason text",
        "parser accepts exact status and trims reason");
    Check(!change!.IsTransitionFrom(status), "same-status transition rejected");
    Check(change.IsTransitionFrom(status == "ACTIVE" ? "INACTIVE" : "ACTIVE"), "opposite transition valid");
    Check(!change.IsTransitionFrom("UNKNOWN"), "unknown source status rejected");
}
Check(!new UserAccountStatusChange("ACTIVE", "short").IsValid, "service validation rejects parser bypass");
Console.WriteLine($"PASS: {checks} independent parser/session-policy assertions; HTTP certification is a separate suite.");
if (args.Length != 0) await ActivationSqlSuite.RunAsync(args);
