using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Application.Dto;
using Application.Models.Sse;
using Application.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Application.Controllers;

[ApiController]
[Route("api/sensor-events")]
public class SensorEventsController : ControllerBase
{
    private readonly SensorEventService _eventService;

    public SensorEventsController(SensorEventService eventService)
    {
        _eventService = eventService;
    }

    [HttpGet("stream")]
    public IResult Stream(CancellationToken cancellationToken)
    {
        var reader = _eventService.Subscribe();

        return TypedResults.ServerSentEvents(ToSseItems(reader, cancellationToken));
    }

    private async IAsyncEnumerable<SseItem<SseUpdateDto>> ToSseItems(
        ChannelReader<SseUpdate> reader,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var item in reader.ReadAllAsync(cancellationToken))
            {
                var sseUpdateDto = new SseUpdateDto
                {
                    Value = item.Value
                };
                yield return new SseItem<SseUpdateDto>(sseUpdateDto, $"{item.TypeName}-update");
            }
        }
        finally
        {
            _eventService.Unsubscribe(reader);
        }
    }
}