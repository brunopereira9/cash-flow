using Summary.Api.Domain.Entities;
using Summary.Api.Domain.Events;

namespace Summary.Api.Application.Interfaces;

public interface ISummaryRepository
{
    Task<DailySummary?> FindDailyAsync(DateOnly date, CancellationToken token);
    Task<bool> ApplyProjectionAsync(ProjectionEvent message, CancellationToken token);
}
