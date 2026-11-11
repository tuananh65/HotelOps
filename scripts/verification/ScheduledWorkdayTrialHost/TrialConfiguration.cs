using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ScheduledWorkdayTrial;

public sealed record TrialConfiguration(string Server, string Database, Guid DatabaseGuid, string Owner, string LeaseId,
    DateTimeOffset LeaseStartsUtc, DateTimeOffset LeaseEndsUtc, string ApprovalStatus, bool ExclusiveLease,
    bool TrialHostMutationApproval, bool AuditFaultInjectionApproval, string SchemaArtifact, string SchemaSha256,
    string FixtureArtifact, string FixtureSha256, string Origin, long ActorId, long BranchId, long DeniedBranchId,
    long AssignmentId, long RaceAssignmentId, long RollbackAssignmentId, string TrialPackageManifest, string TrialPackageSha256,
    long ShiftId, long RaceShiftId, long RollbackShiftId)
{
    public static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    public static async Task<TrialConfiguration> ReadAsync(string path, string hash)
    {
        var bytes = await File.ReadAllBytesAsync(path);
        if (!Hash(bytes).Equals(hash, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("receipt hash");
        return JsonSerializer.Deserialize<TrialConfiguration>(bytes, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("receipt shape");
    }
    public Uri Validate(bool execute, DateTimeOffset now)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(Database ?? "", "^HotelOps_UC139_Trial_[A-Za-z0-9_]{1,80}$")
            || DatabaseGuid == Guid.Empty || string.IsNullOrWhiteSpace(Server) || string.IsNullOrWhiteSpace(Owner)
            || string.IsNullOrWhiteSpace(LeaseId) || Owner.StartsWith("REQUIRES_", StringComparison.Ordinal)
            || LeaseId.StartsWith("REQUIRES_", StringComparison.Ordinal)) throw new InvalidOperationException("trial identity");
        if (!Uri.TryCreate(Origin, UriKind.Absolute, out var origin) || origin.Scheme != "https" || !origin.IsLoopback
            || origin.Port is <= 0 or > 65535 || origin.AbsolutePath != "/" || origin.Query != "" || origin.Fragment != ""
            || origin.UserInfo != "") throw new InvalidOperationException("loopback HTTPS origin");
        if (ActorId <= 0 || BranchId <= 0 || DeniedBranchId <= 0 || BranchId == DeniedBranchId
            || new[] { AssignmentId, RaceAssignmentId, RollbackAssignmentId }.Any(x => x <= 0)
            || new[] { AssignmentId, RaceAssignmentId, RollbackAssignmentId }.Distinct().Count() != 3
            || new[] { ShiftId, RaceShiftId, RollbackShiftId }.Any(x => x <= 0) || new[] { ShiftId, RaceShiftId, RollbackShiftId }.Distinct().Count() != 3)
            throw new InvalidOperationException("fixture IDs");
        if (execute && (ApprovalStatus != "APPROVED" || !ExclusiveLease || !TrialHostMutationApproval
            || LeaseStartsUtc > now || LeaseEndsUtc <= now || LeaseEndsUtc - LeaseStartsUtc > TimeSpan.FromHours(8)))
            throw new InvalidOperationException("active bounded exclusive lease");
        return origin;
    }
    public string ValidateConnection(string connection)
    {
        var cs = new SqlConnectionStringBuilder(connection);
        if (!cs.DataSource.Equals(Server, StringComparison.OrdinalIgnoreCase) || cs.InitialCatalog != Database
            || cs.TrustServerCertificate || cs.Encrypt.ToString() is not ("True" or "Mandatory" or "Strict")
            || cs.AttachDBFilename != "" || cs.FailoverPartner != "" || cs.UserInstance || cs.PersistSecurityInfo
            || !string.IsNullOrEmpty(cs.HostNameInCertificate) || !string.IsNullOrEmpty(cs.ServerCertificate))
            throw new InvalidOperationException("SQL identity/TLS");
        return cs.ConnectionString;
    }
    public async Task VerifyLiveIdentityAsync(string connection, CancellationToken ct)
    {
        Validate(true, DateTimeOffset.UtcNow);
        await using var sql = new SqlConnection(ValidateConnection(connection)); await sql.OpenAsync(ct);
        await using var command = sql.CreateCommand(); command.CommandTimeout = 15;
        command.CommandText = "SELECT DB_NAME(),CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')),(SELECT database_guid FROM sys.database_recovery_status WHERE database_id=DB_ID()),IS_SRVROLEMEMBER('sysadmin'),IS_MEMBER('db_owner'),CONVERT(nvarchar(128),(SELECT value FROM sys.extended_properties WHERE class=0 AND name='HotelOps.UC139.Trial'))";
        await using var row = await command.ExecuteReaderAsync(ct);
        if (!await row.ReadAsync(ct) || row.GetString(0) != Database || !row.GetString(1).Equals(Server, StringComparison.OrdinalIgnoreCase)
            || row.GetGuid(2) != DatabaseGuid || row.IsDBNull(3) || row.GetInt32(3) != 0 || row.IsDBNull(4) || row.GetInt32(4) != 0
            || row.IsDBNull(5) || row.GetString(5) != LeaseId) throw new InvalidOperationException("live isolated identity/principal/owner marker");
        await row.DisposeAsync();
        using var model = new HotelOps.Api.Data.HotelOpsDbContext(new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<HotelOps.Api.Data.HotelOpsDbContext>().UseSqlServer(connection).Options);
        command.CommandText = TrialArtifacts.VerificationSql(model) + "\n" + TrialArtifacts.BindFixture(this);
        await command.ExecuteNonQueryAsync(ct); // source-owned read-only metadata/fixture queries, no installer execution.
        if (await new HotelOps.Api.Data.StartupSchemaValidator(model).ValidateAsync(ct) != HotelOps.Api.Data.SchemaCompatibility.Compatible)
            throw new InvalidOperationException("existing Auth/schema prerequisites");
        Validate(true, DateTimeOffset.UtcNow);
    }
}
