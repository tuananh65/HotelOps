using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HotelOps.Api.Compact.Controllers;
[ApiController,Authorize,Route("api/v2/reusable-inventory")]
public sealed class ReusableInventoryController(ReusableInventoryService service):ControllerBase
{
    [HttpGet]public Task<object> List(long branchId,CancellationToken ct)=>service.List(branchId,ct);
    [HttpGet("destinations")]public Task<object> Destinations(long branchId,CancellationToken ct)=>service.Destinations(branchId,ct);
    [HttpGet("history")]public Task<object> History(long branchId,CancellationToken ct)=>service.History(branchId,ct);
    [HttpPost("positions/{id:long}/move")]public Task<object> Move(long id,ReusableMoveRequest request,CancellationToken ct)=>service.Move(id,request,ct);
    [HttpPost("positions/{id:long}/write-off")]public Task<object> Propose(long id,ReusableWriteOffRequest request,CancellationToken ct)=>service.Propose(id,request,ct);
    [HttpGet("write-offs")]public Task<object> WriteOffs(long branchId,CancellationToken ct)=>service.WriteOffs(branchId,ct);
    [HttpPost("write-offs/{id:long}/decision")]public Task<object> Decide(long id,DecideWriteOffRequest request,CancellationToken ct)=>service.Decide(id,request,ct);
}
