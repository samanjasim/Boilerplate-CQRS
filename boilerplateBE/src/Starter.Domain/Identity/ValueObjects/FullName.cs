using Starter.Domain.Common;
using Starter.Domain.Exceptions;

namespace Starter.Domain.Identity.ValueObjects;

public sealed class FullName : ValueObject
{
    public const int MaxFirstNameLength = 100;
    public const int MaxLastNameLength = 100;

    public string FirstName { get; }
    public string LastName { get; }

    private FullName(string firstName, string lastName)
    {
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
    }

    public static FullName Create(string firstName, string lastName)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            throw new DomainException("First name cannot be empty.", "INVALID_NAME");

        if (string.IsNullOrWhiteSpace(lastName))
            throw new DomainException("Last name cannot be empty.", "INVALID_NAME");

        firstName = firstName.Trim();
        lastName = lastName.Trim();

        if (firstName.Length > MaxFirstNameLength)
            throw new DomainException($"First name cannot exceed {MaxFirstNameLength} characters.", "INVALID_NAME");

        if (lastName.Length > MaxLastNameLength)
            throw new DomainException($"Last name cannot exceed {MaxLastNameLength} characters.", "INVALID_NAME");

        return new FullName(firstName, lastName);
    }

    public static bool TryCreate(string firstName, string lastName, out FullName? result)
    {
        result = null;

        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            return false;

        firstName = firstName.Trim();
        lastName = lastName.Trim();

        if (firstName.Length > MaxFirstNameLength || lastName.Length > MaxLastNameLength)
            return false;

        result = new FullName(firstName, lastName);
        return true;
    }

    public string GetFullName() => $"{FirstName} {LastName}";

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return FirstName.ToLowerInvariant();
        yield return LastName.ToLowerInvariant();
    }

    public override string ToString() => GetFullName();
}
