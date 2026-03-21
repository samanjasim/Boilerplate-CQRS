using System.Reflection;

namespace Starter.Domain.Primitives;

public abstract class Enumeration<TEnum> : IEquatable<Enumeration<TEnum>>
    where TEnum : Enumeration<TEnum>
{
    private static readonly Lazy<Dictionary<int, TEnum>> EnumerationsByValue = new(
        () => GetEnumerations().ToDictionary(e => e.Value));

    private static readonly Lazy<Dictionary<string, TEnum>> EnumerationsByName = new(
        () => GetEnumerations().ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase));

    public int Value { get; }
    public string Name { get; }

    protected Enumeration(int value, string name)
    {
        Value = value;
        Name = name;
    }

    public static TEnum? FromValue(int value)
    {
        return EnumerationsByValue.Value.GetValueOrDefault(value);
    }

    public static TEnum? FromName(string name)
    {
        return EnumerationsByName.Value.GetValueOrDefault(name);
    }

    public static IReadOnlyCollection<TEnum> GetAll()
    {
        return EnumerationsByValue.Value.Values.ToList().AsReadOnly();
    }

    public static bool TryFromValue(int value, out TEnum? enumeration)
    {
        return EnumerationsByValue.Value.TryGetValue(value, out enumeration);
    }

    public static bool TryFromName(string name, out TEnum? enumeration)
    {
        return EnumerationsByName.Value.TryGetValue(name, out enumeration);
    }

    public bool Equals(Enumeration<TEnum>? other)
    {
        if (other is null) return false;
        return GetType() == other.GetType() && Value == other.Value;
    }

    public override bool Equals(object? obj)
    {
        return obj is Enumeration<TEnum> other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    public override string ToString() => Name;

    public static bool operator ==(Enumeration<TEnum>? left, Enumeration<TEnum>? right)
    {
        if (left is null && right is null) return true;
        if (left is null || right is null) return false;
        return left.Equals(right);
    }

    public static bool operator !=(Enumeration<TEnum>? left, Enumeration<TEnum>? right)
    {
        return !(left == right);
    }

    private static IEnumerable<TEnum> GetEnumerations()
    {
        return typeof(TEnum)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(f => f.FieldType == typeof(TEnum))
            .Select(f => (TEnum)f.GetValue(null)!)
            .Where(e => e is not null);
    }
}
