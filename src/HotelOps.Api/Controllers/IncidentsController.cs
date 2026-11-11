using HotelOps.Api.DTOs.Incidents;
using HotelOps.Api.Services.Incidents;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace HotelOps.Api.Controllers;

[ApiController]
[IncidentAccessFilter]
[Route("api/incidents")]
public class IncidentsController : ControllerBase
{
    private readonly IIncidentService _incidentService;

    public IncidentsController(IIncidentService incidentService)
    {
        _incidentService = incidentService;
    }

    [HttpPatch("{id:long}/status")]
    public async Task<ActionResult<IncidentDetailDto>> ChangeIncidentStatus(
        long id, ChangeIncidentStatusRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var incident = await _incidentService.ChangeIncidentStatusAsync(id, request, cancellationToken);
            if (incident is null)
            {
                return NotFound();
            }

            return Ok(incident);
        }
        catch (ValidationException exception)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Incident status validation failed", detail: exception.Message);
        }
    }

    [HttpPatch("{id:long}/triage")]
    public async Task<ActionResult<IncidentDetailDto>> TriageIncident(
        long id, TriageIncidentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var incident = await _incidentService.TriageIncidentAsync(id, request, cancellationToken);
            if (incident is null)
            {
                return NotFound();
            }

            return Ok(incident);
        }
        catch (ValidationException exception)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Incident triage validation failed", detail: exception.Message);
        }
    }

    [HttpPost]
    [Microsoft.AspNetCore.Authorization.Authorize(AuthenticationSchemes = HotelOps.Api.Security.HotelOpsCookieTicket.Scheme)]
    [ValidateAntiForgeryToken]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<IncidentDetailDto>> CreateIncident(
        CreateIncidentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var incident = await _incidentService.CreateIncidentAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetIncident), new { id = incident.IncidentId }, incident);
        }
        catch (ValidationException exception)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Incident validation failed", detail: exception.Message);
        }
    }

    [HttpGet("options")]
    [Microsoft.AspNetCore.Authorization.Authorize(AuthenticationSchemes = HotelOps.Api.Security.HotelOpsCookieTicket.Scheme)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<IncidentOptionsDto>> GetOptions(
        [FromQuery] long? branchId, [FromQuery] long? roomId, CancellationToken cancellationToken)
    {
        if (Request.Query["branchId"].Count > 1 || Request.Query["roomId"].Count > 1)
            return Problem(statusCode: 400, title: "Duplicate incident context query parameter");
        try { return Ok(await _incidentService.GetOptionsAsync(branchId, roomId, cancellationToken)); }
        catch (ValidationException exception)
        {
            return Problem(statusCode: 400, title: "Incident context validation failed", detail: exception.Message);
        }
    }

    [HttpGet]
    public async Task<ActionResult<IncidentPagedResponse>> GetIncidents(
        [FromQuery] IncidentQueryRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var incidents = await _incidentService.GetIncidentsAsync(request, cancellationToken);
            return Ok(incidents);
        }
        catch (ValidationException exception)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Incident query validation failed", detail: exception.Message);
        }
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<IncidentDetailDto>> GetIncident(long id, CancellationToken cancellationToken)
    {
        var incident = await _incidentService.GetIncidentAsync(id, cancellationToken);
        if (incident is null)
        {
            return NotFound();
        }

        return Ok(incident);
    }
}
