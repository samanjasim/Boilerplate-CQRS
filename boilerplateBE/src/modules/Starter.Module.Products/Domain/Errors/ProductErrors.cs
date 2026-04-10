using Starter.Shared.Results;

namespace Starter.Module.Products.Domain.Errors;

public static class ProductErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Products.NotFound",
        "The specified product was not found.");

    public static readonly Error SlugAlreadyExists = Error.Conflict(
        "Products.SlugAlreadyExists",
        "A product with this slug already exists for this tenant.");

    public static readonly Error CannotArchiveDraft = Error.Validation(
        "Products.CannotArchiveDraft",
        "Cannot archive a product that is still in draft status.");

    public static readonly Error AlreadyPublished = Error.Validation(
        "Products.AlreadyPublished",
        "This product is already published.");

    public static readonly Error AlreadyArchived = Error.Validation(
        "Products.AlreadyArchived",
        "This product is already archived.");

    public static Error QuotaExceeded(long limit) =>
        Error.Validation("Products.QuotaExceeded",
            $"Product limit of {limit} has been reached. Upgrade your plan to create more products.");
}
