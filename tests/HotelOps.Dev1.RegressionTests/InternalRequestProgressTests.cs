using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.InternalRequests;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class InternalRequestProgressTests
{
    [Fact]
    public void EvidenceRequiresMatchingTypeExtensionAndBoundedMetadata()
    {
        var png=Convert.ToBase64String(new byte[]{137,80,78,71,13,10,26,10});
        Assert.Equal(".png",InternalRequestEvidence.Validate(new("proof.png","image/png",png)).Extension);
        foreach(var file in new HotelOps.Api.DTOs.Housekeeping.EvidenceUpload[]{new("../proof.png","image/png",png),new("proof.pdf","image/png",png),
            new("proof.png","application/pdf",png),new("proof.png","image/png","bad"),new("proof.png","image/png",Convert.ToBase64String(new byte[]{1,2,3}))})
            Assert.Throws<System.ComponentModel.DataAnnotations.ValidationException>(()=>InternalRequestEvidence.Validate(file));
        Assert.Throws<System.ComponentModel.DataAnnotations.ValidationException>(()=>InternalRequestProgressService.Validate(new(50,null,null,null,null,Enumerable.Repeat(new HotelOps.Api.DTOs.Housekeeping.EvidenceUpload("proof.png","image/png",png),6).ToArray())));
    }
    [Fact]
    public void MissingPercentCannotSilentlyResetProgressToZero() =>
        Assert.Throws<System.Text.Json.JsonException>(() => System.Text.Json.JsonSerializer.Deserialize<InternalRequestProgressInput>("{}"));
    [Fact]
    public void ProgressBoundsAndPersistenceTextLimitsAreEnforced()
    {
        foreach(var percent in new[]{-1,101})
            Assert.Throws<System.ComponentModel.DataAnnotations.ValidationException>(() => InternalRequestProgressService.Validate(new(percent,null,null,null,null)));
        Assert.Equal(100,InternalRequestProgressService.Validate(new(100," note ",null,null,null)).Percent);
        Assert.Equal("note",InternalRequestProgressService.Validate(new(0," note ",null,null,null)).Note);
        Assert.Null(InternalRequestProgressService.Validate(new(0," \t ",null,null,null)).Note);
        Assert.Equal(4000,InternalRequestProgressService.Validate(new(0,null,new string('a',4000),null,null)).Result!.Length);
        foreach(var text in new[]{new string('a',4001),"bad\0text"})
            Assert.Throws<System.ComponentModel.DataAnnotations.ValidationException>(() => InternalRequestProgressService.Validate(new(0,null,text,null,null)));
    }
    private static readonly DateTime Now = new(2026,11,16,0,0,0,DateTimeKind.Utc);
    private static PermissionGrantPath Path() => new("IR_PROCESS","OWN",7,true,true,Now.AddDays(-1),null,true,true);

    [Theory]
    [InlineData("HK_SUPERVISOR")]
    [InlineData("HK_STAFF")]
    [InlineData("MAINTENANCE_LEAD")]
    [InlineData("MAINTENANCE_STAFF")]
    [InlineData("STOREKEEPER")]
    [InlineData("DEPT_MANAGER")]
    public void HandlerRolesRequireCurrentScopedProcessGrant(string role)
    {
        Assert.True(InternalRequestProgressAuthority.EligibleHandler(role,Path(),7,Now));
        Assert.True(InternalRequestProgressAuthority.EligibleHandler(role,Path() with {DataScope="BRANCH"},7,Now));
        foreach(var path in new[]{Path() with {PermissionCode="IR_VIEW"},Path() with {BranchId=8},
            Path() with {DataScope="ALL",BranchId=null},Path() with {AssignmentActive=false},
            Path() with {RoleActive=false},Path() with {BranchActive=false},Path() with {BranchAccessActive=false},
            Path() with {ExpiresAt=Now},Path() with {AssignedAt=Now.AddTicks(1)}})
            Assert.False(InternalRequestProgressAuthority.EligibleHandler(role,path,7,Now));
    }

    [Theory]
    [InlineData("SYS_ADMIN")]
    [InlineData("OWNER")]
    [InlineData("OPERATIONS_MANAGER")]
    [InlineData("STAFF")]
    public void UnlistedActorsDoNotInheritProgressFromPermissionAlone(string role) =>
        Assert.False(InternalRequestProgressAuthority.EligibleHandler(role,Path(),7,Now));
}
