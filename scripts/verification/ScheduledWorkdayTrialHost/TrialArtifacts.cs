using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ScheduledWorkdayTrial;

public static class TrialArtifacts
{
    static readonly string[] Tables = ["workdays", "work_intervals", "time_correction_requests"];
    public const string Guard = """
        -- CANDIDATE ONLY: owner must approve artifact, SQLCMD variable values and disposable lease before execution.
        SET NOCOUNT ON; SET XACT_ABORT ON;
        IF ISNULL(CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')),N'')<>N'$(TrialServer)' OR DB_NAME()<>N'$(TrialDatabase)' OR DB_NAME() NOT LIKE N'HotelOps[_]UC139[_]Trial[_]%'
          OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid=CONVERT(uniqueidentifier,'$(TrialDatabaseGuid)'))
          OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.UC139.Trial' AND CONVERT(nvarchar(128),value)=N'$(LeaseId)')
          THROW 51890,'Disposable trial identity/owner marker required.',1;
        IF SYSUTCDATETIME()<CONVERT(datetime2(7),'$(LeaseStartsUtc)',127) OR SYSUTCDATETIME()>=CONVERT(datetime2(7),'$(LeaseEndsUtc)',127)
          OR DATEDIFF(minute,CONVERT(datetime2(7),'$(LeaseStartsUtc)',127),CONVERT(datetime2(7),'$(LeaseEndsUtc)',127)) NOT BETWEEN 1 AND 480
          THROW 51891,'Fresh bounded trial lease required.',1;
        """;
    public static void Emit(string directory)
    {
        if (Directory.Exists(directory)) throw new InvalidOperationException("New output directory required; no overwrite");
        using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer("Server=offline;Database=offline;Integrated Security=true;Encrypt=true").Options);
        // Generate only the three existing EF WorkTime tables and their indexes; never emit the full database.
        var batches = Regex.Split(db.Database.GenerateCreateScript(), @"(?m)^GO\s*$");
        var ddl = batches.Where(b => Tables.Any(t => Regex.IsMatch(b.TrimStart(), @"^CREATE TABLE (?:\[dbo\]\.)?\[" + t + @"\]\s*\(")
            || Regex.IsMatch(b.TrimStart(), @"^CREATE (?:UNIQUE )?INDEX .* ON (?:\[dbo\]\.)?\[" + t + @"\]"))).ToArray();
        if (ddl.Count(b => b.TrimStart().StartsWith("CREATE TABLE", StringComparison.Ordinal)) != 3) throw new InvalidOperationException("WorkTime DDL coverage");
        var create = string.Join("\n", ddl); // single transaction, no GO inside.
        create = Regex.Replace(create, @"(?<=CREATE TABLE )\[(\w+)\]", "[dbo].[$1]");
        create = Regex.Replace(create, @"(?<=REFERENCES )\[(\w+)\]", "[dbo].[$1]");
        create = Regex.Replace(create, @"(?<= ON )\[(workdays|work_intervals|time_correction_requests)\]", "[dbo].[$1]");
        var verify = VerificationSql(db);
        Directory.CreateDirectory(directory);
        void Write(string name, string text) => File.WriteAllText(Path.Combine(directory, name), text.Replace("\r\n", "\n").TrimEnd('\r', '\n') + "\n", new UTF8Encoding(false));
        Write("01-install-candidate.sql", Guard + "\nBEGIN TRANSACTION;\nIF OBJECT_ID('dbo.workdays') IS NOT NULL OR OBJECT_ID('dbo.work_intervals') IS NOT NULL OR OBJECT_ID('dbo.time_correction_requests') IS NOT NULL THROW 51892,'Existing WorkTime tables: no overwrite.',1;\n" + create +
            "\nEXEC sys.sp_addextendedproperty @name=N'HotelOps.UC139.SchemaCandidate',@value=N'$(LeaseId)';\n" + verify + "\nCOMMIT;");
        Write("02-verify-read-only.sql", Guard + "\n" + verify);
        Write("03-rollback-empty-candidate.sql", Guard + "\nBEGIN TRANSACTION;\nIF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.UC139.SchemaCandidate' AND CONVERT(nvarchar(128),value)=N'$(LeaseId)') THROW 51893,'Not installed by this candidate lease.',1;\nIF EXISTS(SELECT 1 FROM dbo.workdays) OR EXISTS(SELECT 1 FROM dbo.work_intervals) OR EXISTS(SELECT 1 FROM dbo.time_correction_requests) THROW 51894,'Nonempty trial: rollback refused; owner must dispose database separately.',1;\nDROP TABLE dbo.time_correction_requests; DROP TABLE dbo.work_intervals; DROP TABLE dbo.workdays;\nEXEC sys.sp_dropextendedproperty @name=N'HotelOps.UC139.SchemaCandidate';\nCOMMIT;");
        Write("04-fixtures-candidate.sql", Guard + "\n" + FixtureSql);
        Write("05-verify-fixtures-read-only.sql", Guard + "\n" + FixtureVerificationSql + FixtureInstalledSql);
        Write(".gitattributes", "*.sql text eol=lf\npackage.json text eol=lf");
        var names = Directory.GetFiles(directory, "*.sql").Select(Path.GetFileName).Order().ToArray();
        var manifest = new
        {
            contract = "UC139_TRIAL_PACKAGE_V1",
            approval = "CANDIDATE_NOT_APPROVED",
            tables = Tables,
            files = names.Select(n => new { name = n, sha256 = TrialConfiguration.Hash(File.ReadAllBytes(Path.Combine(directory, n!))) }).ToArray()
        };
        Write("package.json", JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
    }
    public static void ValidatePackage(string manifestPath, string manifestHash, string schemaPath, string fixturePath)
    {
        if (!TrialConfiguration.Hash(File.ReadAllBytes(manifestPath)).Equals(manifestHash, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("package receipt hash");
        using var doc = JsonDocument.Parse(File.ReadAllBytes(manifestPath)); var root = doc.RootElement;
        if (root.GetProperty("contract").GetString() != "UC139_TRIAL_PACKAGE_V1") throw new InvalidOperationException("trial package version");
        var directory = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
        var required = new[] { "01-install-candidate.sql", "02-verify-read-only.sql", "03-rollback-empty-candidate.sql", "04-fixtures-candidate.sql", "05-verify-fixtures-read-only.sql" };
        var files = root.GetProperty("files").EnumerateArray().ToArray();
        if (files.Length != required.Length || files.Select(f => f.GetProperty("name").GetString()).Distinct().Count() != required.Length) throw new InvalidOperationException("package closure");
        foreach (var name in required)
        {
            var row = files.Single(f => f.GetProperty("name").GetString() == name);
            if (!TrialConfiguration.Hash(File.ReadAllBytes(Path.Combine(directory, name))).Equals(row.GetProperty("sha256").GetString(), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("package hash");
        }
        if (Path.GetFullPath(schemaPath) != Path.Combine(directory, required[0]) || Path.GetFullPath(fixturePath) != Path.Combine(directory, required[3])) throw new InvalidOperationException("artifact/package binding");
    }
    public static string VerificationSql(HotelOpsDbContext db)
    {
        var sql = new StringBuilder();
        foreach (var type in new[] { typeof(Workday), typeof(WorkInterval), typeof(TimeCorrectionRequest) })
        {
            var e = db.GetService<IDesignTimeModel>().Model.FindEntityType(type)!; var table = e.GetTableName()!;
            sql.AppendLine($"IF OBJECT_ID('dbo.{table}','U') IS NULL THROW 51895,'Missing mapped WorkTime table.',1;");
            sql.AppendLine($"IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.{table}'))<>{e.GetProperties().Count()} THROW 51896,'WorkTime column count mismatch.',1;");
            foreach (var p in e.GetProperties())
            {
                var name = p.GetColumnName(); var columnType = p.GetColumnType()!;
                // COL_LENGTH does not suffice for decimal/date/time/rowversion: compare SQL metadata declaration.
                var expectedType = columnType.Split('(')[0]; if (expectedType == "rowversion") expectedType = "timestamp";
                var size = Regex.Match(columnType, @"\((\d+)\)");
                var extra = p.IsConcurrencyToken ? " AND c.system_type_id=189 AND c.max_length=8" : "";
                if (size.Success) extra += expectedType == "datetime2" ? $" AND c.scale={size.Groups[1].Value}" : $" AND c.max_length={int.Parse(size.Groups[1].Value) * (expectedType == "nvarchar" ? 2 : 1)}";
                if (p.GetCollation() is { } collation) extra += $" AND c.collation_name=N'{collation}'";
                if (p.IsPrimaryKey() && e.FindPrimaryKey()!.Properties.Count == 1 && p.ClrType == typeof(long)) extra += " AND c.is_identity=1";
                sql.AppendLine($"IF NOT EXISTS(SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE c.object_id=OBJECT_ID('dbo.{table}') AND c.name=N'{name}' AND t.name=N'{expectedType}' AND c.is_nullable={(p.IsNullable ? 1 : 0)}{extra}) THROW 51896,'Mapped WorkTime column mismatch.',1;");
            }
            foreach (var fk in e.GetForeignKeys())
            {
                sql.AppendLine($"IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.{table}') AND referenced_object_id=OBJECT_ID('dbo.{fk.PrincipalEntityType.GetTableName()}') AND name=N'{fk.GetConstraintName()}' AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0) THROW 51897,'WorkTime FK missing/untrusted.',1;");
                for (int i = 0; i < fk.Properties.Count; i++) sql.AppendLine($"IF NOT EXISTS(SELECT 1 FROM sys.foreign_key_columns c JOIN sys.foreign_keys f ON c.constraint_object_id=f.object_id WHERE f.parent_object_id=OBJECT_ID('dbo.{table}') AND f.name=N'{fk.GetConstraintName()}' AND c.constraint_column_id={i + 1} AND COL_NAME(c.parent_object_id,c.parent_column_id)=N'{fk.Properties[i].GetColumnName()}' AND COL_NAME(c.referenced_object_id,c.referenced_column_id)=N'{fk.PrincipalKey.Properties[i].GetColumnName()}') THROW 51897,'WorkTime FK column identity mismatch.',1;");
            }
            foreach (var key in e.GetKeys()) sql.AppendLine($"IF NOT EXISTS(SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID('dbo.{table}') AND name=N'{key.GetName()}') THROW 51898,'WorkTime key missing.',1;");
            foreach (var index in e.GetIndexes()) sql.AppendLine($"IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.{table}') AND name=N'{index.GetDatabaseName()}' AND is_unique={(index.IsUnique ? 1 : 0)} AND is_disabled=0{(index.GetFilter() is { } filter ? " AND REPLACE(REPLACE(REPLACE(filter_definition,'(',''),')',''),' ','')=N'" + filter.Replace("'", "''").Replace(" ", "") + "'" : " AND filter_definition IS NULL")}) THROW 51899,'WorkTime serialization index mismatch.',1;");
            foreach (var key in e.GetKeys()) VerifyIndex(key.GetName()!, key.Properties);
            foreach (var index in e.GetIndexes()) VerifyIndex(index.GetDatabaseName()!, index.Properties);
            void VerifyIndex(string name, IReadOnlyList<IProperty> properties)
            {
                sql.AppendLine($"IF (SELECT COUNT(*) FROM sys.index_columns c JOIN sys.indexes i ON i.object_id=c.object_id AND i.index_id=c.index_id WHERE i.object_id=OBJECT_ID('dbo.{table}') AND i.name=N'{name}' AND c.key_ordinal>0)<>{properties.Count} THROW 51899,'WorkTime index key count mismatch.',1;");
                for (int i = 0; i < properties.Count; i++) sql.AppendLine($"IF NOT EXISTS(SELECT 1 FROM sys.index_columns c JOIN sys.indexes x ON x.object_id=c.object_id AND x.index_id=c.index_id WHERE x.object_id=OBJECT_ID('dbo.{table}') AND x.name=N'{name}' AND c.key_ordinal={i + 1} AND c.is_descending_key=0 AND COL_NAME(c.object_id,c.column_id)=N'{properties[i].GetColumnName()}') THROW 51899,'WorkTime index column mismatch.',1;");
            }
        }
        return sql.AppendLine("SELECT 'WORKTIME_SCHEMA_CANDIDATE_VERIFIED' AS result;").ToString();
    }
    public const string FixtureVerificationSql = """
        DECLARE @actor bigint=$(ActorId),@branch bigint=$(BranchId),@denied bigint=$(DeniedBranchId);
        IF @actor<=0 OR @branch<=0 OR @denied<=0 OR @branch=@denied THROW 51900,'Invalid fixture selectors.',1;
        IF NOT EXISTS(SELECT 1 FROM dbo.users u JOIN dbo.departments d ON u.department_id=d.department_id JOIN dbo.branches b ON b.branch_id=u.primary_branch_id WHERE u.user_id=@actor AND u.account_status='ACTIVE' AND u.employment_status='ACTIVE' AND u.deleted_at IS NULL AND u.temporary_password_expires_at IS NULL AND u.password_hash IS NOT NULL AND u.locked_until IS NULL AND u.failed_login_count=0 AND u.primary_branch_id=@branch AND d.branch_id=@branch AND d.is_active=1 AND b.is_active=1) THROW 51901,'Preprovisioned dedicated unrestricted actor/department required.',1;
        IF NOT EXISTS(SELECT 1 FROM dbo.branches WHERE branch_id=@denied AND is_active=1) OR EXISTS(SELECT 1 FROM dbo.user_branch_access WHERE user_id=@actor AND branch_id=@denied AND revoked_at IS NULL) THROW 51902,'Denied branch fixture not isolated.',1;
        IF NOT EXISTS(SELECT 1 FROM dbo.user_branch_access WHERE user_id=@actor AND branch_id=@branch AND revoked_at IS NULL AND granted_at<=SYSUTCDATETIME()) THROW 51903,'Actor branch membership required.',1;
        IF NOT EXISTS(SELECT 1 FROM dbo.user_roles ur JOIN dbo.roles r ON r.role_id=ur.role_id JOIN dbo.role_permissions rp ON rp.role_id=r.role_id JOIN dbo.permissions p ON p.permission_id=rp.permission_id WHERE ur.user_id=@actor AND ur.is_active=1 AND ur.assigned_at<=SYSUTCDATETIME() AND r.is_active=1 AND (ur.expires_at IS NULL OR ur.expires_at>SYSUTCDATETIME()) AND (ur.branch_id IS NULL OR ur.branch_id=@branch) AND p.permission_code='ATTENDANCE_RECORD' AND rp.data_scope IN ('OWN','BRANCH','ALL')) THROW 51904,'Existing attendance grant required; fixture never changes grants.',1;
        DECLARE @today date=CONVERT(date,SYSUTCDATETIME() AT TIME ZONE 'UTC' AT TIME ZONE 'SE Asia Standard Time');
        DECLARE @shifts TABLE(id bigint PRIMARY KEY); INSERT INTO @shifts VALUES($(ShiftId)),($(RaceShiftId)),($(RollbackShiftId));
        IF (SELECT COUNT(*) FROM @shifts)<>3 THROW 51905,'Three distinct approved shifts required.',1;
        IF (SELECT COUNT(*) FROM @shifts f JOIN dbo.work_shifts s ON s.shift_id=f.id JOIN dbo.shift_templates t ON t.shift_template_id=s.shift_template_id JOIN dbo.departments d ON d.department_id=s.department_id JOIN dbo.users creator ON creator.user_id=s.created_by WHERE s.branch_id=@branch AND s.department_id=(SELECT department_id FROM dbo.users WHERE user_id=@actor) AND s.shift_date=@today AND s.status='PLANNED' AND s.scheduled_end_at>s.scheduled_start_at AND t.branch_id=@branch AND t.is_active=1 AND d.branch_id=@branch AND d.is_active=1 AND creator.account_status='ACTIVE' AND creator.deleted_at IS NULL)<>3 THROW 51906,'Approved active today shifts/template/department/creator prerequisites missing.',1;
        IF EXISTS(SELECT 1 FROM @shifts a JOIN dbo.work_shifts sa ON sa.shift_id=a.id JOIN @shifts b ON a.id<b.id JOIN dbo.work_shifts sb ON sb.shift_id=b.id WHERE sa.scheduled_start_at<sb.scheduled_end_at AND sb.scheduled_start_at<sa.scheduled_end_at) THROW 51907,'Overlapping trial shifts refused.',1;
        IF EXISTS(SELECT 1 FROM dbo.workdays WHERE user_id=@actor) OR EXISTS(SELECT 1 FROM dbo.work_intervals WHERE user_id=@actor AND closed_at IS NULL) THROW 51908,'Fresh dedicated actor required; no cleanup.',1;
        """;
    public const string FixtureSql = """
        SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
        BEGIN TRANSACTION;
        SELECT user_id FROM dbo.users WITH(UPDLOCK,HOLDLOCK) WHERE user_id=$(ActorId);
        """ + FixtureVerificationSql + """

        -- No credentials, user/role/grant/membership updates or shared fixture overwrite.
        IF EXISTS(SELECT 1 FROM dbo.shift_assignments WHERE user_id=@actor) THROW 51909,'Actor already has assignment history; no overwrite.',1;
        IF EXISTS(SELECT 1 FROM dbo.user_sessions WHERE user_id=@actor AND revoked_at IS NULL) THROW 51910,'Fresh actor with no live sessions required.',1;
        INSERT dbo.shift_assignments(shift_id,user_id,assignment_status,assigned_by,assigned_at)
        SELECT s.shift_id,@actor,'ASSIGNED',s.created_by,SYSUTCDATETIME() FROM dbo.work_shifts s JOIN @shifts f ON f.id=s.shift_id;
        SELECT shift_assignment_id,shift_id,user_id FROM dbo.shift_assignments WHERE user_id=@actor ORDER BY shift_id;
        COMMIT;
        """;
    public const string FixtureInstalledSql = """

        IF (SELECT COUNT(*) FROM dbo.shift_assignments WHERE user_id=@actor)<>3 OR (SELECT COUNT(*) FROM dbo.shift_assignments a JOIN @shifts f ON f.id=a.shift_id WHERE a.user_id=@actor AND a.assignment_status='ASSIGNED' AND a.assigned_at<=SYSUTCDATETIME())<>3 THROW 51911,'Three installed fresh assignment fixtures required.',1;
        IF EXISTS(SELECT 1 FROM dbo.user_sessions WHERE user_id=@actor AND revoked_at IS NULL) THROW 51912,'Fixture already has live session; no automatic replay.',1;
        """;
    public static string BindFixture(TrialConfiguration r)
    {
        var sql = FixtureVerificationSql + FixtureInstalledSql;
        foreach (var pair in new Dictionary<string, long> { { "ActorId", r.ActorId }, { "BranchId", r.BranchId }, { "DeniedBranchId", r.DeniedBranchId }, { "ShiftId", r.ShiftId }, { "RaceShiftId", r.RaceShiftId }, { "RollbackShiftId", r.RollbackShiftId } })
            sql = sql.Replace("$(" + pair.Key + ")", pair.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
        return sql + $"\nIF NOT EXISTS(SELECT 1 FROM dbo.shift_assignments WHERE shift_assignment_id={r.AssignmentId} AND shift_id={r.ShiftId} AND user_id={r.ActorId}) OR NOT EXISTS(SELECT 1 FROM dbo.shift_assignments WHERE shift_assignment_id={r.RaceAssignmentId} AND shift_id={r.RaceShiftId} AND user_id={r.ActorId}) OR NOT EXISTS(SELECT 1 FROM dbo.shift_assignments WHERE shift_assignment_id={r.RollbackAssignmentId} AND shift_id={r.RollbackShiftId} AND user_id={r.ActorId}) THROW 51913,'Receipt assignment identities mismatch.',1;";
    }
}
