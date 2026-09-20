public interface IProjectionPort
{
    Task<bool> ApplyAsync(ProjectionEvent message, CancellationToken cancellationToken);
}
