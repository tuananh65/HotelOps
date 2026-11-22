using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Compact.Controllers;
[ApiController,Authorize,Route("api/v2/catalog")]
public sealed class CatalogController(CatalogService service):ControllerBase
{
    [HttpGet("{resource}")]public Task<object> List(string resource,long? branchId,CancellationToken ct,int page=1,int pageSize=50)=>service.List(resource,branchId,page,pageSize,ct);
    [HttpPost("{resource}")]public Task<object> Create(string resource,long? branchId,CatalogWriteRequest request,CancellationToken ct)=>service.Save(resource,null,branchId,request,ct);
    [HttpPut("{resource}/{id:long}")]public Task<object> Update(string resource,long id,long? branchId,CatalogWriteRequest request,CancellationToken ct)=>service.Save(resource,id,branchId,request,ct);
}
