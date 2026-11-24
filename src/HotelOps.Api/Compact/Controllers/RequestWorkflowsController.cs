using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Compact.Controllers;

[ApiController,Authorize,Route("api/v2/requests")]
public sealed class RequestWorkflowsController(RequestWorkflowService requests):ControllerBase
{
    [HttpGet] public Task<PageDto<RequestWorkflowSummary>> List(long branchId,CancellationToken ct,string queue="mine",int page=1,int pageSize=30,string? search=null)=>requests.List(branchId,queue,page,pageSize,search,ct);
    [HttpGet("types")] public Task<object> Types(long branchId,CancellationToken ct)=>requests.Types(branchId,ct);
    [HttpGet("{id:long}")] public Task<RequestWorkflowDetail> Detail(long id,CancellationToken ct)=>requests.Detail(id,ct);
    [HttpPost("refills")] public Task<RequestWorkflowDetail> Refill(CreateRefillWorkflowRequest request,CancellationToken ct)=>requests.CreateRefill(request,ct);
    [HttpPost("replenishments")] public Task<RequestWorkflowDetail> Replenish(CreateReplenishmentWorkflowRequest request,CancellationToken ct)=>requests.CreateReplenishment(request,ct);
    [HttpPost("{id:long}/actions/{command}")] public Task<RequestWorkflowDetail> Act(long id,string command,RequestWorkflowAction request,CancellationToken ct)=>requests.Act(id,command,request,ct);
    [HttpPost("{id:long}/receive")] public Task<RequestWorkflowDetail> Receive(long id,ReceiveReplenishmentWorkflowRequest request,CancellationToken ct)=>requests.Receive(id,request,ct);
}
