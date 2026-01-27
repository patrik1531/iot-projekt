namespace Application.Dto;

public class SensorStatsResponse
{
    public double Avg { get; set; }
    public double Min { get; set; }
    public double Max { get; set; }
    public List<SensorDataPoint> Data { get; set; } = new();
}