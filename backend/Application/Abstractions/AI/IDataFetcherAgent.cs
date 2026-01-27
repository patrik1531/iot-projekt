using System.Threading;
using System.Threading.Tasks;
using Application.Dto;

namespace Application.Abstractions.AI;

public interface IDataFetcherAgent
{
    // returns raw data object (implementation-specific)
    Task<object?> FetchAsync(ReportRequest req, CancellationToken ct);
}
