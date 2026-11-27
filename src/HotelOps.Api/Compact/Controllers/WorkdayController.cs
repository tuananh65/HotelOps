using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HotelOps.Api.Compact.Controllers;
[ApiController,Authorize,Route("api/v2/workday")]
public sealed class WorkdayController(WorkdayService service):ControllerBase
{
    [HttpGet("command-access")] public Task<object> CommandAccess(long branchId,CancellationToken ct)=>service.CommandAccess(branchId,ct);
    [HttpGet("personnel")] public Task<PersonnelRow[]> Personnel(long branchId,CancellationToken ct)=>service.Personnel(branchId,ct);
    [HttpGet("personal-summary")] public Task<AttendanceSummaryDto> PersonalSummary(long branchId,CancellationToken ct)=>service.PersonalSummary(branchId,ct);
    [HttpGet] public Task<object> Week(long branchId,DateOnly date,CancellationToken ct)=>service.Week(branchId,date,ct);
    [HttpGet("candidates")] public Task<object> Candidates(long shiftId,DateOnly date,CancellationToken ct)=>service.Candidates(shiftId,date,ct);
    [HttpGet("candidates/batch")] public Task<WorkdayCandidatesDto> BatchCandidates(long shiftId,[FromQuery] DateOnly[] dates,CancellationToken ct)=>service.BatchCandidates(shiftId,dates,ct);
    [HttpPost("assignments/batch")] public Task<object> AssignBatch(AssignShiftsRequest request,CancellationToken ct)=>service.AssignBatch(request,ct);
    [HttpGet("configuration")] public Task<object> Configuration(long branchId,CancellationToken ct)=>service.Configuration(branchId,ct);
    [HttpPost("shifts")] public Task<object> CreateShift(SaveWorkdayShiftRequest request,CancellationToken ct)=>service.SaveShift(null,request,ct);
    [HttpPut("shifts/{id:long}")] public Task<object> UpdateShift(long id,SaveWorkdayShiftRequest request,CancellationToken ct)=>service.SaveShift(id,request,ct);
    [HttpGet("calendar")] public Task<object> Calendar(long branchId,int year,CancellationToken ct)=>service.Calendar(branchId,year,ct);
    [HttpPost("assignments")] public Task<object> Assign(AssignShiftRequest request,CancellationToken ct)=>service.Assign(request,ct);
    [HttpPost("assignments/{id:long}/end")] public Task<object> End(long id,ActionRequest request,CancellationToken ct)=>service.Action(id,"end",request,ct);
    [HttpPost("assignments/{id:long}/cancel")] public Task<object> Cancel(long id,ActionRequest request,CancellationToken ct)=>service.Action(id,"cancel",request,ct);
    [HttpGet("assignments/{id:long}/history")] public Task<object> History(long id,CancellationToken ct)=>service.History(id,ct);
}
