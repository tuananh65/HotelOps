using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace UC004Trial;

// Proposed trial-only profile. No production DI registration, HTTP switch, DDL, or automatic activation.
public sealed record CreateAuditFaultProfile(string Revision, Guid DatabaseGuid, string LeaseId,
    string EmployeeCode, long ActorId, string HostArtifactSha256, string ApprovalReference,
    bool ExecutionApproved, DateTimeOffset StartsUtc, DateTimeOffset EndsUtc, int ExpectedHttpStatus);

public static class CreateAuditFaultPolicy
{
    public const string Revision = "UC004_UNEXPECTED_PRE_AUDIT_V1_PROPOSED";

    public static void Validate(CreateAuditFaultProfile p, Receipt receipt, string deployedHostSha256,
        string fixturePrefix, DateTimeOffset now)
    {
        TrialAdmission.Validate(receipt, receipt.Database, "UC004_HTTP", now);
        TrialAdmission.Require(p.Revision == Revision && p.ExpectedHttpStatus == 500,
            "UNEXPECTED_FAILURE_PROFILE_REQUIRED");
        TrialAdmission.Require(p.ExecutionApproved && !string.IsNullOrWhiteSpace(p.ApprovalReference),
            "SEPARATE_FAULT_APPROVAL_REQUIRED");
        TrialAdmission.Require(p.DatabaseGuid == receipt.DatabaseGuid && p.LeaseId == receipt.LeaseId
            && p.StartsUtc >= receipt.StartsUtc && p.EndsUtc <= receipt.EndsUtc
            && p.StartsUtc <= now && now < p.EndsUtc, "FAULT_LEASE_BINDING_REQUIRED");
        TrialAdmission.Require(System.Text.RegularExpressions.Regex.IsMatch(deployedHostSha256, @"\A[0-9a-fA-F]{64}\z")
            && p.HostArtifactSha256 == deployedHostSha256, "FAULT_HOST_ARTIFACT_REQUIRED");
        TrialAdmission.Require(System.Text.RegularExpressions.Regex.IsMatch(fixturePrefix, @"\AUC004_[a-f0-9]{12}_\z")
            && p.EmployeeCode == fixturePrefix + "auditFault" && p.ActorId > 0, "EXACT_FAULT_OPERATION_REQUIRED");
    }

    public static bool Matches(AuditLog audit, CreateAuditFaultProfile p)
    {
        if (audit.EntityType != "users" || audit.Action != "CREATE" || audit.ActorUserId != p.ActorId
            || string.IsNullOrWhiteSpace(audit.NewValues)) return false;
        try
        {
            using var json = JsonDocument.Parse(audit.NewValues);
            return json.RootElement.ValueKind == JsonValueKind.Object
                && json.RootElement.TryGetProperty("employeeCode", out var code)
                && code.ValueKind == JsonValueKind.String && code.GetString() == p.EmployeeCode;
        }
        catch (JsonException) { return false; }
    }
}

// Candidate host must first independently verify live SQL identity and bind this exact DbContext/transaction.
// This class is deliberately NOT instantiated by --uc004 or any production Program.cs.
public sealed class CreateAuditFault : SaveChangesInterceptor
{
    private HotelOpsDbContext? context;
    private Guid? transactionId;
    private readonly CreateAuditFaultProfile profile;
    private readonly Receipt receipt;
    private readonly string hostSha;
    private readonly string prefix;
    private readonly TimeProvider clock;
    private int fired;
    public bool Fired => Volatile.Read(ref fired) != 0;

    public CreateAuditFault(CreateAuditFaultProfile profile,
        Receipt receipt, string hostSha, string prefix, TimeProvider clock)
    {
        this.profile = profile;
        this.receipt = receipt; this.hostSha = hostSha; this.prefix = prefix; this.clock = clock;
        CreateAuditFaultPolicy.Validate(profile, receipt, hostSha, prefix, clock.GetUtcNow());
    }

    // Trial host registers this instance in EF options, creates the scoped context, then binds once before CreateAsync.
    public void BindContext(HotelOpsDbContext value)
    {
        ArgumentNullException.ThrowIfNull(value);
        TrialAdmission.Require(context is null && !value.ChangeTracker.Entries().Any()
            && value.Database.CurrentTransaction is null, "FRESH_SINGLE_CONTEXT_REQUIRED");
        context = value;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context is null || !ReferenceEquals(eventData.Context, context)) return ValueTask.FromResult(result);
        CreateAuditFaultPolicy.Validate(profile, receipt, hostSha, prefix, clock.GetUtcNow());
        var actualTransaction = context.Database.CurrentTransaction?.TransactionId;
        TrialAdmission.Require(actualTransaction is not null && actualTransaction != Guid.Empty, "BOUND_TRANSACTION_REQUIRED");
        transactionId ??= actualTransaction;
        TrialAdmission.Require(transactionId == actualTransaction, "EXACT_AUDIT_TRANSACTION_REQUIRED");
        var matches = context.ChangeTracker.Entries<AuditLog>().Where(e => e.State == EntityState.Added
            && CreateAuditFaultPolicy.Matches(e.Entity, profile)).ToArray();
        if (matches.Length == 0) return ValueTask.FromResult(result);
        CreateAuditFaultPolicy.Validate(profile, receipt, hostSha, prefix, clock.GetUtcNow());
        TrialAdmission.Require(matches.Length == 1 && context.Database.CurrentTransaction?.TransactionId == transactionId,
            "EXACT_AUDIT_TRANSACTION_REQUIRED");
        // Never permit a retry to turn a previously faulted operation into success.
        Interlocked.Exchange(ref fired, 1);
        throw new InvalidOperationException("UC004 trial unexpected pre-audit failure");
    }
}
