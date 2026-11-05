using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.HotelStructure;
using HotelOps.Api.Services.HotelStructure;
using Xunit;
namespace HotelOps.Dev1.RegressionTests;
public sealed class UpdateValidationTests
{
    [Theory, InlineData("","A",null,null), InlineData("N","",null,null), InlineData("N","A","bad",null), InlineData("N","A",null,"bad")]
    public async Task Invalid_update_fields_rejected_before_store(string name,string address,string? phone,string? email)
    {
        var service=new BranchAdministrationService(null!,null!,null!,null!);
        await Assert.ThrowsAsync<ValidationException>(()=>service.UpdateAsync(null!,0,1,new byte[8],new(name,address,phone,email,"valid reason"),default));
    }
}
