using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.AI;
using Application.Dto;

namespace Application.Services.Reports;

public class ReportOrchestrator
{
    private readonly IDataFetcherAgent _fetcher;
    private readonly IDataProcessorAgent _processor;
    private readonly IDataPresenterAgent _presenter;

    public ReportOrchestrator(IDataFetcherAgent fetcher, IDataProcessorAgent processor, IDataPresenterAgent presenter)
    {
        _fetcher = fetcher;
        _processor = processor;
        _presenter = presenter;
    }

    public async Task<(ReportResponse Structured, string? Text)> RunAsync(ReportRequest req, CancellationToken ct)
    {
        var raw = await _fetcher.FetchAsync(req, ct);
        var processed = await _processor.ProcessAsync(raw!, req, ct);
        var text = await _presenter.PresentAsync(req, processed, ct);
        return (processed, text);
    }
}
