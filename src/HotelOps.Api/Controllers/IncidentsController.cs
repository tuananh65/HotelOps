using HotelOps.Api.DTOs.Incidents;
using HotelOps.Api.Services.Incidents;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController]
[Route("api/incidents")]
public class IncidentsController : ControllerBase
{
    private readonly IIncidentService _incidentService;

    public IncidentsController(IIncidentService incidentService)
    {
        _incidentService = incidentService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<IncidentListDto>>> GetIncidents(CancellationToken cancellationToken)
    {
        var incidents = await _incidentService.GetIncidentsAsync(cancellationToken);
        return Ok(incidents);
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
