using System.Text.RegularExpressions;
using Starter.Domain.Common;
using Starter.Domain.Exceptions;

namespace Starter.Domain.Identity.ValueObjects;

public sealed partial class Email : ValueObject
{
    public const int MaxLength = 256;

    private static readonly Regex EmailRegex = MyEmailRegex();

    public string Value { get; }

    private Email(string value)
    {
        Value = value.ToLowerInvariant().Trim();
    }

    public static string Normalize(string email) => email.ToLowerInvariant().Trim();

    public static Email Create(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new DomainException("Email cannot be empty.", "INVALID_EMAIL");

        email = email.Trim();

        if (email.Length > MaxLength)
            throw new DomainException($"Email cannot exceed {MaxLength} characters.", "INVALID_EMAIL");

        if (!EmailRegex.IsMatch(email))
            throw new DomainException("Email format is invalid.", "INVALID_EMAIL");

        return new Email(email);
    }

    public static bool TryCreate(string email, out Email? result)
    {
        result = null;

        if (string.IsNullOrWhiteSpace(email))
            return false;

        email = email.Trim();

        if (email.Length > MaxLength)
            return false;

        if (!EmailRegex.IsMatch(email))
            return false;

        result = new Email(email);
        return true;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(Email email) => email.Value;

    [GeneratedRegex(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", RegexOptions.Compiled)]
    private static partial Regex MyEmailRegex();
}
