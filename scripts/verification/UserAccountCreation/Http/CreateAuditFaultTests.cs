using HotelOps.Api.Entities;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace UC004Trial;

public static class CreateAuditFaultTests
{
    public static void Run()
    {
        var now = DateTimeOffset.UtcNow;
        var receipt = new Receipt
        {
            ExecutionApproved = true,
            Owner = "offline",
            ApprovalReference = "offline-only",
            Host = Environment.MachineName,
            Database = "HotelOps_Canonical_20261113_120000_abcdef12_Fresh",
            DatabaseGuid = Guid.NewGuid(),
            Server = "offline.invalid",
            Disposable = true,
            FixtureOwned = true,
            Exclusive = true,
            RollbackRequired = true,
            IsolationMarker = new('X', 16),
            Principal = "restricted",
            PrincipalSidHex = "01",
            PrincipalApprovalReference = "offline-only",
            LeaseId = "offline",
            StartsUtc = now.AddMinutes(-1),
            EndsUtc = now.AddMinutes(1),
            Mode = "UC004_HTTP",
            SchemaPath = "offline",
            SchemaSha256 = new('A', 64),
            FixturePath = "offline",
            FixtureSha256 = new('B', 64),
            ApiSha256 = new('C', 64),
            RunnerSha256 = new('D', 64),
            SchemaApprovalReference = "offline",
            FixtureApprovalReference = "offline"
        };
        const string prefix = "UC004_abcdef123456_";
        var profile = new CreateAuditFaultProfile(CreateAuditFaultPolicy.Revision, receipt.DatabaseGuid, receipt.LeaseId,
            prefix + "auditFault", 1, new('E', 64), "offline-only-not-approval", true, receipt.StartsUtc, receipt.EndsUtc, 500);
        var passed = 0;
        void Check(bool value) { if (!value) throw new InvalidOperationException("Fault profile offline assertion failed"); passed++; }
        void Validate(CreateAuditFaultProfile p) => CreateAuditFaultPolicy.Validate(p, receipt, profile.HostArtifactSha256, prefix, now);
        Validate(profile); Check(true);
        foreach (var invalid in new[] {
            profile with { ExecutionApproved = false }, profile with { ApprovalReference = "" }, profile with { Revision = "SQL40501" },
            profile with { ExpectedHttpStatus = 503 }, profile with { DatabaseGuid = Guid.NewGuid() }, profile with { LeaseId = "other" },
            profile with { StartsUtc = now.AddSeconds(1) }, profile with { EndsUtc = now },
            profile with { EndsUtc = receipt.EndsUtc.AddSeconds(1) }, profile with { HostArtifactSha256 = new('F',64) },
            profile with { EmployeeCode = prefix + "other" }, profile with { ActorId = 0 }
        })
        {
            var rejected = false;
            try { Validate(invalid); } catch (AdmissionFailure) { rejected = true; }
            Check(rejected);
        }
        var audit = new AuditLog
        {
            EntityType = "users",
            Action = "CREATE",
            ActorUserId = 1,
            NewValues = System.Text.Json.JsonSerializer.Serialize(new { employeeCode = profile.EmployeeCode })
        };
        Check(CreateAuditFaultPolicy.Matches(audit, profile));
        audit.ActorUserId = 2; Check(!CreateAuditFaultPolicy.Matches(audit, profile));
        audit.ActorUserId = 1; audit.Action = "UPDATE"; Check(!CreateAuditFaultPolicy.Matches(audit, profile));
        audit.Action = "CREATE"; audit.NewValues = "{"; Check(!CreateAuditFaultPolicy.Matches(audit, profile));
        audit.NewValues = "[]"; Check(!CreateAuditFaultPolicy.Matches(audit, profile));
        audit.NewValues = "{\"employeeCode\":1}"; Check(!CreateAuditFaultPolicy.Matches(audit, profile));
        using var context = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer("Server=offline.invalid;Database=offline;Integrated Security=true;Encrypt=true;TrustServerCertificate=false").Options);
        var interceptor = new CreateAuditFault(profile, receipt, profile.HostArtifactSha256, prefix, TimeProvider.System);
        interceptor.BindContext(context); Check(!interceptor.Fired);
        var doubleBindRejected = false;
        try { interceptor.BindContext(context); } catch (AdmissionFailure) { doubleBindRejected = true; }
        Check(doubleBindRejected);
        var cancelled = false;
        try { interceptor.SavingChangesAsync(null!, default, new CancellationToken(true)).GetAwaiter().GetResult(); }
        catch (OperationCanceledException) { cancelled = true; }
        Check(cancelled && !interceptor.Fired);
        context.AuditLogs.Add(audit);
        var dirtyRejected = false;
        try { new CreateAuditFault(profile, receipt, profile.HostArtifactSha256, prefix, TimeProvider.System).BindContext(context); }
        catch (AdmissionFailure) { dirtyRejected = true; }
        Check(dirtyRejected);
        // Empty contexts exercise the interception boundary without opening a connection.
        foreach (var bindOtherContext in new[] { false, true })
        {
            var isolated = new CreateAuditFault(profile, receipt, profile.HostArtifactSha256, prefix, TimeProvider.System);
            using var other = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
                .UseSqlServer("Server=offline.invalid;Database=offline;Integrated Security=true;Encrypt=true;TrustServerCertificate=false").Options);
            if (bindOtherContext) isolated.BindContext(other);
            using var caller = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
                .UseSqlServer("Server=offline.invalid;Database=offline;Integrated Security=true;Encrypt=true;TrustServerCertificate=false")
                .AddInterceptors(isolated).Options);
            var rejected = false;
            try { caller.SaveChangesAsync().GetAwaiter().GetResult(); }
            catch (AdmissionFailure e) when (e.Message == "EXACT_BOUND_CONTEXT_REQUIRED") { rejected = true; }
            Check(rejected && !isolated.Fired);
        }
        Console.WriteLine($"UC004 fault admission/matching: {passed}/25 PASS; interceptor/rollback/HTTP runtime NOT_RUN.");
        if (passed != 25) throw new InvalidOperationException("Incomplete offline suite");
    }
}
