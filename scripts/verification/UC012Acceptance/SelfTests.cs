namespace UC012Acceptance;

public static class SelfTests
{
    public static int Run()
    {
        var count = 0; void Check(bool ok) { if (!ok) throw new Exception("GUARD_TEST_FAILED"); count++; }
        var now = DateTimeOffset.UtcNow;
        var valid = new TrialReceipt { Owner = "fixture-owner", ApprovalReference = "synthetic-only", Server = "SYNTHETIC\\TEST", Database = "HotelOps_UC012_Trial_synthetic", DatabaseGuid = Guid.NewGuid(), SqlPrincipal = "restricted", IsolationMarker = new string('x', 16), LeaseId = "fixture", LeaseStartUtc = now.AddMinutes(-1), LeaseEndUtc = now.AddMinutes(5), Disposable = true, ExclusiveLease = true, ExecutionApproved = true, FixtureOwned = true, RollbackRequired = true, AuditFaultStatus = 503, AuditFaultApproval = "synthetic-only", ActorId = 1, DeniedActorId = 2, ActorUsername = "actor", DeniedUsername = "denied", CodePrefix = "TRIAL", AllowedMutations = Guards.Mutations };
        valid = valid with { HostMachine = Environment.MachineName };
        Guards.Validate(valid, now); Check(true);
        foreach (var bad in new[] { new TrialReceipt(), valid with { ExecutionApproved = false }, valid with { Disposable = false }, valid with { Database = "HotelOpsDB" }, valid with { DatabaseGuid = Guid.Empty }, valid with { ExclusiveLease = false }, valid with { Owner = "" }, valid with { ApprovalReference = "" }, valid with { IsolationMarker = "" }, valid with { SqlPrincipal = "" }, valid with { LeaseEndUtc = now }, valid with { LeaseStartUtc = now.AddSeconds(1) }, valid with { AuditFaultStatus = null }, valid with { AuditFaultStatus = 200 }, valid with { AuditFaultApproval = "" }, valid with { FixtureOwned = false }, valid with { RollbackRequired = false }, valid with { ActorId = 0 }, valid with { ActorId = 2 }, valid with { Origin = "http://localhost:7445" }, valid with { Origin = "https://example.test:7445" }, valid with { Origin = "https://user@localhost:7445" }, valid with { Origin = "https://localhost:7445/?token=x" }, valid with { AllowedMutations = ["CREATE_BRANCH"] }, valid with { CodePrefix = "x%" } })
        { try { Guards.Validate(bad, now); throw new Exception("ACCEPTED_INVALID_RECEIPT"); } catch (GuardFailure) { Check(true); } }
        var cs = "Server=SYNTHETIC\\TEST;Database=HotelOps_UC012_Trial_synthetic;Integrated Security=true;Encrypt=true;TrustServerCertificate=false";
        Guards.Connection(valid, cs); Check(true);
        foreach (var bad in new[] { cs.Replace("Encrypt=true", "Encrypt=false"), cs.Replace("TrustServerCertificate=false", "TrustServerCertificate=true"), cs.Replace("HotelOps_UC012_Trial_synthetic", "HotelOpsDB"), cs.Replace("SYNTHETIC", "OTHER"), cs + ";Failover Partner=other" })
        { try { Guards.Connection(valid, bad); throw new Exception("ACCEPTED_UNSAFE_CONNECTION"); } catch (GuardFailure) { Check(true); } }
        foreach (var tag in new string?[] { null, "*", "W/\"AAAAAAAAAAA=\"", "\"0011223344556677\"", "\"AAAAAAAAAAAA\"" }) Check(!Cases.StrongTag(tag));
        Check(Cases.StrongTag("\"AAAAAAAAAAA=\"")); Check(Guards.Cases.Length == 12 && Guards.Cases.Distinct().Count() == 12);
        try { Guards.Validate(valid with { HostMachine = "UNRELATED_HOST" }, now); throw new Exception("HOST_NOT_BOUND"); } catch (GuardFailure) { Check(true); }
        foreach (var extra in new[] { ";Host Name In Certificate=other", ";Server Certificate=unapproved.pem" })
        { try { Guards.Connection(valid, cs + extra); throw new Exception("CERT_OVERRIDE_ACCEPTED"); } catch (GuardFailure) { Check(true); } }
        Check(Host.Controllers.Length == 3 && !Host.Controllers.Any(t => t.Name.Contains("Workday") || t.Name == "BranchAdministrationController"));
        try { Host.StartAsync(new TrialReceipt(), "unused", new FaultSwitch(), default).GetAwaiter().GetResult(); throw new Exception("HOST_GUARD_BYPASSED"); }
        catch (GuardFailure) { Check(true); }
        Console.WriteLine($"{count}/{count} guard tests PASS; SQL/HTTPS NOT_RUN"); return 0;
    }
}
