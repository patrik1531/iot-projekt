using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Services.Ventilation;
using Microsoft.AspNetCore.Mvc;

namespace Application.Controllers;

[ApiController]
[Route("api/ventilation")]
public sealed class VentilationController : ControllerBase
{
    private readonly VentilationQueryService _q;

    public VentilationController(VentilationQueryService q)
    {
        _q = q;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        var dto = await _q.GetLatestAsync(ct);
        return Ok(dto);
    }

    [HttpGet("events")]
    public async Task<IActionResult> GetEvents([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int limit = 200, CancellationToken ct = default)
    {
        var list = await _q.GetEventsAsync(from, to, limit, ct);
        return Ok(list);
    }
}
