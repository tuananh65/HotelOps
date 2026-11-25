using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HotelOps.Api.Compact.Controllers;
[ApiController,Authorize,Route("api/v2/inventory-lifecycle")]
public sealed class InventoryLifecycleController(InventoryLifecycleService service):ControllerBase
{
    [HttpGet("profiles")]public Task<object> Profiles(long branchId,CancellationToken ct)=>service.Profiles(branchId,ct);
    [HttpPut("profiles")]public Task<object> Configure(ItemLifecycleRequest request,CancellationToken ct)=>service.Configure(request,ct);
    [HttpGet("batches")]public Task<object> Batches(long branchId,long? itemId,CancellationToken ct,int? page=null,int pageSize=25,string? search=null)=>service.Batches(branchId,itemId,ct,page,pageSize,search);
    [HttpPut("batches/{id:long}")]public Task<object> Verify(long id,BatchMetadataRequest request,CancellationToken ct)=>service.VerifyBatch(id,request,ct);
    [HttpGet("batches/{id:long}/history")]public Task<object> History(long id,CancellationToken ct,int? page=null,int pageSize=25)=>service.BatchHistory(id,ct,page,pageSize);
}
