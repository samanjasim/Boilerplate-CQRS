using System.Text.RegularExpressions;
using Starter.Domain.Common;
using Starter.Domain.Exceptions;

namespace Starter.Domain.Identity.ValueObjects;

public sealed partial class PhoneNumber : ValueObject
{
    public const int MaxLength = 20;

    private static readonly Regex PhoneRegex = MyPhoneRegex();

    public string Value { get; }

    private PhoneNumber(string value)
    {
        Value = value;
    }

    public static PhoneNumber Create(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            throw new DomainException("Phone number cannot be empty.", "INVALID_PHONE");

        var cleaned = CleanPhoneNumber(phoneNumber);

        if (cleaned.Length > MaxLength)
            throw new DomainException($"Phone number cannot exceed {MaxLength} characters.", "INVALID_PHONE");

        if (!PhoneRegex.IsMatch(cleaned))
            throw new DomainException("Phone number format is invalid.", "INVALID_PHONE");

        return new PhoneNumber(cleaned);
    }

    public static bool TryCreate(string phoneNumber, out PhoneNumber? result)
    {
        result = null;

        if (string.IsNullOrWhiteSpace(phoneNumber))
            return false;

        var cleaned = CleanPhoneNumber(phoneNumber);

        if (cleaned.Length > MaxLength)
            return false;

        if (!PhoneRegex.IsMatch(cleaned))
            return false;

        result = new PhoneNumber(cleaned);
        return true;
    }

    private static string CleanPhoneNumber(string phoneNumber)
    {
        return phoneNumber.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "").Trim();
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(PhoneNumber phoneNumber) => phoneNumber.Value;

    [GeneratedRegex(@"^\+?[1-9]\d{6,14}$", RegexOptions.Compiled)]
    private static partial Regex MyPhoneRegex();
}
