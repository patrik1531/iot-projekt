using System.Threading;
using System.Threading.Tasks;
using Application.Dto;
using Application.Services.Reports;
using Application.Abstractions.AI;
using Microsoft.AspNetCore.Mvc;

namespace Application.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly ReportOrchestrator _orchestrator;
    private readonly IAiSupervisorService _ai;

    public ReportsController(ReportOrchestrator orchestrator, IAiSupervisorService ai)
    {
        _orchestrator = orchestrator;
        _ai = ai;
    }

    [HttpPost("custom")]
    public async Task<IActionResult> Custom([FromBody] ReportRequest? req, CancellationToken ct)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.Metric))
            return BadRequest(new { error = "metric is required" });

        var (structured, text) = await _orchestrator.RunAsync(req, ct);
        return Ok(new { buckets = structured.Buckets, textSummary = text });
    }

    [HttpGet("ask")]
    public async Task<IActionResult> Ask([FromQuery] string q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q)) return BadRequest(new { error = "q is required" });
        var answer = await _ai.ReplyAsync(0, q, ct);
        return Ok(new { text = answer });
    }

    public class AskRequest
    {
        public string UserText { get; set; } = string.Empty;
    }

    [HttpPost("ask")]
    public async Task<IActionResult> AskPost([FromBody] AskRequest? body, CancellationToken ct)
    {
        if (body == null || string.IsNullOrWhiteSpace(body.UserText)) return BadRequest(new { error = "userText is required" });
        var answer = await _ai.ReplyAsync(0, body.UserText, ct);
        return Ok(new { text = answer });
    }
}
