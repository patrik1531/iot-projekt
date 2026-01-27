// csharp
using System.Collections.Concurrent;
using System.Threading.Channels;
using Application.Models.Sse;

namespace Application.Services;

public class SensorEventService
{
    private readonly List<Channel<SseUpdate>> _channels = new();
    private readonly Lock _lock = new();
    private readonly ConcurrentDictionary<string, SseUpdate> _retained = new();

    public ChannelReader<SseUpdate> Subscribe()
    {
        var channel = Channel.CreateUnbounded<SseUpdate>();
        lock (_lock)
        {
            _channels.Add(channel);
        }
        foreach (var retained in _retained.Values)
        {
            channel.Writer.TryWrite(retained);
        }
        return channel.Reader;
    }

    public void Unsubscribe(ChannelReader<SseUpdate> reader)
    {
        lock (_lock)
        {
            var channel = _channels.FirstOrDefault(c => c.Reader == reader);
            if (channel != null)
            {
                _channels.Remove(channel);
            }
        }
    }

    public async Task SendEventAsync(string? _ignoredSensorType, object data)
    {
        object? payload;
        var valueProperty = data.GetType().GetProperty("Value");
        payload = valueProperty != null ? valueProperty.GetValue(data) : data;

        // Force the event type to the Application.Models.Sensors namespace + class name
        var dataType = data.GetType();
        var className = dataType.Name;
        var typeName = $"{className}";

        var sensorEvent = new SseUpdate
        {
            TypeName = typeName,
            Value = payload
        };
        _retained[typeName] = sensorEvent;

        List<Channel<SseUpdate>> snapshot;
        lock (_lock)
        {
            snapshot = _channels.ToList();
        }

        foreach (var channel in snapshot)
        {
            try
            {
                await channel.Writer.WriteAsync(sensorEvent);
            }
            catch
            {
                // Channel closed, ignore
            }
        }
    }
}
