using System.Reflection;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Services;
using Xunit;

namespace HotelOps.Compact.Tests;
public sealed class MissingDirtyProvenanceTests
{
    private static readonly Type Policy=typeof(WorkService).Assembly.GetType("HotelOps.Api.Compact.Services.ReusableMissingCustody")!;
    private static MissingDirtySource[] Trace(ReusableMovement[] movements,Dictionary<long,MissingDirtySource[]>? corrections=null)=>(MissingDirtySource[])Policy.GetMethod("Trace",BindingFlags.Static|BindingFlags.NonPublic)!
        .Invoke(null,[new ReusablePosition[]{new(){PositionId=1,Status="IN_USE"},new(){PositionId=2,Status="DIRTY"},new(){PositionId=3,Status="DIRTY"},new(){PositionId=4,Status="LAUNDRY"},new(){PositionId=5,Status="WRITTEN_OFF"}},movements,corrections??[]])!;
    private static ReusableMovement M(long id,long? source,long destination,decimal quantity,string kind="HANDOVER",long? work=null)=>new(){ReusableMovementId=id,SourcePositionId=source,DestinationPositionId=destination,Quantity=quantity,Kind=kind,WorkId=work};
    [Fact]public void WholeReturnKeepsTheOriginalCleaningSourceAcrossCustody()=>Assert.Equal(new MissingDirtySource(2,3,4),Assert.Single(Trace([M(1,null,1,4),M(2,1,2,4,"REPLACEMENT",10),M(3,2,3,4)])));
    [Fact]public void PartialUnlabelledTransferNeverBorrowsAnotherTasksDirty()=>Assert.Empty(Trace([M(1,null,1,6),M(2,1,2,2,"REPLACEMENT",10),M(3,1,2,4,"REPLACEMENT",11),M(4,2,3,1)]));
    [Fact]public void WashedUnitsCannotBeReportedAsMissingDirty()=>Assert.Empty(Trace([M(1,null,1,2),M(2,1,2,2,"REPLACEMENT",10),M(3,2,4,2,"LAUNDRY")]));
    [Fact]public void ConfirmedLossOnlySubtractsItsExplicitSourceLot()
    {
        var lots=Trace([M(1,null,1,6),M(2,1,2,2,"REPLACEMENT",10),M(3,1,2,4,"REPLACEMENT",11),M(4,2,5,1,"WRITE_OFF",20)],new(){[20]=[new(2,2,1)]});
        Assert.Equal(1,lots.Single(l=>l.MovementId==2).Quantity);Assert.Equal(4,lots.Single(l=>l.MovementId==3).Quantity);
    }
    [Fact]public void ANewTaskAfterAmbiguousTransferGetsItsOwnNewProvenance()
    {
        var lots=Trace([M(1,null,1,6),M(2,1,2,2,"REPLACEMENT",10),M(3,2,3,1),M(4,1,2,4,"REPLACEMENT",11)]);
        Assert.Equal(new MissingDirtySource(4,2,4),Assert.Single(lots));
    }
    [Fact]public void ReservationSplitsAcrossProvenSourcesWithoutRoundingOrOverdraw()
    {
        var method=Policy.GetMethod("Reserve",BindingFlags.Static|BindingFlags.NonPublic)!;
        MissingDirtySource[] available=[new(2,2,1),new(3,3,3)];
        var result=(MissingDirtySource[])method.Invoke(null,[available,2m])!;
        Assert.Equal(new MissingDirtySource[]{new(2,2,1),new(3,3,1)},result);
        foreach(var quantity in new[]{0m,0.5m,5m}){
            var exception=Assert.Throws<TargetInvocationException>(()=>method.Invoke(null,[available,quantity]));
            Assert.IsType<ApiException>(exception.InnerException);
        }
    }
}
