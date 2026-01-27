namespace Application.Abstractions;

public interface IMqttService
{
    Task PublishMessage(string topic, string message);
}