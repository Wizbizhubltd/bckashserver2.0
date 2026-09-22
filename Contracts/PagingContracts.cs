namespace BCKash.Api.Contracts;

/// <summary>
/// The house pagination envelope (NFR-3: "API responses for list endpoints are paginated by
/// default; no unbounded result sets"), introduced by Phase 2's client list endpoint. Its own
/// file (not ClientContracts.cs) since Phase 3 (Groups) is expected to reuse it for its own
/// paginated list without importing a client-specific file.
/// </summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
