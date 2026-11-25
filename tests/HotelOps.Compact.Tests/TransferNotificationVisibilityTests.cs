using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Services;
using Xunit;

namespace HotelOps.Compact.Tests;

public sealed class TransferNotificationVisibilityTests
{
    [Theory]
    [InlineData(10,10,"OPERATION_MANAGER",true)]
    [InlineData(20,20,"OPERATION_MANAGER",true)]
    [InlineData(10,10,"STOREKEEPER",true)]
    [InlineData(20,20,"STOREKEEPER",true)]
    [InlineData(10,20,"STOREKEEPER",false)]
    [InlineData(20,10,"OPERATION_MANAGER",false)]
    [InlineData(30,30,"STOREKEEPER",false)]
    [InlineData(10,10,"ADMIN",false)]
    public async Task PermissionAndSupportedRoleMustMatchOneTransferParticipant(long permissionBranch,long roleBranch,string role,bool expected)
    {
        var d=new InventoryDocument{Kind="TRANSFER_REQUEST",BranchId=10,DestinationBranchId=20};
        var result=await DocumentService.CanReadTransfer(null!,null!,d,CancellationToken.None,
            (code,branch,ct)=>Task.FromResult(code=="INVENTORY_VIEW"&&branch==permissionBranch),
            (code,branch,ct)=>Task.FromResult(code==role&&branch==roleBranch));
        Assert.Equal(expected,result);
    }

    [Theory]
    [InlineData("TRANSFER",true)]
    [InlineData("TRANSFER_REQUEST",false)]
    [InlineData("RECEIPT",false)]
    [InlineData(null,false)]
    public async Task ReceiptUsesOnlyTransferSourceParticipants(string? sourceKind,bool expected)
    {
        using var cancellation=new CancellationTokenSource();
        var receipt=new InventoryDocument{Kind="TRANSFER_RECEIPT",BranchId=20,SourceDocumentId=42};
        var lookups=0;
        var result=await DocumentService.CanReadTransfer(null!,null!,receipt,cancellation.Token,
            (code,branch,ct)=>{Assert.Equal(cancellation.Token,ct);return Task.FromResult(branch==10);},
            (code,branch,ct)=>Task.FromResult(code=="STOREKEEPER"&&branch==10),
            (id,ct)=>{Assert.Equal(42,id);Assert.Equal(cancellation.Token,ct);lookups++;
                return Task.FromResult(sourceKind==null?null:new InventoryDocument{Kind=sourceKind,BranchId=10,DestinationBranchId=20});});
        Assert.Equal(expected,result);
        Assert.Equal(1,lookups);
    }

    [Fact]
    public async Task UnrelatedDocumentCannotUseTransferException()
    {
        var calls=0;
        var result=await DocumentService.CanReadTransfer(null!,null!,new InventoryDocument{Kind="RECEIPT",BranchId=10},CancellationToken.None,
            (_,_,_)=>{calls++;return Task.FromResult(true);},(_,_,_)=>{calls++;return Task.FromResult(true);});
        Assert.False(result);
        Assert.Equal(0,calls);
    }
}
