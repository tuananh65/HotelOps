using HotelOps.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    private readonly HotelOpsDbContext _dbContext;

    public HealthController(HotelOpsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("database")]
    public async Task<IActionResult> CheckDatabase()
    {
        try
        {
            var canConnect = await _dbContext.Database.CanConnectAsync();

            if (!canConnect)
            {
                return StatusCode(503, new
                {
                    status = "error",
                    database = "HotelOpsDB",
                    message = "Cannot connect to database."
                });
            }

            return Ok(new
            {
                status = "ok",
                database = _dbContext.Database.GetDbConnection().Database,
                message = "Database connection successful."
            });
        }
        catch (Exception ex)
        {
            return StatusCode(503, new
            {
                status = "error",
                database = "HotelOpsDB",
                message = ex.Message
            });
        }
    }
}