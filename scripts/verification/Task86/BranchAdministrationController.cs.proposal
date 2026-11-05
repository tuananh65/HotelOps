using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.HotelStructure;
using HotelOps.Api.Security;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes=HotelOpsCookieTicket.Scheme)]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
[UserAccountActivationErrors]
[RequestSizeLimit(32*1024)]
public sealed class BranchAdministrationController(BranchAdministrationService service):ControllerBase
{
    [HttpPost("api/branches")]
    public Task<IActionResult> Create(CreateBranchRequest input,CancellationToken ct)=>Run((t,s)=>service.CreateAsync(t,s,input,ct),false);
    [HttpGet("api/branches/{id:long}/edit")]
    public Task<IActionResult> Read(long id,CancellationToken ct)=>Run((t,s)=>service.ReadAsync(t,s,id,ct),false);
    [HttpPut("api/branches/{id:long}")]
    public Task<IActionResult> Update(long id,UpdateBranchRequest input,CancellationToken ct)=>Run((t,s)=>service.UpdateAsync(t,s,id,Tag(),input,ct),true);
    [HttpGet("api/branches/{id:long}/deactivation-dependencies")]
    public Task<IActionResult> Dependencies(long id,CancellationToken ct)=>Run((t,s)=>service.DependenciesAsync(t,s,id,ct),false);
    [HttpPost("api/branches/{id:long}/deactivate")]
    public Task<IActionResult> Deactivate(long id,DeactivateStructureRequest input,CancellationToken ct)=>Run((t,s)=>service.DeactivateAsync(t,s,id,Tag(),input,ct),true);

    private byte[] Tag()
    {
        var values=Request.Headers.IfMatch;
        if(values.Count!=1 || values[0] is not {Length:14} text || text[0]!='"' || text[^1]!='"')throw new ValidationException("INVALID_IF_MATCH");
        try{var version=Convert.FromBase64String(text[1..^1]);if(version.Length==8 && Convert.ToBase64String(version)==text[1..^1])return version;}
        catch(FormatException){}
        throw new ValidationException("INVALID_IF_MATCH");
    }
    private async Task<IActionResult> Run(Func<HotelOps.Api.Services.Authentication.SessionReference,long,Task<BranchAdministrationResult>> call,bool conditional)
    {
        if(!Request.IsHttps)return Problem(statusCode:400,title:"HTTPS_REQUIRED");
        if(!HotelOpsCookieTicket.TryRead(User,out var ticket,out var sid))return Problem(statusCode:401,title:"SESSION_INVALID");
        if(conditional && !Request.Headers.ContainsKey("If-Match"))return Problem(statusCode:428,title:"IF_MATCH_REQUIRED");
        try
        {
            var result=await call(ticket!,sid);
            if(result.ETag!=null)Response.Headers.ETag=result.ETag;
            if(result.Status<400)return StatusCode(result.Status,result.Value);
            return new ObjectResult(new ProblemDetails{Status=result.Status,Title=result.Code,Extensions={ ["dependencies"]=result.Value }}){StatusCode=result.Status};
        }
        catch(ValidationException e){return Problem(statusCode:400,title:"VALIDATION_FAILED",detail:e.Message);}
    }
}
