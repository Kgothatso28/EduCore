using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EduCore.Infrastructure.Persistence;

namespace EduCore.Api.Controllers;

[ApiController]
[Route("api/v1/system")]
public class SystemController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly EduCoreDbContext _dbContext;

    public SystemController(IConfiguration configuration, EduCoreDbContext dbContext)
    {
        _configuration = configuration;
        _dbContext = dbContext;
    }

    [HttpGet("health")]
    public async Task<IActionResult> GetHealth()
    {
        var env = _configuration["ASPNETCORE_ENVIRONMENT"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        var canConnect = false;
        try
        {
            canConnect = await _dbContext.Database.CanConnectAsync(HttpContext.RequestAborted);
        }
        catch
        {
            canConnect = false;
        }

        return Ok(new { status = "Healthy", environment = env, database = new { canConnect } });
    }
}
