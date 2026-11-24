using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Compact.Controllers;

[ApiController,Authorize,Route("api/v2/work")]
public sealed class CleaningReconciliationController(WorkService service):ControllerBase
{
    [HttpPost("{id:long}/reconcile-supplies")]
    public Task<object> Reconcile(long id,ActionRequest request,CancellationToken ct)=>service.ReconcileCleaningSupplies(id,request,ct);
}
