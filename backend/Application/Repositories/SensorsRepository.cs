using System.Linq.Expressions;
using Application.Abstractions;
using Application.Data;
using Application.Models;
using Microsoft.EntityFrameworkCore;

namespace Application.Repositories;

public class SensorRepository : ISensorRepository
{
    private readonly ApplicationDbContext _dbContext;

    public SensorRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<T?> GetLatestAsync<T>() where T : class
    {
        return await _dbContext.Set<T>()
            .OrderByDescending(x => EF.Property<DateTime>(x, "Dt"))
            .FirstOrDefaultAsync();
    }

    public async Task<List<T>> GetLast24HoursAsync<T>() where T : class
    {
        var from = DateTime.UtcNow.AddHours(-24);
        return await QueryByDateAsync<T>(from, DateTime.UtcNow);
    }

    public async Task<List<T>> GetLastWeekAsync<T>() where T : class
    {
        var from = DateTime.UtcNow.AddDays(-7);
        return await QueryByDateAsync<T>(from, DateTime.UtcNow);
    }

    public async Task<List<T>> GetLastMonthAsync<T>() where T : class
    {
        var from = DateTime.UtcNow.AddDays(-30);
        return await QueryByDateAsync<T>(from, DateTime.UtcNow);
    }

    public async Task<List<T>> GetLastYearAsync<T>() where T : class
    {
        var from = DateTime.UtcNow.AddMonths(-12);
        return await QueryByDateAsync<T>(from, DateTime.UtcNow);
    }

    public async Task<List<T>> GetByDateRangeAsync<T>(DateTime from, DateTime to) where T : class
    {
        return await QueryByDateAsync<T>(from, to);
    }

    private async Task<List<T>> QueryByDateAsync<T>(DateTime from, DateTime to) where T : class
    {
        var param = Expression.Parameter(typeof(T), "x");
        var dtProperty = Expression.Property(param, "Dt");
        var fromConst = Expression.Constant(from);
        var toConst = Expression.Constant(to);

        var greaterOrEqual = Expression.GreaterThanOrEqual(dtProperty, fromConst);
        var lessOrEqual = Expression.LessThanOrEqual(dtProperty, toConst);
        var andExpr = Expression.AndAlso(greaterOrEqual, lessOrEqual);

        var lambda = Expression.Lambda<Func<T, bool>>(andExpr, param);

        return await _dbContext.Set<T>()
            .Where(lambda)
            .OrderBy(x => EF.Property<DateTime>(x, "Dt"))
            .ToListAsync();
    }

    public async Task<List<Sensor>> GetAllAsync()
    {
        return await _dbContext.Set<Sensor>().OrderBy(s => s.Name).ToListAsync();
    }

    public async Task<List<Sensor>> GetOnlineAsync()
    {
        return await _dbContext.Set<Sensor>().Where(s => s.Status == Status.Online).OrderBy(s => s.Name).ToListAsync();
    }

    public async Task<List<Sensor>> GetOfflineAsync()
    {
        return await _dbContext.Set<Sensor>().Where(s => s.Status == Status.Offline).OrderBy(s => s.Name).ToListAsync();
    }
}
