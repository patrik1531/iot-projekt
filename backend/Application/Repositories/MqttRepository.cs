using System.Buffers;
using System.Reflection;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Application.Abstractions;
using Application.Data;
using Application.Models;
using Application.Models.Sensors;
using Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Protocol;

namespace Application.Repositories;

public class MqttRepository : IMqttRepository
{
    private readonly IMqttClient _client;
    private readonly NotifierSettings _settings;
    private readonly MqttClientOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SensorEventService _eventService;

    // cache: metric name ("air_quality") -> CLR type (AirQuality)
    private static readonly Dictionary<string, Type> _metricTypeCache =
        new(StringComparer.OrdinalIgnoreCase);

    public MqttRepository(
        IOptions<NotifierSettings> settings,
        IServiceScopeFactory scopeFactory,
        SensorEventService eventService)
    {
        _settings = settings.Value;
        _scopeFactory = scopeFactory;
        _eventService = eventService;

        var factory = new MqttClientFactory();
        _client = factory.CreateMqttClient();

        var builder = new MqttClientOptionsBuilder()
            .WithClientId(_settings.Uid)
            .WithTcpServer(_settings.Broker, _settings.Port)
            .WithCleanSession();

        if (!string.IsNullOrWhiteSpace(_settings.Username))
        {
            builder.WithCredentials(_settings.Username, _settings.Password);
        }
        
        if (_settings.Ssl)
        {
            builder.WithTlsOptions(tls =>
            {
                tls.UseTls();
                tls.WithSslProtocols(SslProtocols.Tls12);

                // DEV ONLY:
                tls.WithAllowUntrustedCertificates(true);
                tls.WithIgnoreCertificateChainErrors(true);
                tls.WithIgnoreCertificateRevocationErrors(true);
                tls.WithCertificateValidationHandler(_ => true);
            });
        }
        
        builder.WithWillTopic($"{_settings.TopicPrefix}/{_settings.Uid}/status")
            .WithWillPayload("{\"status\":\"offline\"}")
            .WithWillQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .WithWillRetain();

        _options = builder.Build();

        _client.ApplicationMessageReceivedAsync += async e =>
        {
            var topic = e.ApplicationMessage.Topic ?? string.Empty;
            
            var seq = e.ApplicationMessage.Payload;
            var payload = seq.IsEmpty ? string.Empty : Encoding.UTF8.GetString(seq.ToArray());

            if (topic.EndsWith("/cmd", StringComparison.OrdinalIgnoreCase))
                await HandleCommand(payload);
            else if (topic.EndsWith("/notify", StringComparison.OrdinalIgnoreCase))
                await HandleNotification(payload);
            else
                await HandleSensors(payload, topic);
        };
    }

