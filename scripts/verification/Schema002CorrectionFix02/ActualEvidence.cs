using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HotelOps.Sql09;

// Raw independently supplied bytes plus provenance; expected hashes are never actual evidence.
public sealed record EvidenceEnvelope(string PayloadJson,string KeyId,string SignatureBase64);
public sealed record OwnershipReceipt(string Contract,string Issuer,string ReceiptId,string ProbeId,string FixtureId,
    string TargetTable,string RowsSha256,string CaptureSqlSha256,DateTimeOffset ExpiresUtc);
public sealed record ProviderCollectionReceipt(string Contract,string Issuer,string ReceiptId,Guid ExecutionId,
    string ProbeId,string FixtureId,string CaptureSha256,string Source,IReadOnlyList<ProviderError> Errors);
public sealed record FixtureReceiptVerification(string ExpectedSha256,string? ActualSha256,string Status,
    string? KeyId,string? VerifiedIssuer,string? VerifiedReceiptId);

// Must authenticate accepted issuer/key, purpose, owner/lane/lease and immutable provenance.
// No accepted production implementation or signing key is shipped. Caller records are not authorization.
public interface IActualEvidenceTrust
{
    void Require(string purpose,EvidenceEnvelope envelope);
}
public interface INamedIndexActualEvidenceSource
{
    IActualEvidenceTrust Trust {get;}
    EvidenceEnvelope? FixtureOwnership {get;}
    // Must originate in an independent provider observer for THIS execution, not SQL capture conversion.
    Task<EvidenceEnvelope?> ReadProviderErrorsAsync(Guid executionId,CancellationToken ct);
}

public static class ActualEvidence
{
    public static string Hash(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static FixtureReceiptVerification RequireFixture(NamedIndexContract expected,EvidenceEnvelope? actual,IActualEvidenceTrust? trust)
    {
        var actualHash=actual?.PayloadJson is null?null:Hash(actual.PayloadJson);
        try
        {
            var r=ValidateFixture(expected,actual,trust);
            return new(expected.FixtureOwnershipReceiptSha256,actualHash,"VERIFIED",actual!.KeyId,r.Issuer,r.ReceiptId);
        }
        catch(Exception error)
        {
            error.Data["FixtureReceiptVerificationJson"]=JsonSerializer.Serialize(new FixtureReceiptVerification(
                expected.FixtureOwnershipReceiptSha256,actualHash,"NOT_VERIFIED",actual?.KeyId,null,null));
            throw;
        }
    }
    private static OwnershipReceipt ValidateFixture(NamedIndexContract expected,EvidenceEnvelope? actual,IActualEvidenceTrust? trust)
    {
        if(actual is null||trust is null)throw new InvalidOperationException("FIXTURE_OWNERSHIP_NOT_VERIFIED: actual trusted receipt missing");
        ValidateEnvelope(actual);
        if(!Hash(actual.PayloadJson).Equals(expected.FixtureOwnershipReceiptSha256,StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("ACTUAL_FIXTURE_RECEIPT_HASH_MISMATCH");
        trust.Require("SCHEMA002_FIXTURE_OWNERSHIP_V1",actual);
        var r=JsonSerializer.Deserialize<OwnershipReceipt>(actual.PayloadJson)??throw new InvalidOperationException("FIXTURE_RECEIPT_MISSING");
        if(r.Contract!="SCHEMA002_FIXTURE_OWNERSHIP_V1"||string.IsNullOrWhiteSpace(r.Issuer)||string.IsNullOrWhiteSpace(r.ReceiptId)||
            r.ProbeId!=expected.ProbeId||r.FixtureId!=expected.FixtureId||r.TargetTable!=expected.TargetTable||
            !string.Equals(r.RowsSha256,expected.RowsSha256,StringComparison.OrdinalIgnoreCase)||
            !string.Equals(r.CaptureSqlSha256,expected.CaptureSqlSha256,StringComparison.OrdinalIgnoreCase)||r.ExpiresUtc<=DateTimeOffset.UtcNow)
            throw new InvalidOperationException("TRUSTED_FIXTURE_RECEIPT_BINDING_INVALID");
        return r;
    }
    public static IReadOnlyList<ProviderError> RequireProvider(NamedIndexContract expected,NamedIndexOutcome outcome,IActualEvidenceTrust? trust)
    {
        var envelope=outcome.ProviderEvidence;
        if(envelope is null||trust is null)throw new InvalidOperationException("PROVIDER_EVIDENCE_NOT_AVAILABLE: SQL TRY/CATCH is not provider evidence");
        ValidateEnvelope(envelope);trust.Require("SCHEMA002_PROVIDER_ERRORS_V1",envelope);
        var r=JsonSerializer.Deserialize<ProviderCollectionReceipt>(envelope.PayloadJson)??throw new InvalidOperationException("PROVIDER_COLLECTION_MISSING");
        if(r.Contract!="SCHEMA002_PROVIDER_ERRORS_V1"||string.IsNullOrWhiteSpace(r.Issuer)||string.IsNullOrWhiteSpace(r.ReceiptId)||
            r.ExecutionId==Guid.Empty||r.ExecutionId!=outcome.ExecutionId||r.ProbeId!=expected.ProbeId||r.FixtureId!=expected.FixtureId||
            r.CaptureSha256!=Hash(JsonSerializer.Serialize(outcome.Capture))||r.Source!="Microsoft.Data.SqlClient.SqlException.Errors"||
            r.Errors is null||outcome.ProviderErrors is null||!r.Errors.SequenceEqual(outcome.ProviderErrors))
            throw new InvalidOperationException("INDEPENDENT_PROVIDER_COLLECTION_BINDING_INVALID");
        return r.Errors;
    }
    private static void ValidateEnvelope(EvidenceEnvelope e)
    {
        if(string.IsNullOrWhiteSpace(e.PayloadJson)||string.IsNullOrWhiteSpace(e.KeyId)||string.IsNullOrWhiteSpace(e.SignatureBase64))
            throw new InvalidOperationException("ACTUAL_EVIDENCE_PROVENANCE_MISSING");
    }
}
