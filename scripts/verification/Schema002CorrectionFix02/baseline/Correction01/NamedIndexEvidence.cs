using System.Text;
using System.Text.Json;

namespace HotelOps.Sql09;

// Proposed replacement of SQL09's existing validator, not a second validator.
public sealed record ProviderError(int Number,string Message);
public sealed record NamedIndexContract(string ProbeId,string TargetTable,string ExpectedIndexName,int ExpectedErrorNumber,
    string FixtureId,string RowsSha256,string CaptureSqlSha256,string FixtureOwnershipReceiptSha256);
public sealed record NamedIndexCapture(string ProbeId,string TargetTable,string ExpectedIndexName,string ExecutionResult,
    int? ErrorNumber,string? ErrorMessage,string? ErrorProcedure,int? ErrorLine,int XactStateAtCatch,int TransactionsAtCatch);
public sealed record NamedIndexOutcome(NamedIndexCapture Capture,IReadOnlyList<ProviderError> ProviderErrors,
    bool RolledBack,int TransactionsAfter,string BeforeProtectedHash,string AfterProtectedHash,string FixtureOwnershipReceiptSha256);

public static class NamedIndexEvidence
{
    public static string ExpectedIndex(string probeId)=>probeId switch
    {
        "n01-duplicate-event"=>"UX_n01_operation_event_v1",
        "legacy-duplicate-assignment" or "legacy-cross-family-duplicate"=>"UX_fs009_publication_operation",
        _=>throw new InvalidOperationException("UNREVIEWED_DUPLICATE_PROBE")
    };
    public static void RequireContract(NamedIndexContract expected)
    {
        if(expected is null)throw new InvalidOperationException("NAMED_INDEX_CONTRACT_MISSING");
        if(expected.TargetTable!="dbo.notification_publications"||expected.ExpectedIndexName!=ExpectedIndex(expected.ProbeId)||expected.ExpectedErrorNumber!=2601||string.IsNullOrWhiteSpace(expected.FixtureId))
            throw new InvalidOperationException("EXACT_REVIEWED_INDEX_TABLE_ERROR_CONTRACT_REQUIRED");
        foreach(var hash in new[]{expected.RowsSha256,expected.CaptureSqlSha256,expected.FixtureOwnershipReceiptSha256})
            if(!IsHash(hash))throw new InvalidOperationException("HASH_BOUND_FIXTURE_AND_CAPTURE_REQUIRED");
    }
    public static string[] RequireFixture(string probeId,string rowsJson)
    {
        ExpectedIndex(probeId);
        try
        {
            using var doc=JsonDocument.Parse(rowsJson);var rows=doc.RootElement.EnumerateArray().ToArray();
            if(rows.Length!=2)throw new InvalidOperationException("TWO_ISOLATED_DUPLICATE_ROWS_REQUIRED");
            long Id(JsonElement r,string n)=>r.GetProperty(n).GetInt64();
            bool Null(JsonElement r,string n)=>r.GetProperty(n).ValueKind==JsonValueKind.Null;
            var actor=Id(rows[0],"actor_user_id");var op=rows[0].GetProperty("operation_key").GetGuid();
            if(actor<=0||op==Guid.Empty||rows.Any(r=>Id(r,"actor_user_id")!=actor||r.GetProperty("operation_key").GetGuid()!=op||r.GetProperty("schema_version").GetInt32()!=1))throw new InvalidOperationException("SAME_OWNED_ACTOR_OPERATION_REQUIRED");
            var publications=rows.Select(r=>r.GetProperty("publication_id").GetGuid()).ToArray();
            if(publications.Any(p=>p==Guid.Empty)||publications.Distinct().Count()!=2)throw new InvalidOperationException("UNRELATED_PUBLICATION_PK_COLLISION");
            var occurrence=rows.Where(r=>!Null(r,"occurrence_key")).Select(r=>r.GetProperty("occurrence_key").GetString()!).ToArray();
            if(occurrence.Any(k=>string.IsNullOrEmpty(k)||k.Length>120)||occurrence.Distinct(StringComparer.Ordinal).Count()!=occurrence.Length)throw new InvalidOperationException("UNRELATED_OCCURRENCE_COLLISION");
            var types=rows.Select(r=>r.GetProperty("event_type").GetString()).ToArray();
            foreach(var r in rows)
            {
                using var payload=JsonDocument.Parse(r.GetProperty("payload_json").GetString()!);
                var revision=r.GetProperty("revision_hex").GetString();if(revision is not {Length:16}||!revision.All(Uri.IsHexDigit)||payload.RootElement.ValueKind!=JsonValueKind.Object)throw new InvalidOperationException("VALID_SOURCE_PAYLOAD_REVISION_REQUIRED");
            }
            if(probeId=="n01-duplicate-event")
            {
                if(types[0]!=types[1]||types[0] is not ("inspection.failed" or "cleaning.rework.requested")||occurrence.Length!=2||
                   rows.Any(r=>!Null(r,"assignment_id")||Id(r,"inspection_id")<=0||!IsHash(r.GetProperty("payload_hash_hex").GetString()))||
                   rows.Select(r=>Id(r,"inspection_id")).Distinct().Count()!=2)throw new InvalidOperationException("ISOLATED_N01_EVENT_INDEX_SCENARIO_REQUIRED");
                if(types[0]=="inspection.failed"?rows.Any(r=>!Null(r,"rework_id")):rows.Any(r=>Id(r,"rework_id")<=0)||rows.Select(r=>Id(r,"rework_id")).Distinct().Count()!=2)
                    throw new InvalidOperationException("EXACT_DISTINCT_REWORK_BINDING_REQUIRED");
            }
            else
            {
                if(rows.Any(r=>!Null(r,"inspection_id")||!Null(r,"payload_hash_hex")))throw new InvalidOperationException("LEGACY_METADATA_REQUIRED");
                if(probeId=="legacy-duplicate-assignment")
                {
                    if(types.Any(t=>t!="CLEANING_TASK_ASSIGNED")||rows.Any(r=>Id(r,"assignment_id")<=0||!Null(r,"rework_id"))||rows.Select(r=>Id(r,"assignment_id")).Distinct().Count()!=2)
                        throw new InvalidOperationException("UNRELATED_ASSIGNMENT_INDEX_COLLISION");
                }
                else if(!types.Contains("REWORK_SUBMITTED")||types.Count(t=>t is "CLEANING_BLOCKER_REPORTED" or "CLEANING_TASK_COMPLETED")!=1||occurrence.Length!=2||
                    rows.Any(r=>r.GetProperty("event_type").GetString()=="REWORK_SUBMITTED"?(!Null(r,"assignment_id")||Id(r,"rework_id")<=0):(Id(r,"assignment_id")<=0||!Null(r,"rework_id"))))
                    throw new InvalidOperationException("EXACT_LEGACY_CROSS_FAMILY_SCENARIO_REQUIRED");
            }
            return rows.Select(r=>"["+r.GetRawText()+"]").ToArray();
        }
        catch(Exception error)when(error is JsonException or KeyNotFoundException or FormatException or ArgumentException or InvalidOperationException)
        {throw new InvalidOperationException("INVALID_OR_AMBIGUOUS_OWNED_DUPLICATE_FIXTURE",error);}
    }
    public static void Require(NamedIndexContract expected,NamedIndexOutcome observed)
    {
        RequireContract(expected);
        if(observed is null||observed.Capture is null)throw new InvalidOperationException("SQL_CAPTURE_MISSING");
        var c=observed.Capture;
        if(c.ProbeId!=expected.ProbeId||c.TargetTable!=expected.TargetTable||c.ExpectedIndexName!=expected.ExpectedIndexName||
           c.ExecutionResult!="ERROR_CAPTURED"||c.ErrorNumber!=expected.ExpectedErrorNumber||string.IsNullOrWhiteSpace(c.ErrorMessage)||
           c.ErrorProcedure is not null||c.ErrorLine is null or <=0||c.TransactionsAtCatch!=1||c.XactStateAtCatch is not (-1 or 1))
            throw new InvalidOperationException("COMPLETE_EXPECTED_SQL_CAPTURE_REQUIRED");
        var root=RequireOneDuplicate(observed.ProviderErrors);
        if(root.Number!=c.ErrorNumber||root.Message!=c.ErrorMessage||!TryNative(root.Number,root.Message,out var table,out var index)||
           table!=expected.TargetTable||index!=expected.ExpectedIndexName)
            throw new InvalidOperationException("INTENDED_UNIQUE_INDEX_TABLE_NOT_PROVEN");
        if(!observed.RolledBack||observed.TransactionsAfter!=0||!IsHash(observed.BeforeProtectedHash)||!IsHash(observed.AfterProtectedHash)||
           !StringComparer.OrdinalIgnoreCase.Equals(observed.BeforeProtectedHash,observed.AfterProtectedHash)||
           observed.FixtureOwnershipReceiptSha256!=expected.FixtureOwnershipReceiptSha256)
            throw new InvalidOperationException("OWNED_FIXTURE_ROLLBACK_PRESERVATION_NOT_PROVEN");
    }
    // Retain existing SQL09 signature and its known valid index-only test messages.
    // This necessary legacy check is NEVER sufficient for the corrected SQL probes.
    public static void Require(string expectedIndex,IReadOnlyCollection<ProviderError> errors)
    {
        if(expectedIndex is not ("UX_n01_operation_event_v1" or "UX_fs009_publication_operation"))throw new InvalidOperationException("UNREVIEWED_DUPLICATE_INDEX_ASSERTION");
        var root=RequireOneDuplicate(errors);string? actual=null;
        if(TryNative(root.Number,root.Message,out _,out var index))actual=index;
        else if(root.Number==2601)
        {
            var text=root.Message.Trim();var pos=0;
            if(Take(text,ref pos,"duplicate unique index ")&&Name(text,ref pos,out var name)&&pos==text.Length&&Normalize(name,1,out var normalized))actual=normalized;
        }
        if(actual!=expectedIndex)throw new InvalidOperationException("INTENDED_UNIQUE_INDEX_NOT_PROVEN");
    }
    private static ProviderError RequireOneDuplicate(IReadOnlyCollection<ProviderError> errors)
    {
        if(errors is null||errors.Count==0||errors.Any(e=>e is null||e.Number==1205||string.IsNullOrWhiteSpace(e.Message)))throw new InvalidOperationException("INTENDED_UNIQUE_INDEX_NOT_PROVEN");
        var roots=errors.Where(e=>e.Number is 2601 or 2627).ToArray();
        if(roots.Length!=1||errors.Any(e=>e!=roots[0]&&(e.Number!=3621||e.Message.Trim()!="The statement has been terminated.")))
            throw new InvalidOperationException("INTENDED_UNIQUE_INDEX_NOT_PROVEN");
        return roots[0];
    }
    private static bool TryNative(int number,string message,out string table,out string index)
    {
        table=index="";var s=message.Trim();var p=0;string rawTable,rawIndex;
        if(number==2601)
        {
            if(!Take(s,ref p,"Cannot insert duplicate key row in object ")||!Name(s,ref p,out rawTable)||
               !Take(s,ref p," with unique index ")||!Name(s,ref p,out rawIndex))return false;
        }
        else if(number==2627)
        {
            if(!Take(s,ref p,"Violation of UNIQUE KEY constraint ")||!Name(s,ref p,out rawIndex)||
               !Take(s,ref p,". Cannot insert duplicate key in object ")||!Name(s,ref p,out rawTable))return false;
        }
        else return false;
        // Values are opaque. Names occurring in key values can never satisfy identity.
        if(!Take(s,ref p,". The duplicate key value is (")||!s.EndsWith(").",StringComparison.Ordinal)||p>=s.Length-2)return false;
        return Normalize(rawTable,2,out table)&&Normalize(rawIndex,1,out index);
    }
    private static bool Take(string s,ref int p,string token)
    {if(!s.AsSpan(p).StartsWith(token,StringComparison.Ordinal))return false;p+=token.Length;return true;}
    private static bool Name(string s,ref int p,out string value)
    {
        value="";if(p>=s.Length)return false;
        if(s[p]=='\'')
        {
            p++;var b=new StringBuilder();while(p<s.Length){var ch=s[p++];if(ch!='\''){b.Append(ch);continue;}if(p<s.Length&&s[p]=='\''){b.Append('\'');p++;continue;}value=b.ToString();return value.Length>0;}return false;
        }
        // Native values may themselves contain bracket/double-quoted identifiers.
        var start=p;if(!Identifier(s,ref p,out _))return false;
        while(p<s.Length&&s[p]=='.'&&p+1<s.Length&&(s[p+1]=='['||s[p+1]=='"')){p++;if(!Identifier(s,ref p,out _))return false;}
        value=s[start..p];return true;
    }
    private static bool Normalize(string value,int count,out string normalized)
    {
        normalized="";var parts=new List<string>();var p=0;
        while(p<value.Length){if(!Identifier(value,ref p,out var part))return false;parts.Add(part);if(p==value.Length)break;if(value[p++]!='.'||p==value.Length)return false;}
        if(parts.Count!=count)return false;normalized=string.Join('.',parts);return true;
    }
    private static bool Identifier(string s,ref int p,out string value)
    {
        value="";if(p>=s.Length)return false;var b=new StringBuilder();var open=s[p];
        if(open is '[' or '"')
        {
            var close=open=='['?']':'"';p++;
            while(p<s.Length){var c=s[p++];if(c!=close){b.Append(c);continue;}if(p<s.Length&&s[p]==close){b.Append(close);p++;continue;}value=b.ToString();return value.Length>0;}return false;
        }
        while(p<s.Length&&(char.IsAsciiLetterOrDigit(s[p])||s[p]=='_'))b.Append(s[p++]);value=b.ToString();return value.Length>0;
    }
    private static bool IsHash(string? value)=>value is {Length:64}&&value.All(Uri.IsHexDigit);
}
