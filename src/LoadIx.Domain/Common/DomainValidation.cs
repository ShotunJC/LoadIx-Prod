namespace LoadIx.Domain.Common;

internal static class DomainValidation
{
    public static DomainError? Required(string? value, string fieldName, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new DomainError(
                $"{fieldName}.required",
                $"{fieldName} is required.");
        }

        return value.Trim().Length > maximumLength
            ? new DomainError(
                $"{fieldName}.too_long",
                $"{fieldName} cannot exceed {maximumLength} characters.")
            : null;
    }

    public static string? Optional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
