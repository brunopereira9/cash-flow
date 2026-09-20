namespace Core.Api.Application.Models;

public sealed record CreateEntryRequest(
    decimal Amount,
    string? Type,
    string? Description,
    DateOnly? BusinessDate);
