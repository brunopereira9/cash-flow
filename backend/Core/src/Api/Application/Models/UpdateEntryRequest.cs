namespace Core.Api.Application.Models;

public sealed record UpdateEntryRequest(
    decimal Amount,
    string? Type,
    string? Description,
    DateOnly? BusinessDate,
    int Version);
