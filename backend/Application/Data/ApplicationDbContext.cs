using Application.Models;
using Application.Models.Sensors;
using Application.Models.Ventilation;
using Microsoft.EntityFrameworkCore;

namespace Application.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }
    
    public DbSet<AirQuality>  AirQuality { get; set; }
    public DbSet<AirQualityPercent>  AirQualityPercent { get; set; }
    public DbSet<Humidity>  Humidity { get; set; }
    public DbSet<Motion>  Motion { get; set; }
    public DbSet<Temperature> Temperature { get; set; }
    public DbSet<Window> Window { get; set; }
    public DbSet<Sensor> Sensor { get; set; }
    public DbSet<VentilationEvent> VentilationEvent { get; set; }
}