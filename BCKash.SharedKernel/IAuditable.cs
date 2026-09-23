namespace BCKash.SharedKernel;

/// <summary>
/// Marker for entities whose create/update/delete must be recorded in audit_trail
/// (FR-SEC-6). Applied by the SaveChanges interceptor in BCKash.Infrastructure —
/// entities that don't implement this are never audited.
/// </summary>
public interface IAuditable
{
}
