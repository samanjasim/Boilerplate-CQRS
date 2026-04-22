using Starter.Domain.Common.Access.Enums;

namespace Starter.Domain.Common.Access;

public interface IShareable
{
    Guid Id { get; }
    ResourceVisibility Visibility { get; }
}
