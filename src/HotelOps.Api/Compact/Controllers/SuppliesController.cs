using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HotelOps.Api.Compact.Controllers;

[ApiController,Authorize,Route("api/v2/supplies")]
public sealed class SuppliesController(SupplyConfigurationService configuration,StaffSupplyService supplies):ControllerBase
{
    [HttpGet("configuration")] public Task<IReadOnlyList<SupplyConfigurationDto>> Configuration(long branchId,CancellationToken ct)=>configuration.List(branchId,ct);
    [HttpPut("configuration")] public Task<SupplyConfigurationDto> Save(SaveSupplyConfigurationRequest request,CancellationToken ct)=>configuration.Save(request,ct);
    [HttpGet("allocations")] public Task<IReadOnlyList<StaffSupplyAllocationDto>> Allocations(long branchId,long? userId,CancellationToken ct)=>supplies.List(branchId,userId,ct);
    [HttpGet("allocations/{id:long}/history")] public Task<IReadOnlyList<SupplyLedgerDto>> History(long id,CancellationToken ct)=>supplies.History(id,ct);
    [HttpGet("documents/{id:long}")] public Task<object> Document(long id,CancellationToken ct)=>supplies.Document(id,ct);
    [HttpGet("room-assessments/{workId:long}")] public Task<IReadOnlyList<RoomSupplyAssessmentLine>> Assessment(long workId,CancellationToken ct)=>supplies.RoomAssessment(workId,ct);
    [HttpPost("room-assessments/{workId:long}")] public Task<IReadOnlyList<RoomSupplyAssessmentLine>> Assess(long workId,AssessRoomSuppliesRequest request,CancellationToken ct)=>supplies.AssessRoom(workId,request,ct);
    // Allocation, consumption and return have no standalone public mutation endpoint.
    [HttpGet("minimum")] public Task<object> Minimum(long branchId,long warehouseId,long itemId,CancellationToken ct)=>configuration.Minimum(branchId,warehouseId,itemId,ct);
    [HttpPut("minimum")] public Task<object> SaveMinimum(SaveStockMinimumRequest request,CancellationToken ct)=>configuration.SaveMinimum(request,ct);
    // They are called atomically by workday/work/request transitions.
}
