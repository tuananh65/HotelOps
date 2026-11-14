using System.Net.Http.Json;
using System.Security.Cryptography;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

internal static class SprintActivationScenario
{
    // Invoked only after SprintAuthProbe verifies the exact disposable identity and empty baseline.
    internal static async Task RunAsync(HotelOpsDbContext db, HttpClient http, long actor, string csrf,
        List<object> checks, CancellationToken ct, string? username = null, string? password = null)
    {
        if (await db.UserRoles.AnyAsync(ct)) throw new Exception("Empty grant baseline required");
        var prefix = "D3-ACT-" + Guid.NewGuid().ToString("N")[..8];
        var now = DateTime.UtcNow;
        var target = new User { EmployeeCode = prefix, Username = prefix, Email = prefix + "@example.invalid",
            FullName = "Synthetic activation target", AccountStatus = "INACTIVE", EmploymentStatus = "ACTIVE",
            CreatedAt = now, LockedUntil = now.AddHours(1), FailedLoginCount = 5,
            TemporaryPasswordExpiresAt = now.AddMinutes(-1), PasswordHash = "synthetic-unusable-hash" };
        long grantId = 0;
        async Task<string?> Patch(long id, int expected, string? tag, bool token = true, string reason = "Synthetic activation verification")
        {
            using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/users/{id}/activate")
                { Content = JsonContent.Create(new { reason }) };
            if (token) request.Headers.Add("X-CSRF-TOKEN", csrf);
            if (tag != null) request.Headers.TryAddWithoutValidation("If-Match", tag);
            using var response = await http.SendAsync(request, ct);
            checks.Add(new { activationExpected = expected, actual = (int)response.StatusCode,
                hasIfMatch = tag != null, hasCsrf = token, selfTarget = id == actor });
            if ((int)response.StatusCode != expected) throw new Exception("Activation response mismatch: " + (int)response.StatusCode);
            return response.Headers.ETag?.Tag;
        }
        try
        {
            db.Users.Add(target); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            db.UserSessions.Add(new UserSession { UserId = target.UserId,
                SessionKeyHash = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
                CreatedAt = now.AddMinutes(-10), ExpiresAt = now.AddHours(2), RevokedAt = now.AddMinutes(-5) });
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            var before = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == target.UserId, ct);
            var priorSession = await db.UserSessions.AsNoTracking().SingleAsync(s => s.UserId == target.UserId, ct);
            var tag = '"' + Convert.ToHexString(before.RowVersion) + '"';
            await Patch(target.UserId, 400, tag, token: false);
            await Patch(target.UserId, 428, null);
            await Patch(target.UserId, 400, "W/" + tag);
            await Patch(target.UserId, 400, tag, reason: "short");
            await Patch(target.UserId, 403, tag);
            var admin = await db.Roles.Where(r => r.RoleCode == "SYS_ADMIN" && r.IsActive).Select(r => r.RoleId).SingleAsync(ct);
            var grant = new UserRole { UserId = actor, RoleId = admin, IsActive = true, AssignedAt = now.AddMinutes(-1) };
            db.UserRoles.Add(grant); await db.SaveChangesAsync(ct); grantId = grant.UserRoleId; db.ChangeTracker.Clear();
            await Patch(actor, 403, tag);
            await Patch(target.UserId, 412, "\"0000000000000000\"");
            using (var profile = await http.GetAsync($"/api/users/{target.UserId}/profile", ct))
            {
                if ((int)profile.StatusCode != 200 || profile.Headers.ETag?.Tag != tag)
                    throw new Exception("Inactive management profile/version unavailable");
            }
            string? next;
            if (username != null && password != null)
            {
                var result = await SprintActivationBrowser.RunAsync(username, password, target.UserId, ct);
                checks.Add(new { activationBrowser = result });
                if (!result.GetProperty("success").GetBoolean()) throw new Exception("Activation browser failed");
                next = '"' + Convert.ToHexString(await db.Users.Where(u => u.UserId == target.UserId).Select(u => u.RowVersion).SingleAsync(ct)) + '"';
                if (await db.UserSessions.CountAsync(s => s.UserId == actor && s.RevokedAt == null, ct) != 1)
                    throw new Exception("Browser administrator session not revoked on logout");
                checks.Add(new { browserLogoutPersisted = true });
            }
            else next = await Patch(target.UserId, 200, tag);
            var after = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == target.UserId, ct);
            var session = await db.UserSessions.AsNoTracking().SingleAsync(s => s.UserId == target.UserId, ct);
            if (after.AccountStatus != "ACTIVE" || next == tag || next != '"' + Convert.ToHexString(after.RowVersion) + '"'
                || after.LockedUntil != before.LockedUntil || after.FailedLoginCount != before.FailedLoginCount
                || after.PasswordHash != before.PasswordHash || after.TemporaryPasswordExpiresAt != before.TemporaryPasswordExpiresAt
                || session.RevokedAt != priorSession.RevokedAt || session.SessionKeyHash != priorSession.SessionKeyHash)
                throw new Exception("Activation altered credential/lockout/session fields or returned wrong version");
            await Patch(target.UserId, 412, tag);
            await Patch(target.UserId, 409, next);
            var entityId = target.UserId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var audit = await db.AuditLogs.AsNoTracking().Where(a => a.ActorUserId == actor && a.EntityType == "users" && a.EntityId == entityId).ToArrayAsync(ct);
            if (audit.Length != 1 || audit[0].Action != "UPDATE" || !audit[0].NewValues!.Contains("Synthetic activation verification"))
                throw new Exception("Activation audit missing, duplicated or incomplete");
            checks.Add(new { accountActivated = true, currentEtag = true, lockoutPreserved = true,
                credentialExpiryPreserved = true, revokedSessionPreserved = true, singleAudit = true });
        }
        finally
        {
            db.ChangeTracker.Clear(); await using var cleanup = await db.Database.BeginTransactionAsync(CancellationToken.None);
            var entityId = target.UserId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            await db.AuditLogs.Where(a => a.ActorUserId == actor && a.EntityType == "users" && a.EntityId == entityId).ExecuteDeleteAsync(CancellationToken.None);
            await db.UserSessions.Where(s => s.UserId == target.UserId).ExecuteDeleteAsync(CancellationToken.None);
            // Restrict cleanup to this exact actor/role, including an ambiguous fixture SaveChanges outcome.
            await db.UserRoles.Where(r => r.UserId == actor && (r.UserRoleId == grantId || r.Role.RoleCode == "SYS_ADMIN")).ExecuteDeleteAsync(CancellationToken.None);
            await db.Users.Where(u => u.UserId == target.UserId && u.Username == prefix).ExecuteDeleteAsync(CancellationToken.None);
            await cleanup.CommitAsync(CancellationToken.None);
            if (await db.Users.AnyAsync(u => u.UserId == target.UserId) || await db.UserRoles.AnyAsync()) throw new Exception("Activation cleanup failed");
            checks.Add(new { activationFixtureCleanup = true });
        }
    }
}
