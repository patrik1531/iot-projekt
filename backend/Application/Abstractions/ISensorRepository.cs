using Application.Models;

namespace Application.Abstractions;

public interface ISensorRepository
{
    Task<List<Sensor>> GetAllAsync();
    Task<List<Sensor>> GetOnlineAsync();
    Task<List<Sensor>> GetOfflineAsync();

    Task<T?> GetLatestAsync<T>() where T : class;
    Task<List<T>> GetLast24HoursAsync<T>() where T : class;
    Task<List<T>> GetLastWeekAsync<T>() where T : class;
    Task<List<T>> GetLastMonthAsync<T>() where T : class;
    Task<List<T>> GetLastYearAsync<T>() where T : class;
    Task<List<T>> GetByDateRangeAsync<T>(DateTime from, DateTime to) where T : class;
}