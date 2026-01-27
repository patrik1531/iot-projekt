using Application.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace Application.Controllers;

[ApiController]
[Route("api/mqtt")]
public class MqttController : ControllerBase
{
    private readonly IMqttService _mqttService;

    public MqttController(IMqttService mqttService)
    {
        _mqttService = mqttService;
    }

    [HttpPost("publish")]
    public async Task<IActionResult> Publish([FromQuery] string topic, [FromBody] string message)
    {
        await _mqttService.PublishMessage(topic, message);
        return Ok("Message Published");
    }
}