using HotelOps.Api.Data;
using HotelOps.Api.Services.Housekeeping;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class InspectionRoomProjectionTests
{
    private static InspectionProjectionFacts Passed => new("PASSED","COMPLETED","PASS","COMPLETED","COMPLETED","INSPECTION",
        true,true,true,false,false,true,false);
    private static InspectionProjectionFacts Failed => Passed with{InspectionState="FAILED",Result="FAIL",CleaningState="REWORK_REQUIRED",
        CleaningStatus="IN_PROGRESS",RequiredCriteriaPassed=false,HasFailedCriterion=true};

    [Fact]
    public void Valid_passed_current_cycle_with_receipt_and_required_results_projects_READY()
    {
        var result=InspectionRoomProjectionRules.Evaluate(Passed);
        Assert.Equal("READY",result.OperationalStatus);Assert.Equal("READY",result.EffectiveReadiness);
    }

    [Theory]
    [InlineData("INSPECTION")][InlineData("OUT_OF_SERVICE")][InlineData("DIRTY")]
    public void Active_block_has_precedence_over_both_approve_and_reject(string status)
    {
        foreach(var source in new[]{Passed,Failed})
        {
            var projection=InspectionRoomProjectionRules.Evaluate(source with{EffectiveBlock=true,OperationalStatus=status});
            Assert.Equal(status,projection.OperationalStatus);Assert.Equal("BLOCKED",projection.EffectiveReadiness);
        }
    }

    [Fact]
    public void Reject_uses_readiness_overlay_and_preserves_existing_CK_compatible_operational_status()
    {
        var projection=InspectionRoomProjectionRules.Evaluate(Failed);
        Assert.Equal("INSPECTION",projection.OperationalStatus);Assert.Equal("REWORK_REQUIRED",projection.Readiness);
    }

    [Theory]
    [InlineData("receipt")][InlineData("cycle")][InlineData("inactive")][InlineData("mandatory")][InlineData("rework")]
    [InlineData("occupied")][InlineData("out-of-service")][InlineData("status-only")][InlineData("inspection-only")][InlineData("pending")]
    public void Status_change_alone_or_incomplete_authority_cannot_create_READY(string mutation)
    {
        var s=mutation switch
        {
            "receipt"=>Passed with{TrustedConclusionReceipt=false},"cycle"=>Passed with{CurrentCycle=false},
            "inactive"=>Passed with{ActiveRoom=false},"mandatory"=>Passed with{RequiredCriteriaPassed=false},
            "rework"=>Passed with{HasOpenRework=true},"occupied"=>Passed with{OperationalStatus="OCCUPIED"},
            "out-of-service"=>Passed with{OperationalStatus="OUT_OF_SERVICE"},"status-only"=>Passed with{Result=null},
            "inspection-only"=>Passed with{CleaningState="PENDING_INSPECTION"},_=>Passed with{InspectionState="PENDING_CONCLUSION"}
        };
        Assert.Throws<InvalidOperationException>(()=>InspectionRoomProjectionRules.Evaluate(s));
    }

    [Fact]
    public async Task Adapter_rejects_independent_or_missing_transaction_before_any_SQL()
    {
        using var db=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer().Options);
        var adapter=new InspectionRoomProjectionAdapter(db);
        var failure=await Assert.ThrowsAsync<InvalidOperationException>(()=>adapter.ApplyAsync(db.Database.GetDbConnection(),null!,1,1,1,new byte[8],Guid.NewGuid(),default));
        Assert.Equal("INSPECTION_PROJECTION_CALLER_SERIALIZABLE_TRANSACTION_REQUIRED",failure.Message);
    }
}
