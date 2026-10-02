namespace NewDynamicFormGenAPI.Models.Common;

/// <summary>
/// Abstract base class for entities that require a database-generated identifier.
/// </summary>
/// <remarks>
/// This class serves as a foundation for domain entities that need to be persisted.
/// Subclasses inherit the primary key property and can extend it with domain-specific attributes.
/// </remarks>
public abstract class BaseEntity
{
    /// <summary>
    /// Gets or sets the primary key identifier for this entity.
    /// </summary>
    public int Id { get; set; }
}
