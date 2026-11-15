using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Services.InternalRequests;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;
public sealed class InternalRequestHandoverTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    [InlineData("bad\0summary")]
    public void SummaryAndRejectionReasonRequireMeaningfulText(string? text)=>
        Assert.Throws<ValidationException>(()=>InternalRequestHandoverStore.Required(text));

    [Fact]
    public void PersistenceLimitAndMultilineSummaryArePreserved()
    {
        Assert.Equal("Status\nNext step",InternalRequestHandoverStore.Required(" Status\nNext step "));
        Assert.Equal(4000,InternalRequestHandoverStore.Required(new string('a',4000)).Length);
        Assert.Throws<ValidationException>(()=>InternalRequestHandoverStore.Required(new string('a',4001)));
        Assert.Null(InternalRequestHandoverStore.Optional(" \t "));
    }
}
