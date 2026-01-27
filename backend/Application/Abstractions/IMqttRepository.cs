namespace Application.Abstractions;

public interface IMqttRepository
{
    Task ConnectAsync();
    Task PublishAsync(string topic, string payload);
}