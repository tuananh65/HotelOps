using HotelOps.Api.Security;
using HotelOps.Api.Services.Assets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController,InsightErrors,Authorize(AuthenticationSchemes=HotelOpsCookieTicket.Scheme),AutoValidateAntiforgeryToken]
[Route("api/asset-custody/{assetId:long}")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class AssetCustodyController(AssetCustodyWorkflow service,IConfiguration configuration,IWebHostEnvironment environment):ControllerBase
{
    [HttpGet]
    public async Task<object> Read(long assetId,CancellationToken ct,int page=1)
    {
        try{return await service.ReadAsync(assetId,ct,page);}
        catch(System.ComponentModel.DataAnnotations.ValidationException e){throw new HotelOps.Api.Services.Insights.InsightException(503,"PHOTO_POLICY_UNAVAILABLE",e.Message);}
    }
    [HttpPost,RequestSizeLimit(72*1024*1024)]
    public async Task<object> Submit(long assetId,AssetCustodySubmitInput input,CancellationToken ct)
    {
        try{return await service.SubmitAsync(assetId,Version(),input,configuration["Evidence:PrivateRoot"]??Path.Combine(environment.ContentRootPath,"App_Data","evidence"),ct);}
        catch(System.ComponentModel.DataAnnotations.ValidationException e){throw new HotelOps.Api.Services.Insights.InsightException(400,"MSG23",e.Message);}
    }
    public sealed record DecisionInput(string? Reason);
    [HttpPost("{handoverId:long}/acknowledge")]
    public Task<object> Accept(long assetId,long handoverId,CancellationToken ct)=>service.DecideAsync(assetId,handoverId,Version(),true,null,ct);
    [HttpPost("{handoverId:long}/reject")]
    public Task<object> Reject(long assetId,long handoverId,DecisionInput input,CancellationToken ct)=>service.DecideAsync(assetId,handoverId,Version(),false,input.Reason,ct);
    private string? Version()=>Request.Headers.IfMatch.ToString() is{Length:>0} value?value:null;
}
