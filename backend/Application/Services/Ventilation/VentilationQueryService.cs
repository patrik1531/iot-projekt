using Application.Data;
using Application.Dto.Ventilation;
using Microsoft.EntityFrameworkCore;

namespace Application.Services.Ventilation;

public sealed class VentilationQueryService
{
    private readonly ApplicationDbContext _db;

    public VentilationQueryService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<VentilationStatusDto> GetLatestAsync(CancellationToken ct)
    {
        var last = await _db.VentilationEvent
            .OrderByDescending(x => x.Dt)
            .FirstOrDefaultAsync(ct);

        if (last == null)
            return new VentilationStatusDto { Message = "No events" };

        return new VentilationStatusDto
        {
            Dt = last.Dt,
            Co2Ppm = last.Co2Ppm,
            WindowOpen = last.WindowOpen,
            Level = last.Level.ToString(),
            Action = last.Action.ToString(),
            Message = last.Message
        };
    }

    public async Task<List<VentilationEventDto>> GetEventsAsync(DateTime? fromUtc, DateTime? toUtc, int limit, CancellationToken ct)
    {
        var q = _db.VentilationEvent.AsQueryable();
        if (fromUtc.HasValue) q = q.Where(x => x.Dt >= fromUtc.Value);
        if (toUtc.HasValue) q = q.Where(x => x.Dt <= toUtc.Value);

        limit = limit <= 0 ? 200 : Math.Min(limit, 2000);

        var rows = await q
            .OrderByDescending(x => x.Dt)
            .Take(limit)
            .Select(x => new VentilationEventDto
            {
                Id = x.Id,
                Dt = x.Dt,
                DeviceId = x.DeviceId,
                Co2Ppm = x.Co2Ppm,
                WindowOpen = x.WindowOpen,
                Level = x.Level.ToString(),
                Action = x.Action.ToString(),
                Message = x.Message
            })
            .ToListAsync(ct);

        return rows;
    }
}