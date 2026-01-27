namespace Application.Dto;

public class SensorLatestDto
{
    public object? Value { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime Dt { get; set; }
    public string? Units { get; set; }
}
