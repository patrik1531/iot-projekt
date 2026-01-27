using Application.Abstractions;

namespace Application.Services;

public class MqttService : IMqttService
{
    private readonly IMqttRepository _mqttRepository;

    public MqttService(IMqttRepository mqttRepository)
    {
        _mqttRepository = mqttRepository;
    }

    public async Task PublishMessage(string topic, string message)
    {
        await _mqttRepository.ConnectAsync();
        await _mqttRepository.PublishAsync(topic, message);
    }
}