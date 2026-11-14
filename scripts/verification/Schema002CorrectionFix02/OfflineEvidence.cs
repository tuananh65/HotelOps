using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HotelOps.Sql09;

// Unit-test-only ephemeral trust root. Never linked into a production acceptance launcher.
internal sealed class OfflineEvidence : IActualEvidenceTrust
{
    private static readonly RSA Key=RSA.Create(2048);
    private static readonly DateTimeOffset Expiry=DateTimeOffset.UtcNow.AddHours(1);
    internal static readonly OfflineEvidence Trust=new();
    internal static readonly string H=new('A',64);
    internal static EvidenceEnvelope Sign<T>(T payload,string purpose)
    {
        var json=JsonSerializer.Serialize(payload);
        var signature=Key.SignData(Encoding.UTF8.GetBytes(purpose+"\n"+json),HashAlgorithmName.SHA256,RSASignaturePadding.Pss);
        return new(json,"OFFLINE_EPHEMERAL_TEST_KEY_NOT_OWNER_ACCEPTED",Convert.ToBase64String(signature));
    }
    public void Require(string purpose,EvidenceEnvelope envelope)
    {
        if(envelope.KeyId!="OFFLINE_EPHEMERAL_TEST_KEY_NOT_OWNER_ACCEPTED"||!Key.VerifyData(Encoding.UTF8.GetBytes(purpose+"\n"+envelope.PayloadJson),Convert.FromBase64String(envelope.SignatureBase64),HashAlgorithmName.SHA256,RSASignaturePadding.Pss))
            throw new InvalidOperationException("OFFLINE_SIGNATURE_OR_PROVENANCE_REJECTED");
    }
    internal static EvidenceEnvelope Fixture(NamedIndexContract c)=>Sign(new OwnershipReceipt("SCHEMA002_FIXTURE_OWNERSHIP_V1","OFFLINE_TEST_ISSUER","OFFLINE_TEST_RECEIPT",c.ProbeId,c.FixtureId,c.TargetTable,c.RowsSha256,c.CaptureSqlSha256,Expiry),"SCHEMA002_FIXTURE_OWNERSHIP_V1");
    internal static NamedIndexContract Contract(string id)
    {
        var c=new NamedIndexContract(id,"dbo.notification_publications",NamedIndexEvidence.ExpectedIndex(id),2601,"OFFLINE_ONLY_FIXTURE",H,H,H);
        return c with{FixtureOwnershipReceiptSha256=ActualEvidence.Hash(Fixture(c).PayloadJson)};
    }
    internal static NamedIndexOutcome Outcome(NamedIndexContract c,string message,int number)
    {
        var actual=Fixture(c);
        var outcome=new NamedIndexOutcome(new(c.ProbeId,c.TargetTable,c.ExpectedIndexName,"ERROR_CAPTURED",number,message,null,23,-1,1),[],true,0,H,H,ActualEvidence.Hash(actual.PayloadJson),actual,null,Guid.NewGuid());
        return WithProvider(outcome,[new(number,message)]);
    }
    internal static NamedIndexOutcome WithProvider(NamedIndexOutcome o,IReadOnlyList<ProviderError> errors)
    {
        var fixture=JsonSerializer.Deserialize<OwnershipReceipt>(o.FixtureReceipt!.PayloadJson)!;
        var receipt=new ProviderCollectionReceipt("SCHEMA002_PROVIDER_ERRORS_V1","OFFLINE_TEST_PROVIDER_OBSERVER","OFFLINE_TEST_PROVIDER_RECEIPT",o.ExecutionId,fixture.ProbeId,fixture.FixtureId,ActualEvidence.Hash(JsonSerializer.Serialize(o.Capture)),"Microsoft.Data.SqlClient.SqlException.Errors",errors);
        return o with{ProviderErrors=errors,ProviderEvidence=Sign(receipt,"SCHEMA002_PROVIDER_ERRORS_V1")};
    }
}
