using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HotelOps.Api.Compact.Controllers;
[ApiController,Authorize,Route("api/v2/workday")]
public sealed class WorkdayController(WorkdayService service):ControllerBase
{
    [HttpGet] public Task<object> Week(long branchId,DateOnly date,CancellationToken ct)=>service.Week(branchId,date,ct);
    [HttpGet("candidates")] public Task<object> Candidates(long shiftId,DateOnly date,CancellationToken ct)=>service.Candidates(shiftId,date,ct);
    [HttpPost("assignments")] public Task<object> Assign(AssignShiftRequest request,CancellationToken ct)=>service.Assign(request,ct);
    [HttpPost("assignments/{id:long}/{command}")] public Task<object> Action(long id,string command,ActionRequest request,CancellationToken ct)=>service.Action(id,command,request,ct);
    [HttpGet("assignments/{id:long}/history")] public Task<object> History(long id,CancellationToken ct)=>service.History(id,ct);
}
