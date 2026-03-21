namespace Starter.Shared.Results;

public sealed record ValidationError(string PropertyName, string ErrorMessage)
{
    public override string ToString() => $"{PropertyName}: {ErrorMessage}";
}

public sealed class ValidationErrors
{
    private readonly List<ValidationError> _errors = [];

    public IReadOnlyList<ValidationError> Errors => _errors.AsReadOnly();
    public bool HasErrors => _errors.Count > 0;

    public void Add(string propertyName, string errorMessage)
    {
        _errors.Add(new ValidationError(propertyName, errorMessage));
    }

    public void AddRange(IEnumerable<ValidationError> errors)
    {
        _errors.AddRange(errors);
    }

    public static ValidationErrors FromErrors(IEnumerable<ValidationError> errors)
    {
        var validationErrors = new ValidationErrors();
        validationErrors.AddRange(errors);
        return validationErrors;
    }

    public Dictionary<string, string[]> ToDictionary()
    {
        return _errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
    }
}
