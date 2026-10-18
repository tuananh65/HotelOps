using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Maintenance;
using HotelOps.Api.Services.Maintenance;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController]
[Route("api/maintenance-works")]
public class MaintenanceWorksController : ControllerBase
{
    private readonly IMaintenanceWorkService _service;

    public MaintenanceWorksController(IMaintenanceWorkService service)
    {
        _service = service;
    }

    [HttpPost("/api/incidents/{incidentId:long}/maintenance-works")]
    public async Task<ActionResult<MaintenanceWorkDetailDto>> CreateWork(long incidentId,
        CreateMaintenanceWorkRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.CreateWorkAsync(incidentId, request, cancellationToken);
            if (result is null)
                return NotFound();
            return CreatedAtAction(nameof(GetWork), new { id = result.MaintenanceWorkId }, result);
        }
        catch (ValidationException exception)
        {
            return ValidationFailure(exception);
        }
    }

    [HttpPatch("{id:long}/assignment")]
    public async Task<ActionResult<MaintenanceWorkDetailDto>> AssignWork(long id,
        AssignMaintenanceWorkRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.AssignWorkAsync(id, request, cancellationToken);
            if (result is null)
                return NotFound();
            return Ok(result);
        }
        catch (ValidationException exception)
        {
            return ValidationFailure(exception);
        }
    }

    [HttpPost("{id:long}/start")]
    public async Task<ActionResult<MaintenanceWorkDetailDto>> StartWork(long id,
        StartMaintenanceWorkRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.StartWorkAsync(id, request, cancellationToken);
            if (result is null)
                return NotFound();
            return Ok(result);
        }
        catch (ValidationException exception)
        {
            return ValidationFailure(exception);
        }
    }

    [HttpPost("{id:long}/hold")]
    public async Task<ActionResult<MaintenanceWorkDetailDto>> HoldWork(long id,
        HoldMaintenanceWorkRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.HoldWorkAsync(id, request, cancellationToken);
            if (result is null)
                return NotFound();
            return Ok(result);
        }
        catch (ValidationException exception)
        {
            return ValidationFailure(exception);
        }
    }

    [HttpPost("{id:long}/resume")]
    public async Task<ActionResult<MaintenanceWorkDetailDto>> ResumeWork(long id,
        ResumeMaintenanceWorkRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.ResumeWorkAsync(id, request, cancellationToken);
            if (result is null)
                return NotFound();
            return Ok(result);
        }
        catch (ValidationException exception)
        {
            return ValidationFailure(exception);
        }
    }

    [HttpPost("{id:long}/logs")]
    public async Task<ActionResult<MaintenanceWorkLogDto>> CreateLog(long id,
        CreateMaintenanceWorkLogRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.CreateLogAsync(id, request, cancellationToken);
            if (result is null)
                return NotFound();
            return CreatedAtAction(nameof(GetWork), new { id }, result);
        }
        catch (ValidationException exception)
        {
            return ValidationFailure(exception);
        }
    }

    [HttpPost("{id:long}/submit-result")]
    public async Task<ActionResult<MaintenanceWorkDetailDto>> SubmitResult(long id,
        SubmitMaintenanceResultRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.SubmitResultAsync(id, request, cancellationToken);
            if (result is null)
                return NotFound();
            return Ok(result);
        }
        catch (ValidationException exception)
        {
            return ValidationFailure(exception);
        }
    }

    [HttpPost("{id:long}/verify")]
    public async Task<ActionResult<MaintenanceWorkDetailDto>> VerifyWork(long id,
        VerifyMaintenanceWorkRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.VerifyWorkAsync(id, request, cancellationToken);
            if (result is null)
                return NotFound();
            return Ok(result);
        }
        catch (ValidationException exception)
        {
            return ValidationFailure(exception);
        }
    }

    [HttpPost("{id:long}/return")]
    public async Task<ActionResult<MaintenanceWorkDetailDto>> ReturnWork(long id,
        ReturnMaintenanceWorkRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.ReturnWorkAsync(id, request, cancellationToken);
            if (result is null)
                return NotFound();
            return Ok(result);
        }
        catch (ValidationException exception)
        {
            return ValidationFailure(exception);
        }
    }

    [HttpPost("{id:long}/cancel")]
    public async Task<ActionResult<MaintenanceWorkDetailDto>> CancelWork(long id,
        CancelMaintenanceWorkRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.CancelWorkAsync(id, request, cancellationToken);
            if (result is null)
                return NotFound();
            return Ok(result);
        }
        catch (ValidationException exception)
        {
            return ValidationFailure(exception);
        }
    }

    [HttpGet]
    public async Task<ActionResult<MaintenancePagedResponse>> GetWorks(
        [FromQuery] MaintenanceQueryRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.GetWorksAsync(request, cancellationToken));
        }
        catch (ValidationException exception)
        {
            return ValidationFailure(exception);
        }
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<MaintenanceWorkDetailDto>> GetWork(long id, CancellationToken cancellationToken)
    {
        var result = await _service.GetWorkAsync(id, cancellationToken);
        if (result is null)
            return NotFound();
        return Ok(result);
    }

    private ObjectResult ValidationFailure(ValidationException exception) =>
        Problem(statusCode: StatusCodes.Status400BadRequest,
            title: "Maintenance validation failed", detail: exception.Message);
}
