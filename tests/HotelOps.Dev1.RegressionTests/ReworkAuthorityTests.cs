using System.Security.Cryptography;
using HotelOps.Api.Data;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.Housekeeping;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class ReworkAuthorityTests
{
    private static byte[] Hash(string value) => SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value));
    private static ReworkAuthorityBinding Binding => new(1,2,3,4,5,6,[0,0,0,0,0,0,0,1]);
    private static ReworkAuthorityProof Proof => new(Binding,ReworkAuthorityContract.Origin,1,ReworkAuthorityContract.Issuer,1,
        7,Guid.Parse("11111111-1111-4111-8111-111111111111"),10,Hash("source"),Hash("evidence"));
    private static ReworkAuthorityObservation Observed => new(Binding,3,true,true,true,true,true,20,Hash("source"),Hash("evidence"));

    [Fact]
    public void Valid_trusted_committed_binding_is_accepted_by_pure_contract_only()
        => Assert.Null(ReworkAuthorityContract.Validate(Proof,Observed));

    [Theory]
    [InlineData("synthetic")][InlineData("origin")][InlineData("version")][InlineData("issuer")][InlineData("issuer-version")]
    [InlineData("actor")][InlineData("operation")][InlineData("uncommitted")][InlineData("missing-transaction")]
    [InlineData("rework")][InlineData("inspection")][InlineData("task")][InlineData("room")][InlineData("branch")][InlineData("checklist-version")]
    [InlineData("revision")][InlineData("source-digest")][InlineData("evidence-digest")][InlineData("malformed-digest")]
    public void Forged_or_stale_proof_is_rejected(string mutation)
    {
        var p=Proof;
        p=mutation switch
        {
            "synthetic"=>p with{Origin="SYNTHETIC_TEST_ONLY"},"origin"=>p with{Origin="FAILED"},"version"=>p with{ContractVersion=2},
            "issuer"=>p with{Issuer="CLIENT"},"issuer-version"=>p with{IssuerVersion=2},"actor"=>p with{ActorUserId=0},
            "operation"=>p with{OperationId=Guid.Empty},"uncommitted"=>p with{WriterTransactionId=20},"missing-transaction"=>p with{WriterTransactionId=0},
            "rework"=>p with{Binding=p.Binding with{ReworkId=99}},"inspection"=>p with{Binding=p.Binding with{InspectionId=99}},
            "task"=>p with{Binding=p.Binding with{CleaningTaskId=99}},"room"=>p with{Binding=p.Binding with{RoomId=99}},
            "branch"=>p with{Binding=p.Binding with{BranchId=99}},"checklist-version"=>p with{Binding=p.Binding with{VersionId=99}},
            "revision"=>p with{Binding=p.Binding with{InspectionRevision=[0,0,0,0,0,0,0,2]}},
            "source-digest"=>p with{SourceDigest=Hash("changed")},"evidence-digest"=>p with{EvidenceDigest=Hash("changed")},
            _=>p with{SourceDigest=new byte[31]}
        };
        Assert.NotNull(ReworkAuthorityContract.Validate(p,Observed));
    }

    [Theory]
    [InlineData("issuer-disabled")][InlineData("receipt-forged")][InlineData("audit-missing")][InlineData("not-failed")]
    [InlineData("cycle")][InlineData("superseded")][InlineData("read-revoked")][InlineData("source-mutated")][InlineData("evidence-mutated")]
    public void Source_or_authorization_change_invalidates_the_existing_proof(string mutation)
    {
        var s=Observed;
        s=mutation switch
        {
            "issuer-disabled"=>s with{TrustedIssuer=false},"receipt-forged"=>s with{ReceiptMatches=false},
            "audit-missing"=>s with{AuditMatches=false},"not-failed" or "superseded"=>s with{CurrentFailedSource=false},
            "cycle"=>s with{CurrentCycleId=99},"read-revoked"=>s with{AuthorizedWorker=false},
            "source-mutated"=>s with{SourceDigest=Hash("different selection")},_=>s with{EvidenceDigest=Hash("mutated bytes")}
        };
        Assert.NotNull(ReworkAuthorityContract.Validate(Proof,s));
    }

    private static ReworkAuthorityEvidence Evidence(long id,string kind="DEFECT",long source=10)
        => new(kind,source,id,"inspection/file.png","file.png","image/png",10,7,
            new DateTime(2026,11,9,0,0,0,DateTimeKind.Utc),Hash("file-content"));

    [Fact]
    public void Evidence_digest_is_order_independent_and_binds_edges_metadata_and_file_bytes()
    {
        var a=Evidence(1);var b=Evidence(2,"RESULT",11);
        var original=ReworkAuthorityContract.EvidenceDigest([a,b]);
        Assert.Equal(original,ReworkAuthorityContract.EvidenceDigest([b,a]));
        foreach(var changed in new[]{a with{SourceId=12},a with{Kind="RESULT"},a with{StorageKey="other.png"},
            a with{FileName="changed.png"},a with{ContentDigest=Hash("changed bytes")},a with{UploadedBy=8}})
            Assert.NotEqual(original,ReworkAuthorityContract.EvidenceDigest([changed,b]));
        Assert.NotEqual(original,ReworkAuthorityContract.EvidenceDigest([a]));
        Assert.Throws<ArgumentException>(()=>ReworkAuthorityContract.EvidenceDigest([a,a]));
    }

    [Fact]
    public void Selected_defect_or_result_mutation_changes_the_versioned_source_digest()
    {
        var evidence=ReworkAuthorityContract.EvidenceDigest([]);var legacy=Hash("legacy source");
        var original=ReworkAuthorityContract.SourceDigest("[{\"defect_id\":1,\"outcome\":\"FAIL\"}]",legacy,evidence);
        Assert.NotEqual(original,ReworkAuthorityContract.SourceDigest("[{\"defect_id\":2,\"outcome\":\"FAIL\"}]",legacy,evidence));
        Assert.NotEqual(original,ReworkAuthorityContract.SourceDigest("[{\"defect_id\":1,\"outcome\":\"PASS\"}]",legacy,evidence));
        Assert.NotEqual(original,ReworkAuthorityContract.SourceDigest("[{\"defect_id\":1,\"outcome\":\"FAIL\"}]",Hash("new revision"),evidence));
    }

    [Fact]
    public async Task Snapshot_reader_rejects_missing_caller_transaction_before_SQL()
    {
        using var db=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer().Options);
        var service=new RoomTypeCreationService(db,null!,null!,null!,TimeProvider.System);
        var failure=await Assert.ThrowsAsync<InvalidOperationException>(()=>service.BuildReworkAuthoritySnapshotAsync(db.Database.GetDbConnection(),null!,1,null,default));
        Assert.Equal("REWORK_AUTHORITY_CALLER_SERIALIZABLE_TRANSACTION_REQUIRED",failure.Message);
    }
}