    public async Task ConnectAsync()
    {
        if (!_client.IsConnected)
        {
            Console.WriteLine($"Connecting to MQTT broker {_settings.Broker}:{_settings.Port}...");
            await _client.ConnectAsync(_options);
            Console.WriteLine("Connected to MQTT broker!");
        }

        await PublishAsync($"{_settings.Uid}/status", "{\"status\":\"online\"}");

        var notifyTopic = $"{_settings.TopicPrefix}/{_settings.Uid}/notify";
        var cmdTopic = $"{_settings.TopicPrefix}/{_settings.Uid}/cmd";
        
        var sensorsTopic = "kpi/kronos/joke/ps418ph/data";

        var subBuilder = new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(f => f.WithTopic(notifyTopic).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce))
            .WithTopicFilter(f => f.WithTopic(cmdTopic).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce))
            .WithTopicFilter(f => f.WithTopic(sensorsTopic).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce));

        Console.WriteLine($"Subscribing to: {notifyTopic}");
        Console.WriteLine($"Subscribing to: {cmdTopic}");
        Console.WriteLine($"Subscribing to: {sensorsTopic}");

        await _client.SubscribeAsync(subBuilder.Build());

        Console.WriteLine("MQTT setup complete!");
    }

    public async Task PublishAsync(string topic, string payload)
    {
        var message = new MqttApplicationMessageBuilder()
            .WithTopic($"{_settings.TopicPrefix}/{topic}")
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await _client.PublishAsync(message);
    }

    private async Task HandleCommand(string payload)
    {
        var command = JsonSerializer.Deserialize<Command>(payload);
        if (command == null) return;

        Console.WriteLine($"CMD: {command.Cmd}");

        if (string.Equals(command.Cmd, "shutdown", StringComparison.OrdinalIgnoreCase))
        {
            await PublishAsync($"{_settings.Uid}/status", "{\"status\":\"offline\"}");

            var disc = new MqttClientDisconnectOptionsBuilder()
                .WithReason(MqttClientDisconnectOptionsReason.NormalDisconnection)
                .Build();

            await _client.DisconnectAsync(disc);
        }
    }

    private Task HandleNotification(string payload)
    {
        var notification = JsonSerializer.Deserialize<Notification>(payload);
        if (notification == null) return Task.CompletedTask;

        Console.WriteLine($"NOTIFY: {notification.Title} - {notification.Body}");
        return Task.CompletedTask;
    }

    private async Task HandleSensors(string payload, string topic)
    {
        Console.WriteLine("=== SENSORS DATA RECEIVED ===");
        Console.WriteLine($"topic: {topic}");
        Console.WriteLine(payload);
        Console.WriteLine("==============================");

        try
        {
            // parse JSON once
            if (string.IsNullOrWhiteSpace(payload))
                return;

            using var doc = JsonDocument.Parse(payload);

            // -------- MULTI METRICS payload: {"metrics":[...], "dt": "..."} --------
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("metrics", out var metricsElem)
                && metricsElem.ValueKind == JsonValueKind.Array)
            {
                // device id from topic (kpi/endor/joke/ps418ph/data -> ps418ph)
                var segments = topic.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                var deviceId = segments.Length >= 2 ? segments[^2] : "unknown";

                var sensorAssembly = typeof(Temperature).Assembly;

                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var addedAny = false;

                foreach (var metric in metricsElem.EnumerateArray())
                {
                    try
                    {
                        if (!metric.TryGetProperty("name", out var nameProp))
                        {
                            Console.WriteLine("Metric without 'name' -> skipping");
                            continue;
                        }

                        var name = nameProp.GetString();
                        if (string.IsNullOrWhiteSpace(name))
                        {
                            Console.WriteLine("Metric with empty 'name' -> skipping");
                            continue;
                        }

                        // resolve metric name -> sensor CLR type (cached)
                        var sensorType = ResolveSensorTypeFromMetricName(sensorAssembly, name.Trim());
                        if (sensorType == null)
                            continue;

                        // Deserialize THIS metric object into the right model
                        var metricJson = metric.GetRawText();
                        var sensorObj = JsonSerializer.Deserialize(metricJson, sensorType, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        if (sensorObj == null)
                        {
                            Console.WriteLine($"Failed to deserialize '{name}' into {sensorType.Name}");
                            continue;
                        }

                        // Optional: duplicate check by Dt (works if Dt is PK and present)
                        DateTime? dtValue = TryParseMetricDt(metric, doc.RootElement);

                        if (dtValue.HasValue)
                        {
                            var existing = await dbContext.FindAsync(sensorType, new object[] { dtValue.Value });
                            if (existing != null)
                            {
                                Console.WriteLine($"Skipping duplicate {sensorType.Name} Dt={dtValue.Value:o}");
                                continue;
                            }
                        }

                        dbContext.Add(sensorObj);
                        addedAny = true;

                        // SSE event per metric
                        await _eventService.SendEventAsync(deviceId, sensorObj);
                        Console.WriteLine($"Queued save + SSE: {sensorType.Name} (device={deviceId})");
                    }
                    catch (Exception exMetric)
                    {
                        Console.WriteLine($"Error handling metric entry: {exMetric.Message}");
                    }
                }

                if (addedAny)
                {
                    await dbContext.SaveChangesAsync();
                    Console.WriteLine("Saved metrics to database.");
                }

                return;
            }

            // -------- FALLBACK: single sensor payload routed by topic suffix --------
            var lastSegment = topic.Contains('/') ? topic.Split('/').Last() : topic;
            var sensorShort = lastSegment.Contains('-') ? lastSegment.Split('-').Last() : lastSegment;

            if (string.IsNullOrWhiteSpace(sensorShort))
            {
                Console.WriteLine("Could not determine sensor name from topic.");
                return;
            }

            var typeName = ToPascalCaseFromSnake(sensorShort);

            var sensorAssembly2 = typeof(Temperature).Assembly;
            var fullTypeName = $"Application.Models.Sensors.{typeName}";
            var sensorType2 = sensorAssembly2.GetType(fullTypeName);

            if (sensorType2 == null)
            {
                Console.WriteLine($"Unknown sensor type for topic {topic} (tried {fullTypeName})");
                return;
            }

            var sensorObjSingle = JsonSerializer.Deserialize(payload, sensorType2, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (sensorObjSingle == null)
            {
                Console.WriteLine("Deserialized sensor object is null.");
                return;
            }

            using var scopeSingle = _scopeFactory.CreateScope();
            var dbContextSingle = scopeSingle.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            dbContextSingle.Add(sensorObjSingle);
            await dbContextSingle.SaveChangesAsync();

            Console.WriteLine($"Saved sensor data ({typeName}) to database.");

            await _eventService.SendEventAsync(sensorShort, sensorObjSingle);
            Console.WriteLine($"Sent SSE event for {typeName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving sensor data: {ex.Message}");
        }
    }

    private static Type? ResolveSensorTypeFromMetricName(Assembly sensorAssembly, string metricName)
    {
        // cache by metricName (e.g. "air_quality")
        if (_metricTypeCache.TryGetValue(metricName, out var cached))
            return cached;

        var typeName = ToPascalCaseFromSnake(metricName); // "air_quality" -> "AirQuality"
        var fullTypeName = $"Application.Models.Sensors.{typeName}";
        var t = sensorAssembly.GetType(fullTypeName);

        if (t == null)
        {
            Console.WriteLine($"Unknown metric '{metricName}' (tried {fullTypeName}) -> skipping");
            return null;
        }

        _metricTypeCache[metricName] = t;
        return t;
    }

    private static DateTime? TryParseMetricDt(JsonElement metric, JsonElement root)
    {
        // metric.dt
        if (metric.TryGetProperty("dt", out var dtProp) && dtProp.ValueKind == JsonValueKind.String)
        {
            var dtStr = dtProp.GetString();
            if (!string.IsNullOrWhiteSpace(dtStr) &&
                DateTime.TryParse(dtStr, null,
                    System.Globalization.DateTimeStyles.AdjustToUniversal |
                    System.Globalization.DateTimeStyles.AssumeUniversal,
                    out var parsed))
            {
                return parsed;
            }
        }

        // root.dt fallback
        if (root.TryGetProperty("dt", out var rootDt) && rootDt.ValueKind == JsonValueKind.String)
        {
            var dtStr = rootDt.GetString();
            if (!string.IsNullOrWhiteSpace(dtStr) &&
                DateTime.TryParse(dtStr, null,
                    System.Globalization.DateTimeStyles.AdjustToUniversal |
                    System.Globalization.DateTimeStyles.AssumeUniversal,
                    out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static string ToPascalCaseFromSnake(string snake)
    {
        if (string.IsNullOrWhiteSpace(snake)) return snake;
        var parts = snake.Split(new[] { '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            var p = parts[i];
            parts[i] = p.Length == 0
                ? p
                : char.ToUpperInvariant(p[0]) + (p.Length > 1 ? p.Substring(1) : string.Empty);
        }
        return string.Concat(parts);
    }

    private static string ToSnakeCaseFromPascal(string pascal)
    {
        if (string.IsNullOrEmpty(pascal)) return pascal;
        var withUnderscores = Regex.Replace(pascal, "([a-z0-9])([A-Z])", "$1_$2");
        return withUnderscores.ToLowerInvariant();
    }
}