using System.Threading;
using System.Threading.Tasks;
using Application.Dto;

namespace Application.Abstractions.AI;

public interface IDataProcessorAgent
{
    Task<ReportResponse> ProcessAsync(object rawData, ReportRequest req, CancellationToken ct);
}
