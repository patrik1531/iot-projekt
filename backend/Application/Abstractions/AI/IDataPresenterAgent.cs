using System.Threading;
using System.Threading.Tasks;
using Application.Dto;

namespace Application.Abstractions.AI;

public interface IDataPresenterAgent
{
    // returns human-readable summary (or JSON string if requested)
    Task<string> PresentAsync(ReportRequest req, ReportResponse processed, CancellationToken ct);
}
