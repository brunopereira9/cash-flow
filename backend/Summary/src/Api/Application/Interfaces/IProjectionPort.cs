using Summary.Api.Domain.Events;

namespace Summary.Api.Application.Interfaces;

public interface IProjectionPort
{
    Task<bool> ApplyAsync(ProjectionEvent message, CancellationToken cancellationToken);
}