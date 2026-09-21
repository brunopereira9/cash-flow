using Core.Api.Application.Models;

namespace Core.Api.Application.Validation;

public static class EntryValidator
{
    public static Dictionary<string, string[]> Validate(CreateEntryRequest request) =>
        Validate(request.Amount, request.Type, request.Description, request.BusinessDate);

    public static Dictionary<string, string[]> Validate(UpdateEntryRequest request) =>
        Validate(request.Amount, request.Type, request.Description, request.BusinessDate);

    private static Dictionary<string, string[]> Validate(
        decimal amount,
        string? type,
        string? description,
        DateOnly? businessDate)
    {
        var errors = new Dictionary<string, string[]>();
        if (amount <= 0 || decimal.Round(amount, 2) != amount)
            errors["Amount"] = ["must be positive with at most two decimal places"];
        if (!string.Equals(type, "credit", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(type, "debit", StringComparison.OrdinalIgnoreCase))
            errors["Type"] = ["must be either credit or debit"];
        if (string.IsNullOrWhiteSpace(description)) errors["Description"] = ["required"];
        if (businessDate is DateOnly date && date > Today()) errors["BusinessDate"] = ["cannot be in the future"];
        return errors;
    }

    private static DateOnly Today() =>
        DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
                DateTime.UtcNow,
                "America/Sao_Paulo"));
}
