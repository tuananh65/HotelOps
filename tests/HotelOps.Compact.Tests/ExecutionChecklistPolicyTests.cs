using HotelOps.Api.Compact.Services;
using Xunit;

namespace HotelOps.Compact.Tests;

public sealed class ExecutionChecklistPolicyTests
{
    [Theory]
    [InlineData("CLEANING",false,false)]
    [InlineData("REWORK",false,false)]
    [InlineData("CLEANING",true,true)]
    [InlineData("REWORK",true,true)]
    [InlineData("MAINTENANCE",false,true)]
    [InlineData("MAINTENANCE",true,true)]
    [InlineData("OPERATIONAL",false,false)]
    public void MissingCleaningChecklistIsOptionalButAttachedAndMaintenanceRemainRequired(string kind,bool template,bool required)=>
        Assert.Equal(required,ExecutionChecklistPolicy.RequiresSnapshot(kind,template));
}
