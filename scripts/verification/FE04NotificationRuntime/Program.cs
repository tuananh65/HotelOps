using System.Security;
using System.Text.Json;
using HotelOps.Api.Services.Housekeeping.Acceptance;
using Microsoft.Win32;

// No public host, SQL case writer, producer, worker, permission installer or DDL switch.
// Trusted owner keys must be provisioned separately into HKLM by the security/runtime owner.
try
{
    if (args is ["--plan"])
    {
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            status = "CONTRACT_PREPARED_NOT_ACCEPTED", sql = "15_NOT_RUN", https = "NOT_RUN",
            requiredOwners = Fe04NotificationApproval.Owners,
            requiredDecisions = Fe04NotificationApproval.Decisions,
            trustPolicy = @"HKLM\SOFTWARE\HotelOps\FE04NotificationAcceptance\OwnerKeys",
            businessCaller = "BLOCKED: published target/activation guards unchanged; owner-approved adapter wiring required"
        }));
        return 0;
    }
    if (args.Length != 4 || args[0] is not ("--check-approval" or "--preflight"))
        throw new InvalidOperationException("ONLY_PLAN_APPROVAL_CHECK_OR_READONLY_PREFLIGHT_SUPPORTED");
    if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("OWNER_WINDOWS_TRUST_POLICY_REQUIRED");
    var pins = new Dictionary<string, string>();
    using (var policy = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\HotelOps\FE04NotificationAcceptance\OwnerKeys", writable: false))
        foreach (var owner in Fe04NotificationApproval.Owners)
            pins[owner] = policy?.GetValue(owner) as string ?? "";
    using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(2));
    var ct = budget.Token;
    var packet = JsonSerializer.Deserialize<Fe04SignedApproval>(await File.ReadAllTextAsync(args[1], ct))
        ?? throw new InvalidOperationException("SIGNED_OWNER_PACKET_REQUIRED");
    var contract = Fe04NotificationApproval.Verify(packet, pins, DateTimeOffset.UtcNow);
    await Fe04NotificationApproval.VerifyFilesAsync(contract, args[2], args[3], ct);
    if (args[0] == "--check-approval")
    {
        Console.WriteLine("OWNER_SIGNATURES_AND_BOUND_SOURCE_VERIFIED; RUNTIME_NOT_VERIFIED; SQL_15_NOT_RUN");
        return 0;
    }
    // Credentials only through an inherited private pipe from the protected DPAPI launcher.
    // Never accept a connection string, password argument, env fallback or arbitrary SQL target.
    var input = await Console.In.ReadLineAsync(ct);
    using var json = JsonDocument.Parse(input ?? throw new InvalidOperationException("PROTECTED_CREDENTIAL_PIPE_REQUIRED"));
    if (json.RootElement.GetProperty("user").GetString() != Fe04NotificationApproval.Login)
        throw new InvalidOperationException("EXACT_RESTRICTED_SQL_LOGIN_REQUIRED");
    using var secure = new SecureString();
    var password = json.RootElement.GetProperty("password").GetString();
    if (string.IsNullOrEmpty(password)) throw new InvalidOperationException("PROTECTED_CREDENTIAL_REQUIRED");
    foreach (var character in password) secure.AppendChar(character);
    secure.MakeReadOnly();
    input = password = null;
    await using var adapter = await Fe04NotificationRuntimeAdapter.OpenAsync(packet, pins, secure,
        args[2], args[3], TimeProvider.System, ct);
    Console.WriteLine("RESTRICTED_PREFLIGHT_PASS; RUNNER_LOCK_HELD_DURING_CHECK_ONLY; SQL_15_NOT_RUN; HTTPS_NOT_RUN; BUSINESS_CALLER_NOT_ACTIVATED");
    return 0;
}
catch (Exception)
{
    // Driver/JSON/IO errors can contain sensitive configuration. Never print the exception.
    Console.Error.WriteLine("FE04_PREFLIGHT_BLOCKED: verify owner signatures, source hashes, lease, restricted identity, permissions and catalog; no acceptance PASS issued.");
    return 3;
}
