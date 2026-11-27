using HotelOps.Api.Compact.Services;
using Microsoft.Extensions.Configuration;
using Xunit;
namespace HotelOps.Compact.Tests;
public sealed class AdvisoryTests
{
    [Fact]public async Task RemovedIncidentAnalysisRejectsBeforeAccessingDataOrProvider(){
        var service=new AdvisoryService(null!,null!,null!,TimeProvider.System);
        var error=await Assert.ThrowsAsync<HotelOps.Api.Compact.Infrastructure.ApiException>(()=>service.Analyze(new(1,"INCIDENT"),CancellationToken.None));
        Assert.Equal("ADVISORY_KIND_INVALID",error.Code);
    }
    [Fact]public void NoObservationsNeverInventUsage()=>Assert.Equal(0,AdvisoryMath.Forecast(0,30,14));
    [Fact]public void ForecastUsesObservedUseAndExplicitPeriod()=>Assert.Equal(14,AdvisoryMath.Forecast(30,30,14));
    [Fact]public void RepeatingDailyRateDoesNotInflateExactHorizon(){
        Assert.Equal(2m,AdvisoryMath.Forecast(1m,7,14));
        Assert.Equal(0.0002m,AdvisoryMath.Forecast(0.0001m,7,14));
    }
    [Fact]public void FractionalForecastRoundsUpToStockPrecision()=>Assert.Equal(0.3334m,AdvisoryMath.Forecast(1m,3,1));
    [Fact]public void ExpiringStockCannotCoverFutureNeed()=>Assert.Equal(8,AdvisoryMath.Replenishment(10,4,12,10));
    [Fact]public void MinimumStockIsPreserved()=>Assert.Equal(3,AdvisoryMath.Replenishment(2,8,5,0));
    [Fact]public void TransferRespectsSourceDemandAndPendingRequests()=>Assert.Equal(2,AdvisoryMath.Transfer(10,20,6,15,3));
    [Fact]public void TransferNeverBecomesNegative()=>Assert.Equal(0,AdvisoryMath.Transfer(5,2,10,6,0));
    [Fact]public async Task DisabledProviderNeverCreatesHttpClient(){
        var provider=new ConfiguredAdvisoryProvider(new ForbiddenClient(),new ConfigurationBuilder().Build());
        var result=await provider.Explain("DEMAND",new{quantity=1},CancellationToken.None);
        Assert.Equal("BLOCKED_EXTERNAL_CONFIGURATION",result.Status);Assert.Null(result.Text);
    }
    [Fact]public async Task MissingModelNeverUsesAnImplicitModel(){
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"AI:Enabled","true"},{"AI:Provider","OpenAI"},{"AI:ApiKey","synthetic-test-placeholder"}}).Build();
        var result=await new ConfiguredAdvisoryProvider(new ForbiddenClient(),config).Explain("DEMAND",new{},CancellationToken.None);
        Assert.Equal("BLOCKED_EXTERNAL_CONFIGURATION",result.Status);
    }
    private sealed class ForbiddenClient:IHttpClientFactory{public HttpClient CreateClient(string name)=>throw new InvalidOperationException("No external call permitted in this test.");}
    [Theory]
    [InlineData("null")]
    [InlineData("{\"status\":123}")]
    [InlineData("{\"status\":\"completed\",\"output\":[3]}")]
    [InlineData("{\"status\":\"completed\",\"output\":[{\"content\":[3,{\"type\":false}]}]}")]
    public async Task MalformedProviderShapeFallsBack(string json){
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"AI:Enabled","true"},{"AI:Provider","OpenAI"},{"AI:Model","test-model"},{"AI:ApiKey","synthetic-test-placeholder"}}).Build();
        var result=await new ConfiguredAdvisoryProvider(new FakeClients(json),config).Explain("DEMAND",new{},CancellationToken.None);
        Assert.Equal("PROVIDER_INVALID_RESPONSE",result.Status);Assert.Null(result.Text);
    }
    private sealed class FakeClients(string json):IHttpClientFactory{public HttpClient CreateClient(string name)=>new(new FakeHandler(json));}
    private sealed class FakeHandler(string json):HttpMessageHandler{
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK){Content=new StringContent(json)});
    }
}
