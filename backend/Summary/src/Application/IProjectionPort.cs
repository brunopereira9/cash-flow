namespace CashFlow.Summary.Application;

public interface IProjectionPort
{
    Task<bool> ApplyAsync(ProjectionEvent message, CancellationToken cancellationToken);
}
