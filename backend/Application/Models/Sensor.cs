using System.ComponentModel.DataAnnotations;

namespace Application.Models;

public enum Status
{
    Offline,
    Online
}

public class Sensor
{
    [Key]
    public string Name { get; set; } = string.Empty;
    public Status Status { get; set; } = Status.Offline;
    
}