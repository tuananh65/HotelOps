using HotelOps.Api.Compact.Services;
using Xunit;

namespace HotelOps.Compact.Tests;

public sealed class ExecutionChecklistPolicyTests
{
    [Theory]
    [InlineData("CLEANING",false,false)]
    [InlineData("REWORK",false,false)]
    [InlineData("CLEANING",true,false)]
    [InlineData("REWORK",true,false)]
    [InlineData("MAINTENANCE",false,false)]
    [InlineData("MAINTENANCE",true,false)]
    [InlineData("OPERATIONAL",false,false)]
    public void PhotoReportsDoNotRequireExecutionChecklist(string kind,bool template,bool required)=>
        Assert.Equal(required,ExecutionChecklistPolicy.RequiresSnapshot(kind,template));
}
