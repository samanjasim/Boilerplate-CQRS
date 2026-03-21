namespace Starter.Domain.Common;

public abstract class BaseAuditableEntity<TId> : BaseEntity<TId> where TId : notnull
{
    public Guid? CreatedBy { get; protected set; }
    public Guid? ModifiedBy { get; protected set; }

    protected BaseAuditableEntity() : base() { }
    protected BaseAuditableEntity(TId id) : base(id) { }
}

public abstract class BaseAuditableEntity : BaseAuditableEntity<Guid>
{
    protected BaseAuditableEntity() : base() { }
    protected BaseAuditableEntity(Guid id) : base(id) { }
}
