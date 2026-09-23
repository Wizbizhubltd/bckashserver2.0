namespace BCKash.SharedKernel;

/// <summary>Matches the legacy created_at/updated_at columns present on nearly every table.</summary>
public interface IHasTimestamps
{
    DateTime? CreatedAt { get; set; }
    DateTime? UpdatedAt { get; set; }
}

/// <summary>Matches tables that additionally carry a soft-delete column (e.g. offices.deleted_at).</summary>
public interface ISoftDelete
{
    DateTime? DeletedAt { get; set; }
}
