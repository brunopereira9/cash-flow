using Summary.Api.Application.Interfaces;
using Summary.Api.Domain.Events;

namespace Summary.Api.Application.Services;

public sealed class ProjectionService(ISummaryRepository repository)
{
    public Task<bool> ApplyAsync(ProjectionEvent message, CancellationToken token) =>
        repository.ApplyProjectionAsync(message, token);
}
